// 主行为入口：薄编排层，将 OnMissionTick 等事件委托给各子系统处理
// 保留所有 public static 字段和方法以维持外部 API 兼容
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using static TaleWorlds.Library.MathF;

namespace ProjectileTrajectorySystem
{
    public class SkillSystemBehavior : MissionLogic
    {
        // ===== 以下为 public API 必须保留的静态字段 =====

        // 移动目标预瞄
        public static readonly Dictionary<float, GameEntity> LeadPredictionLine = new();
        public static GameEntity PredictionMarker = null;

        // 敌人运动数据缓存
        public static readonly Dictionary<Agent, AgentMotionData> TrackedAgents = new();

        public class AgentMotionData
        {
            public Vec3 LastPosition;
            public Vec3 Velocity;
            public float LastUpdateTime;
        }

        public class AgentMissileSpeedData
        {
            public Agent Agent;
            public MissionWeapon Weapon;
            public float MissileSpeed;
        }

        public static readonly Dictionary<int, List<AgentMissileSpeedData>> WoW_AgentMissileSpeedData = new();
        public static readonly Dictionary<float, GameEntity> WoW_Line = new();              // 玩家/攻城弹道
        public static readonly List<GameEntity> WoW_CustomGameEntity = new();
        public static readonly Dictionary<int, Dictionary<float, GameEntity>> EnemyTrajectoryLines = new();
        public static readonly Dictionary<int, List<Vec3>> StoredMissileTrajectoryPoints = new();
        public static readonly Dictionary<int, Dictionary<float, GameEntity>> MissileTrajectoryLines = new();
        public static readonly Dictionary<GameEntity, uint> GameEntityColorCache = new();
        public static readonly Dictionary<(int agentIndex, string weaponId, string ammoId), float> ProjectileSpeedCache = new();

        // Debug
        public static string debugString1 = "";
        public static string debugString2 = "";

        // 攻城武器落点视角相机系统（飞行到落点上空 + 红边高亮目标标记）
        public static SiegeLandingCameraSystem SiegeLanding;

        // 实例级子系统
        private readonly SlowMotionSystem _slowMotion = new SlowMotionSystem();

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        // ===== 生命周期 =====

        public override void OnCreated()
        {
            // 渲染器初始化（预缓存模板实体）
            TrajectoryRenderer.Initialize(Mission.Current);

            WoW_Line.Clear();
            WoW_CustomGameEntity.Clear();
            EnemyTrajectoryLines.Clear();
            MissileTrajectoryLines.Clear();
            StoredMissileTrajectoryPoints.Clear();
            GameEntityColorCache.Clear();
            ProjectileSpeedCache?.Clear();
            GameEntityPool.Clear();
            EnemyOutlineSystem.Clear();
            AlphaBlurSystem.RestoreAll();

            PerformanceDebugger.ResetAll();

        }

        protected override void OnEndMission()
        {

            WoW_Line.Clear();
            WoW_CustomGameEntity.Clear();
            EnemyTrajectoryLines.Clear();
            MissileTrajectoryLines.Clear();
            StoredMissileTrajectoryPoints.Clear();
            GameEntityColorCache.Clear();
            ProjectileSpeedCache?.Clear();
            GameEntityPool.Clear();

            // Mission 结束阶段引擎已释放 Agent/GameEntity 的原生视觉对象，
            // 此时只能丢弃缓存，绝不能再回写描边或 Alpha，否则会访问已释放内存触发
            // AccessViolationException（该异常无法被 try/catch 捕获）。
            EnemyOutlineSystem.Clear(restoreVisuals: false);
            AlphaBlurSystem.RestoreAll(restoreVisuals: false);
            LeadPredictionSystem.Clear(removeEntities: false);

            // 攻城武器落点视角相机系统：Mission 结束时必须销毁，避免后续在失效对象上触发 AV
            if (SiegeLanding != null)
            {
                try { SiegeLanding.Destroy(); } catch (Exception) { }
                SiegeLanding = null;
            }

            // 渲染器清理
            TrajectoryRenderer.Shutdown();
        }

        // ===== 主 Tick =====

        public override void OnMissionTick(float dt)
        {
            ProjectileTrajectorySettingsManager.ProcessPendingReload();

            var settings = ProjectileTrajectorySettingsManager.Settings;
            if (!settings.EnableTrajectory) return;

            Agent player = Agent.Main;
            if (player == null) return;

            // 1. 敌人弹道预测 (旧版)
            if (settings.EnemyTrajectory)
                EnemyTrajectorySystem.Update();

            // 2. 玩家弹道
            if (settings.PlayerTrajectory)
                PlayerTrajectorySystem.Update(player);

            // 3. 敌人高亮
            if (settings.EnemyHighlight)
                EnemyOutlineSystem.Update();

            // 4. 投射物弹道 DebugLine 持续绘制
            if (settings.MissileTrajectory)
                MissileTrajectorySystem.DrawAllStoredTrajectories();

            // 5. Alpha虚化平滑更新
            AlphaBlurSystem.UpdateSmoothing();

            // 6. 移动目标预瞄
            if (settings.EnableLeadPrediction)
                LeadPredictionSystem.Update(player);

            // 7. 敌人运动数据更新
            UpdateEnemyMotionData(dt);

            // 8. 慢动作
            _slowMotion.Update();

            // 9. 性能诊断收尾（统计 + 输出）
            PerformanceDebugger.EndOfFrame();

            // 10. 攻城武器落点视角（飞行到落点上空 + 红边高亮目标标记）
            UpdateSiegeLandingView(dt, settings);
        }

