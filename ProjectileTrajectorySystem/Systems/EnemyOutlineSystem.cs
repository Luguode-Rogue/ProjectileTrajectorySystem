// 敌人轮廓高亮系统：瞄准玩家的敌方远程单位红色描边
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProjectileTrajectorySystem
{
    internal static class EnemyOutlineSystem
    {
        private static readonly Dictionary<Agent, uint?> _cache = new();

        public static void Update()
        {
            Agent player = Agent.Main;
            if (player == null) return;

            var aiming = FindAimingEnemies(player);

            uint? targetColor = new Color(1f, 0f, 0f, 1f).ToUnsignedInteger();
            foreach (var enemy in aiming)
                Apply(enemy, targetColor);

            // 清理不再瞄准的敌人
            var toRemove = new List<Agent>();
            foreach (var kvp in _cache)
            {
                if (!aiming.Contains(kvp.Key))
                {
                    ResetContourSafe(kvp.Key);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var key in toRemove)
                _cache.Remove(key);
        }

        private static void ResetContourSafe(Agent agent)
        {
            if (!MissionSafety.CanTouchVisuals(agent)) return;
            try { agent.AgentVisuals.SetContourColor(null, true); }
            catch { }
        }

        private static List<Agent> FindAimingEnemies(Agent player)
        {
            var result = new List<Agent>();
            foreach (Agent agent in Mission.Current.Agents)
            {
                if (agent == null || !agent.IsActive() || agent == player) continue;
                if (agent.Team == null || agent.Team.TeamIndex == -1
                    || player.Team == null || !agent.Team.IsEnemyOf(player.Team)) continue;

                MissionWeapon weapon = agent.WieldedWeapon;
                if (weapon.IsEmpty || !weapon.CurrentUsageItem.IsRangedWeapon) continue;

                Agent target = agent.GetTargetAgent();
                if (target == null || target != player) continue;

                if (agent.GetCurrentActionStage(1) == Agent.ActionStage.AttackReady)
                    result.Add(agent);
            }
            return result;
        }

        private static void Apply(Agent agent, uint? color)
        {
            if (_cache.TryGetValue(agent, out var current) && current == color)
                return;

            if (!MissionSafety.CanTouchVisuals(agent)) return;

            try { agent.AgentVisuals.SetContourColor(color, true); }
            catch { return; }
            _cache[agent] = color;

            // 骑马敌人：坐骑也描边
            Agent mount = agent.MountAgent;
            if (mount == null) return;
            if (_cache.TryGetValue(mount, out var mountColor) && mountColor == color) return;
            if (!MissionSafety.CanTouchVisuals(mount)) return;

            try
            {
                mount.AgentVisuals.SetContourColor(color, true);
                _cache[mount] = color;
            }
            catch { }
        }

        /// <summary>
        /// 清空描边缓存。
        /// </summary>
        /// <param name="restoreVisuals">
        /// 是否顺带把描边还原。Mission 结束（OnEndMission）时必须传 false：
        /// 此时引擎已释放 Agent 的原生视觉对象，再去写入会造成访问已释放内存
        /// （AccessViolationException），且该异常无法通过 try/catch 拦截。
        /// </param>
        public static void Clear(bool restoreVisuals = true)
        {
            if (restoreVisuals)
            {
                foreach (var kvp in _cache)
                    ResetContourSafe(kvp.Key);
            }
            _cache.Clear();
        }
    }
}
