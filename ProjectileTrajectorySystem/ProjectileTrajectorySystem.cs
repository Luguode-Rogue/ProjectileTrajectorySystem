// 投射物弹道计算与可视化系统 —— 薄外观层
// 所有 public 方法签名保持不变，内部委托给 Core/Systems 子系统
// 支持：玩家远程武器、攻城武器、敌方 AI 预测、投射物弹道、GameEntity/DebugLine 双显示管线
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProjectileTrajectorySystem
{
    public static class ProjectileTrajectorySystem
    {
        // ===== 玩家 / 攻城武器弹道 (public API) =====

        public static void UpdateTrajectory(Agent agent, RangedSiegeWeapon siegeWeapon = null)
        {
            if (agent == null || !agent.IsActive()) return;
            if (siegeWeapon == null) return;

            PlayerTrajectorySystem.Update(agent); // 委托给子系统
        }

        public static void UpdateTrajectoryRangeWeapon(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return;
            PlayerTrajectorySystem.Update(agent);
        }

        // ===== 敌人预测弹道 (public API) =====

        public static void UpdateEnemyTrajectory(Agent enemy)
        {
            EnemyTrajectorySystem.UpdateSingle(enemy);
        }

        public static void ClearEnemyTrajectory(int agentIndex)
        {
            EnemyTrajectorySystem.ClearTrajectory(agentIndex);
        }

        // ===== 轨迹清理 (public API) =====

        public static void ClearNormalTrajectory()
        {
            PerformanceDebugger.OnClearNormalTrajectory();
            foreach (var e in SkillSystemBehavior.WoW_Line.Values)
                GameEntityPool.Return(e);
            SkillSystemBehavior.WoW_Line.Clear();
        }

        // ===== DebugLine (public API) =====

        public static void DrawDebugLineTrajectory(List<Vec3> points, uint color)
        {
            TrajectoryRenderer.DrawDebugLineTrajectory(points, color);
        }

        public static void RenderDebugLine(
            Vec3 position, Vec3 direction, uint color, bool depthCheck, float time)
        {
            TrajectoryRenderer.RenderDebugLine(position, direction, color, depthCheck, time);
        }

        // ===== 投射物弹道 (public API) =====

        public static void ClearMissileTrajectory(int missileIndex)
        {
            if (!SkillSystemBehavior.MissileTrajectoryLines.TryGetValue(missileIndex, out var line))
                return;

            foreach (var e in line.Values)
                GameEntityPool.Return(e);
            line.Clear();
            SkillSystemBehavior.MissileTrajectoryLines.Remove(missileIndex);
        }

        public static void UpdateMissileTrajectory(
            int missileIndex, Vec3 startPos, Vec3 direction, float speed,
            float airFriction, float maxTime = 3.0f, float timeStep = 0.1f)
        {
            var missileLine = SkillSystemBehavior.GetOrCreateMissileLine(missileIndex);
            var trajectoryPoints = SimulateMissileTrajectory(
                startPos, direction, speed, airFriction, timeStep, maxTime,
                0f, missileLine, 0xFF00FF00u);

            if (trajectoryPoints != null)
                SkillSystemBehavior.StoreMissileTrajectoryPoints(missileIndex, trajectoryPoints);
        }

        // ===== 移动目标预瞄 (public API) =====

        public static void DrawLeadTrajectory(Agent player, Vec3 targetPredictedPos, float speed)
        {
            var settings = ProjectileTrajectorySettingsManager.Settings;
            if (!settings.UseDebugLineDisplay && !settings.UseGameEntityDisplay)
                return;

            Vec3 direction = targetPredictedPos - player.GetEyeGlobalPosition();
            direction.Normalize();

            PlayerTrajectorySystem.SimulateAndRender(
                player.GetEyeGlobalPosition(), direction, speed,
                0.3f, 4.8f, 0.15f, 0f,
                customLine: SkillSystemBehavior.LeadPredictionLine,
                baseColor: 0xFFFF0000u);
        }

        public static Vec3 CalculateLeadPosition(Agent player, Agent target, float projectileSpeed)
        {
            return LeadPredictionMath.CalculateLeadPosition(player, target, projectileSpeed);
        }

        public static Vec3 CalculateProjectileFiringSolution(Vec3 start, Vec3 end, float speed, float gravity)
        {
            return TrajectoryPhysics.CalculateFiringSolution(start, end, speed, gravity);
        }

        // ===== 攻城武器反射接口 (public API) =====

        public static float GetSiegeShootingSpeed(RangedSiegeWeapon weapon)
        {
            return SiegeWeaponHelper.GetShootingSpeed(weapon);
        }

        public static Vec3 GetSiegeShootingDirection(RangedSiegeWeapon weapon)
        {
            return SiegeWeaponHelper.GetShootingDirection(weapon);
        }

        public static Vec3 GetSiegeProjectileStartPosition(RangedSiegeWeapon weapon)
        {
            return SiegeWeaponHelper.GetProjectileStartPosition(weapon);
        }

        public static float GetWeaponAirFriction(Agent agent, out EquipmentIndex _)
        {
            return SiegeWeaponHelper.GetAirFriction(agent, out _);
        }

        // ===== 内部：投射物弹道模拟 =====

        private static List<Vec3> SimulateMissileTrajectory(
            Vec3 startPos, Vec3 direction, float speed, float airFriction,
            float timeStep, float maxTime, float timeKeyOffset,
            Dictionary<float, GameEntity> missileLine, uint color)
        {
            var settings = ProjectileTrajectorySettingsManager.Settings;
            if (!settings.UseGameEntityDisplay && !settings.UseDebugLineDisplay)
                return null;

            direction.Normalize();
            Vec3 previousPos = startPos;
            List<Vec3> calculatedPoints = new List<Vec3>();

            for (float t = 0f; t <= maxTime; t += timeStep)
            {
                Vec3 currentPos = TrajectoryPhysics.CalculatePosition(
                    startPos, direction, speed, t, 0f, airFriction);
                calculatedPoints.Add(currentPos);

                if (settings.UseGameEntityDisplay)
                    TrajectoryRenderer.PlaceGameEntityMarker(
                        currentPos, t + timeKeyOffset, missileLine, color);

                if (settings.UseDebugLineDisplay)
                    TrajectoryRenderer.DrawDebugLineSegment(
                        previousPos, currentPos - previousPos, color, 0f);

                previousPos = currentPos;
            }

            return calculatedPoints;
        }
    }
}
