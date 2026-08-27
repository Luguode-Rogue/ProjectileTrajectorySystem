// 移动目标预瞄算法：迭代拟合计算提前量
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;


namespace ProjectileTrajectorySystem
{
    internal static class LeadPredictionMath
    {
        private const int MaxIterations = 5;
        private const float ConvergenceThreshold = 0.001f;
        private const float Gravity = 9.806f; // 与引擎 MBGlobals.Gravity 一致

        /// <summary>计算移动目标的预测位置（提前量）</summary>
        public static Vec3 CalculateLeadPosition(Agent player, Agent target, float projectileSpeed)
        {
            Vec3 shooterPos = player.GetEyeGlobalPosition();
            Vec3 targetCurrentPos = target.GetEyeGlobalPosition();

            Vec3 targetVelocity = Vec3.Zero;
            if (SkillSystemBehavior.TrackedAgents.TryGetValue(target, out var data))
                targetVelocity = data.Velocity;

            float horizontalDistance = (targetCurrentPos.AsVec2 - shooterPos.AsVec2).Length;
            float timeToTarget = horizontalDistance / projectileSpeed;

            float previousTime = 0;
            float currentTime = timeToTarget;
            int iterations = 0;

            while (TaleWorlds.Library.MathF.Abs(previousTime - currentTime) > ConvergenceThreshold && iterations < MaxIterations)
            {
                previousTime = currentTime;
                Vec3 predictedTargetPos = targetCurrentPos + (targetVelocity * previousTime);

                Vec3 shotDir = TrajectoryPhysics.CalculateFiringSolution(
                    shooterPos, predictedTargetPos, projectileSpeed, Gravity);

                if (shotDir != Vec3.Invalid && shotDir != Vec3.Zero)
                {
                    // 抛物线有解：使用精确飞行时间
                    Vec3 velocityVector = shotDir * projectileSpeed;
                    currentTime = TrajectoryPhysics.CalculateFlightTime(
                        velocityVector, shooterPos.z, predictedTargetPos.z);
                }
                else
                {
                    // 无解：回退到水平直线估算
                    Vec3 simpleDelta = predictedTargetPos - shooterPos;
                    currentTime = simpleDelta.AsVec2.Length / projectileSpeed;
                }

                iterations++;
            }

            return targetCurrentPos + (targetVelocity * currentTime);
        }
    }
}