        /// <summary>
        /// 落点视角：按下配置按键切换相机飞往当前攻城武器预期落点（看向地面），
        /// 并在落点处显示红色描边的目标标记。
        /// </summary>
        private void UpdateSiegeLandingView(float dt, ProjectileTrajectorySettingsData settings)
        {
            if (!settings.EnableSiegeLandingView) return;

            // 懒初始化相机系统（需要 MissionScreen）。仅在 Mission 仍活跃时创建，
            // 避免在 Mission 卸载/切换阶段对正在死亡的 MissionScreen 接管 CustomCamera 触发 AV。
            if (SiegeLanding == null)
            {
                Mission activeMission = Mission.Current;
                if (activeMission == null || activeMission.MissionIsEnding || activeMission.Scene == null)
                    return;
                var ms = ScreenManager.TopScreen as MissionScreen;
                if (ms == null || ms.CombatCamera == null) return;
                SiegeLanding = new SiegeLandingCameraSystem(ms);
            }

            if (Input.IsKeyPressed(settings.SiegeLandingViewKey))
            {
                SiegeLanding.Toggle();
            }

            if (SiegeLanding.IsActive)
            {
                // 进入落点视角后，鼠标滚轮上下调整镜头俯视高度
                float scrollDelta = Input.DeltaMouseScroll;
                if (scrollDelta != 0f)
                    SiegeLanding.AdjustHeight(scrollDelta);
                SiegeLanding.Tick(dt);
            }
        }

        // ===== 投射物事件 =====

        public override void OnAgentShootMissile(
            Agent shooterAgent, EquipmentIndex weaponIndex,
            Vec3 position, Vec3 velocity, Mat3 orientation,
            bool hasRigidBody, int forcedMissileIndex)
        {
            base.OnAgentShootMissile(shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex);

            if (shooterAgent == Agent.Main)
            {
                debugString2 = $"{shooterAgent.GetEyeGlobalPosition()} {velocity.Length}";
            }

            MissileTrajectorySystem.OnAgentShootMissile(
                shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex);
        }

        public override void OnMissileHit(
            Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
        {
            MissileTrajectorySystem.OnMissileHit(attacker, victim, isCanceled, collisionData);
        }

        // ===== 敌人运动数据更新（供预瞄系统使用） =====

        private void UpdateEnemyMotionData(float dt)
        {
            Agent player = Agent.Main;
            if (player == null) return;

            foreach (Agent agent in Mission.Current.Agents)
            {
                bool usable;
                try { usable = agent != player && agent.IsActive() && agent.IsHuman; }
                catch (NullReferenceException) { continue; }
                if (!usable) continue;

                Vec3 currentPos = agent.Position;
                float currentTime = Mission.Current.CurrentTime;

                if (!TrackedAgents.ContainsKey(agent))
                {
                    TrackedAgents[agent] = new AgentMotionData
                    {
                        LastPosition = currentPos,
                        LastUpdateTime = currentTime
                    };
                    continue;
                }

                AgentMotionData data = TrackedAgents[agent];
                float timeDelta = currentTime - data.LastUpdateTime;

                if (timeDelta > 0.01f)
                {
                    data.Velocity = (currentPos - data.LastPosition) / timeDelta;
                    data.LastUpdateTime = currentTime;
                    data.LastPosition = currentPos;
                }
            }
        }

        // ===== GameEntity 对象池（保留为 public API 兼容） =====

        internal static GameEntity GetOrCreateGameEntity()
        {
            return GameEntityPool.GetOrCreate();
        }

        internal static void ReturnGameEntityToPool(GameEntity entity)
        {
            GameEntityPool.Return(entity);
        }

        internal static Dictionary<float, GameEntity> GetOrCreateMissileLine(int missileIndex)
        {
            if (!MissileTrajectoryLines.TryGetValue(missileIndex, out var line))
            {
                line = new Dictionary<float, GameEntity>();
                MissileTrajectoryLines[missileIndex] = line;
            }
            return line;
        }

        internal static void StoreMissileTrajectoryPoints(int missileIndex, List<Vec3> points)
        {
            if (points != null && points.Count > 0)
                StoredMissileTrajectoryPoints[missileIndex] = new List<Vec3>(points);
        }

        internal static void ClearStoredMissileTrajectoryPoints(int missileIndex)
        {
            StoredMissileTrajectoryPoints.Remove(missileIndex);
        }

        // ===== Alpha虚化（保留 public static 签名供外部/DLC 调用） =====

        public static void SetSiegeTargetAlpha(RangedSiegeWeapon siege, float targetAlpha)
        {
            AlphaBlurSystem.SetSiegeTargetAlpha(siege, targetAlpha);
        }

        // ===== 缓存辅助 =====

        public bool TryGetCachedMissileSpeed(Agent agent, string weapon, string ammo, out float speed)
        {
            speed = 0f;
            if (agent == null) return false;
            return ProjectileSpeedCache.TryGetValue((agent.Index, weapon, ammo), out speed);
        }
    }
}
