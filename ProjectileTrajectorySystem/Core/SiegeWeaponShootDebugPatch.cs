// 攻城器射击修正：Harmony补丁拦截SetupProjectileToShoot，消除随机散布和速度随机化
// 两个独立开关（MCM勾选框）：
//   1. ShootFix      - 主开关：移除散布+固定速度
//   2. SmartSkip     - 智能跳过多弹片攻城器（散弹类保留原生随机性）
using HarmonyLib;
using System.Reflection;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProjectileTrajectorySystem
{
    /// <summary>
    /// 拦截 RangedSiegeWeapon.SetupProjectileToShoot 方法。
    /// 根据 MCM 三个独立开关决定行为：
    /// - ShootFix 关闭 → 不干预，原方法正常执行
    /// - ShootFix 开启 + SmartSkip 开启 + 散弹 → 跳过，保留原生随机
    /// - ShootFix 开启 → 消除散布，固定速度
    /// - DebugText 开启 → 额外输出射击参数到信息栏
    /// SmartSkip 利用参数 randomizeMissileSpeed 自动区分单发/散弹，无需手动配置武器列表。
    /// </summary>
    [HarmonyPatch(typeof(RangedSiegeWeapon), "SetupProjectileToShoot")]
    internal static class SiegeWeaponShootDebugPatch
    {
        // 缓存 GetGlobalVelocity 反射方法，避免每次射击都查找
        private static readonly MethodInfo _getGlobalVelocityMethod =
            AccessTools.Method(typeof(RangedSiegeWeapon), "GetGlobalVelocity");

        /// <summary>
        /// Harmony Prefix 补丁。
        /// 返回 false 跳过原方法（接管射击参数），返回 true 让原方法正常执行。
        /// </summary>
        static bool Prefix(RangedSiegeWeapon __instance, bool randomizeMissileSpeed,
            ref Vec3 direction, ref Mat3 orientation,
            ref float missileBaseSpeed, ref float missileShootingSpeed)
        {
            var settings = ProjectileTrajectorySettings.Instance;
            if (settings == null)
                return true;

            // 主开关关闭 → 不干预
            if (!settings.SiegeWeaponShootFix)
                return true;

            // 智能跳过：散弹类攻城器保留原生随机性
            if (settings.SiegeWeaponSmartSkip && randomizeMissileSpeed)
                return true;

            // 计算无随机的射击参数
            Vec3 shootingDir = SiegeWeaponHelper.GetShootingDirection(__instance);
            float shootingSpd = SiegeWeaponHelper.GetShootingSpeed(__instance);

            orientation = Mat3.Identity;
            orientation.f = shootingDir;
            orientation.Orthonormalize();

            Vec3 globalVel = Vec3.Zero;
            if (_getGlobalVelocityMethod != null)
                globalVel = (Vec3)_getGlobalVelocityMethod.Invoke(__instance, null);

            direction = shootingSpd * orientation.f + globalVel;
            missileShootingSpeed = direction.Normalize();
            missileBaseSpeed = shootingSpd;

            return false; // 跳过原方法
        }
    }
}
