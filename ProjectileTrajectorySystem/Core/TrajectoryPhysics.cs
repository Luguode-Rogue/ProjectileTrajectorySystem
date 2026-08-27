// 弹道物理计算：欧拉积分模拟、抛物线解算
using System;
using TaleWorlds.Library;

namespace ProjectileTrajectorySystem
{
    internal static class TrajectoryPhysics
    {
        public const float Gravity = 9.806f; // 与引擎 MBGlobals.Gravity 一致
        public const float IntegrationDt = 0.01f;

        /// <summary>欧拉积分法计算弹道位置</summary>
        public static Vec3 CalculatePosition(
            Vec3 origin, Vec3 direction, float speed, float time,
            float airFriction, float airFrictionOverride = -1f)
        {
            Vec3 velocity = direction * speed;
            Vec3 position = origin;
            float frictionToUse = airFrictionOverride >= 0 ? airFrictionOverride : airFriction;

            for (float t = 0; t < time; t += IntegrationDt)
            {
                float v = velocity.Length;
                if (v > 0.001f)
                    velocity -= velocity.NormalizedCopy() * (frictionToUse * v * v * IntegrationDt);
                velocity.z -= Gravity * IntegrationDt;
                position += velocity * IntegrationDt;
            }

            return position;
        }

        /// <summary>抛物线发射解算：给定起点、终点、初速和重力，求发射速度矢量</summary>
        /// <returns>速度矢量（非方向），无解时返回 Vec3.Invalid</returns>
        public static Vec3 CalculateFiringSolution(Vec3 start, Vec3 end, float speed, float gravity)
        {
            Vec2 horizontalDistance = new Vec2(end.x - start.x, end.y - start.y);
            float horizontalRange = horizontalDistance.Length;
            float verticalDistance = end.z - start.z;

            float speedSquared = speed * speed;
            float sqrtTerm = speedSquared * speedSquared
                - gravity * (gravity * horizontalRange * horizontalRange + 2 * verticalDistance * speedSquared);

            if (sqrtTerm < 0.0f)
                return Vec3.Invalid;

            float sqrtValue = (float)Math.Sqrt(sqrtTerm);
            float angle = (float)Math.Atan2(speedSquared - sqrtValue, gravity * horizontalRange);

            Vec3 firingSolution = new Vec3(horizontalDistance.x, horizontalDistance.y, 0);
            firingSolution.Normalize();
            firingSolution *= (float)Math.Cos(angle) * speed;
            firingSolution.z = (float)Math.Sin(angle) * speed;

            return firingSolution;
        }

        /// <summary>计算抛物线飞行时间</summary>
        public static float CalculateFlightTime(Vec3 velocityVector, float shooterZ, float targetZ)
        {
            return (velocityVector.z + TaleWorlds.Library.MathF.Sqrt(
                velocityVector.z * velocityVector.z + 2 * Gravity * (shooterZ - targetZ))) / Gravity;
        }
    }
}
