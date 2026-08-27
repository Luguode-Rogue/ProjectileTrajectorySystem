// 移动目标预瞄系统：迭代拟合计算提前量，绘制预测线和标记
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProjectileTrajectorySystem
{
    internal static class LeadPredictionSystem
    {
        private const string PREDICTION_MESH = "mangonel_mapicon_projectile";

        public static void Update(Agent player)
        {
            if (!ProjectileTrajectorySettingsManager.Settings.EnableLeadPrediction)
            {
                Clear();
                return;
            }

            if (player == null || !player.IsActive())
            {
                Clear();
                return;
            }

            // 1. 获取玩家武器
            EquipmentIndex weaponIndex = player.GetPrimaryWieldedItemIndex();
            if (weaponIndex == EquipmentIndex.None)
            {
                Clear();
                return;
            }

            MissionWeapon weapon = player.Equipment[weaponIndex];
            if (weapon.IsEmpty || !weapon.CurrentUsageItem.IsRangedWeapon)
            {
                Clear();
                return;
            }

            // 2. 选择最近的敌人
            Agent targetEnemy = GetNearestEnemy(player);
            if (targetEnemy == null)
            {
                Clear();
                return;
            }

            // 3. 获取弹速
            float projectileSpeed = (float)weapon.GetModifiedMissileSpeedForCurrentUsage()
                * player.AgentDrivenProperties.MissileSpeedMultiplier;

            // 4. 核心计算
            Vec3 predictedPosition = LeadPredictionMath.CalculateLeadPosition(
                player, targetEnemy, projectileSpeed);

            if (predictedPosition == Vec3.Zero && predictedPosition == Vec3.Invalid)
            {
                Clear();
                return;
            }

            // 5. 绘制预测标记
            DrawMarker(predictedPosition);

            // 6. 绘制预测弹道线
            ProjectileTrajectorySystem.DrawLeadTrajectory(player, predictedPosition, projectileSpeed);
        }

        private static Agent GetNearestEnemy(Agent player)
        {
            Agent nearest = null;
            float nearestDist = float.MaxValue;

            foreach (Agent agent in Mission.Current.Agents)
            {
                if (agent == player || !agent.IsHuman || agent.IsFriendOf(Agent.Main))
                    continue;

                float dist = (agent.Position - player.Position).LengthSquared;
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = agent;
                }
            }
            return nearest;
        }

        private static void DrawMarker(Vec3 pos)
        {
            if (SkillSystemBehavior.PredictionMarker == null)
            {
                try
                {
                    SkillSystemBehavior.PredictionMarker = GameEntity.Instantiate(
                        Mission.Current.Scene, PREDICTION_MESH, true);
                }
                catch { }
            }

            if (SkillSystemBehavior.PredictionMarker != null)
            {
                SkillSystemBehavior.PredictionMarker.SetLocalPosition(pos);
            }
        }

        /// <param name="removeEntities">
        /// 是否真正移除原生实体。Mission 卸载（OnEndMission）时必须传 false：
        /// 此时实体已随场景销毁，调用 Remove / SetLocalPosition 会访问已释放内存，
        /// 触发无法被 try/catch 捕获的 AccessViolationException。
        /// </param>
        public static void Clear(bool removeEntities = true)
        {
            if (removeEntities && MissionSafety.IsSceneAlive())
            {
                foreach (var kv in SkillSystemBehavior.LeadPredictionLine.ToList())
                {
                    var entity = kv.Value;
                    if (!MissionSafety.CanTouchEntity(entity)) continue;
                    try { entity.Remove(0); } catch { }
                }

                var marker = SkillSystemBehavior.PredictionMarker;
                if (MissionSafety.CanTouchEntity(marker))
                {
                    try { marker.SetLocalPosition(new Vec3(float.MaxValue)); } catch { }
                }
            }

            SkillSystemBehavior.LeadPredictionLine.Clear();

            // PredictionMarker 是 static 字段，跨 Mission 不会自动重置。
            // 不置空的话，下一场战斗会拿到上一场已销毁的实体引用。
            if (!removeEntities)
                SkillSystemBehavior.PredictionMarker = null;
        }
    }
}
