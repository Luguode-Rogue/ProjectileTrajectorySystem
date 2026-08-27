// 投射物弹道系统：基于 OnAgentShootMissile 的一次性弹道计算与持续显示
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProjectileTrajectorySystem
{
    internal static class MissileTrajectorySystem
    {
        /// <summary>处理 Agent 发射投射物事件</summary>
        public static void OnAgentShootMissile(
            Agent shooterAgent, EquipmentIndex weaponIndex,
            Vec3 position, Vec3 velocity, Mat3 orientation,
            bool hasRigidBody, int forcedMissileIndex)
        {
            var settings = ProjectileTrajectorySettingsManager.Settings;
            if (!settings.EnableTrajectory || !settings.MissileTrajectory)
                return;

            Agent playerAgent = Agent.Main;
            if (playerAgent == null) return;

            // 判断是否相关投射物：玩家发射 或 敌人瞄准玩家
            bool isPlayerProjectile = shooterAgent == playerAgent;
            bool isEnemyTargetingPlayer = shooterAgent != playerAgent
                && shooterAgent.Team != null && playerAgent.Team != null
                && shooterAgent.Team.IsEnemyOf(playerAgent.Team)
                && shooterAgent.GetTargetAgent() == playerAgent;

            if (!isPlayerProjectile && !isEnemyTargetingPlayer)
                return;

            if (shooterAgent == null || !shooterAgent.IsActive())
                return;

            // 获取新生成的 Missile
            var missilesList = Mission.Current.MissilesList;
            if (missilesList.Count == 0) return;

            var newMissile = missilesList[missilesList.Count - 1];
            if (newMissile == null) return;

            int missileIndex = newMissile.Index;
            Vec3 startPos = position;
            float speed = velocity.Length;
            Vec3 direction = speed > 0 ? velocity.NormalizedCopy() : orientation.f;
            float airFriction = SiegeWeaponHelper.GetAirFriction(shooterAgent, out _);

            // 缓存初速
            var weapon = shooterAgent.Equipment[weaponIndex];
            if (!weapon.IsEmpty)
            {
                var weaponItem = weapon.Item;
                var ammoItem = newMissile.Weapon.Item;
                int agentIndex = shooterAgent.Index;
                string weaponId = weaponItem?.Name.ToString();
                string ammoId = ammoItem?.Name.ToString();
                var key = (agentIndex, weaponId, ammoId);

                if (!SkillSystemBehavior.ProjectileSpeedCache.ContainsKey(key))
                    SkillSystemBehavior.ProjectileSpeedCache[key] = speed;
            }

            // 一次性计算并更新轨迹
            ProjectileTrajectorySystem.UpdateMissileTrajectory(
                missileIndex, startPos, direction, speed, airFriction);
        }

        /// <summary>处理投射物命中事件，清理对应轨迹</summary>
        public static void OnMissileHit(
            Agent attacker, Agent victim, bool isCanceled,
            AttackCollisionData collisionData)
        {
            int missileIndex = collisionData.AffectorWeaponSlotOrMissileIndex;
            if (missileIndex < 0) return; // 非 Missile（装备槽 = -1）

            var settings = ProjectileTrajectorySettingsManager.Settings;
            if (!settings.EnableTrajectory || !settings.MissileTrajectory)
                return;

            // 清理 GameEntity 轨迹
            if (settings.UseGameEntityDisplay)
                ProjectileTrajectorySystem.ClearMissileTrajectory(missileIndex);

            // 清理 DebugLine 轨迹点
            if (settings.UseDebugLineDisplay)
                SkillSystemBehavior.StoredMissileTrajectoryPoints.Remove(missileIndex);
        }

        /// <summary>每帧绘制所有已存储的投射物 DebugLine 轨迹</summary>
        public static void DrawAllStoredTrajectories()
        {
            var settings = ProjectileTrajectorySettingsManager.Settings;
            if (!settings.EnableTrajectory || !settings.MissileTrajectory || !settings.UseDebugLineDisplay)
                return;

            foreach (var kvp in SkillSystemBehavior.StoredMissileTrajectoryPoints)
            {
                TrajectoryRenderer.DrawDebugLineTrajectory(kvp.Value, 0xFF00FF00u); // 绿色
            }
        }
    }
}
