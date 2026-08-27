// 敌人弹道追踪系统：扫描瞄准玩家的敌人，显示预测射击线
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProjectileTrajectorySystem
{
    internal static class EnemyTrajectorySystem
    {
        private static readonly List<Agent> _tracked = new();
        private static bool _hasCollided;
        private static float _airFriction;

        private static int MaxTracked => ProjectileTrajectorySettingsManager.Settings.MaxTrackedEnemiesLegacy;

        public static void Update()
        {
            Agent player = Agent.Main;
            if (player == null) return;

            UpdateTracking(player);
            UpdateTrajectories();
        }

        // 安全的"是否可安全调用原生方法"判据。
        // 注意：不能使用 agent == null 判定！崩溃 agent 的托管对象本身非 null（真实存在），
        // 真正失效的是底层原生句柄（死亡/移除中间态）。
        // 这种失效 agent 调用 IsActive() 会访问已释放内存，抛出的往往是 AccessViolationException
        // （诊断树可见大量属性抛 AV），而不仅是 NullReferenceException——单纯 catch NRE 拦不住 AV，会崩溃。
        //
        // 判据采用原版标准做法：直接调用 Agent.IsActive()（原版引擎判定 agent 原生句柄是否仍有效的公开方法），
        // 并用 [HandleProcessCorruptedStateExceptions] + catch 兜底 AV/NRE。
        // 旧版里有人用 agent.Pointer 字段做前置短路，但 Pointer 是原生字段、在新版引擎已改名/私有化，
        // 编译期引用会报 CS1061，且它并非原版标准 API，故此处不依赖它，统一走 IsActive()。
        [HandleProcessCorruptedStateExceptions]
        private static bool IsAgentUsable(Agent agent)
        {
            if (agent == null||agent.Index==null) return false;
            try
            {
                return agent.IsActive();
            }
            catch (NullReferenceException)
            {
                return false;
            }
            catch (AccessViolationException)
            {
                return false;
            }
        }

        private static void UpdateTracking(Agent player)
        {
            // 清理失效
            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                Agent tracked = _tracked[i];
                if (!IsAgentUsable(tracked) || !IsAimingPlayer(tracked, player))
                {
                    try { ClearTrajectory(tracked.Index); }
                    catch (NullReferenceException) { }
                    _tracked.RemoveAt(i);
                }
            }

            if (_tracked.Count >= MaxTracked) return;

            foreach (Agent agent in Mission.Current.Agents)
            {
                if (_tracked.Count >= MaxTracked) break;
                if (_tracked.Contains(agent)) continue;
                // 先用安全判据过滤掉句柄已失效的 agent，避免进入 IsAimingPlayer 后崩溃
                if (!IsAgentUsable(agent)) continue;
                if (IsAimingPlayer(agent, player))
                    _tracked.Add(agent);
            }
        }

        private static void UpdateTrajectories()
        {
            foreach (Agent enemy in _tracked)
                UpdateSingle(enemy);
        }

        [HandleProcessCorruptedStateExceptions]
        public static void UpdateSingle(Agent enemy)
        {
            if (enemy == null) return;
            // 原生句柄已释放时不调用任何原生方法（见 IsAgentUsable 说明）
            try
            {
                if (!enemy.IsActive()) return;

                EquipmentIndex weaponIndex = enemy.GetPrimaryWieldedItemIndex();
                if (weaponIndex == EquipmentIndex.None) return;

                MissionWeapon weapon = enemy.Equipment[weaponIndex];
                if (weapon.IsEmpty || !weapon.CurrentUsageItem.IsRangedWeapon) return;

                var line = GetOrCreateLine(enemy.Index);
                _hasCollided = false;

                float speed = weapon.GetModifiedMissileSpeedForCurrentUsage()
                    * enemy.AgentDrivenProperties.MissileSpeedMultiplier;
                _airFriction = SiegeWeaponHelper.GetAirFriction(enemy, out _);

                var activeKeys = new HashSet<float>();
                PlayerTrajectorySystem.SimulateAndRender(
                    enemy.GetEyeGlobalPosition(), enemy.LookDirection, speed,
                    0.3f, 3.0f, 0.2f, 0f,
                    customLine: line, baseColor: 0xFFFF4444u, activeKeys: activeKeys);

                // 裁剪多余实体
                TrajectoryRenderer.PruneUnusedEntities(line, activeKeys);
            }
            catch (NullReferenceException)
            {
                // agent 原生句柄在渲染过程中失效（死亡/移除中间态），放弃本次绘制
            }
            catch (AccessViolationException)
            {
                // 同上，但为 AccessViolationException（已释放内存访问），放弃本次绘制
            }
        }

        public static void ClearTrajectory(int agentIndex)
        {
            if (!SkillSystemBehavior.EnemyTrajectoryLines.TryGetValue(agentIndex, out var line)) return;

            PerformanceDebugger.OnClearEnemyTrajectory();
            foreach (var e in line.Values)
                GameEntityPool.Return(e);
            line.Clear();
            SkillSystemBehavior.EnemyTrajectoryLines.Remove(agentIndex);
        }

        private static Dictionary<float, GameEntity> GetOrCreateLine(int id)
        {
            if (!SkillSystemBehavior.EnemyTrajectoryLines.TryGetValue(id, out var line))
            {
                line = new Dictionary<float, GameEntity>();
                SkillSystemBehavior.EnemyTrajectoryLines[id] = line;
            }
            return line;
        }

        [HandleProcessCorruptedStateExceptions]
        public static bool IsAimingPlayer(Agent agent, Agent player)
        {
            if (agent == null || player == null) return false;

            // 防御：agent 处于"非 null 但原生句柄已失效"的死亡/移除中间态时，
            // IsActive / GetTargetAgent / WieldedWeapon / GetCurrentActionStage 等原生调用
            // 会在访问已释放的原生句柄时抛 NullReferenceException（甚至 AccessViolationException）。
            // 该版本 Agent 无 IsValid/IsRemoved 等托管判空属性，故统一调用原版 IsActive() 并用 try/catch 兜底，
            // catch 内绝不再触碰 agent（否则会二次崩溃）。
            try
            {
                if (!agent.IsActive()) return false;
                if (agent.Team == null || player.Team == null
                    || !agent.Team.IsValid || !player.Team.IsValid) return false;
                if (!agent.Team.IsEnemyOf(player.Team)) return false;

                Agent target = agent.GetTargetAgent();
                if (target == null || target != player) return false;

                MissionWeapon weapon = agent.WieldedWeapon;
                if (weapon.IsEmpty || !weapon.CurrentUsageItem.IsRangedWeapon) return false;

                return agent.GetCurrentActionStage(1) == Agent.ActionStage.AttackReady;
            }
            catch (NullReferenceException)
            {
                return false;
            }
            catch (AccessViolationException)
            {
                return false;
            }
        }
    }
}
