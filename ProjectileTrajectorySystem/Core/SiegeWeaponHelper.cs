// 攻城武器反射接口：通过反射访问引擎私有成员
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProjectileTrajectorySystem
{
    internal static class SiegeWeaponHelper
    {
        /// <summary>
        /// 攻城武器自身的根实体（用于射线检测时忽略自身，对齐引擎 RayCastForClosestEntityOrTerrainIgnoreEntity 的 ignoredEntity 参数）。
        /// </summary>
        /// <summary>
        /// 攻城武器自身的根实体（WeakGameEntity），用于射线检测时忽略自身，
        /// 对齐引擎 RayCastForClosestEntityOrTerrainIgnoreEntity 的 ignoredEntity 参数（类型即 WeakGameEntity）。
        /// </summary>
        public static WeakGameEntity GetRootEntity(RangedSiegeWeapon weapon)
        {
            if (weapon == null)
                return default;
            try
            {
                // RangedSiegeWeapon.GameEntity 返回 WeakGameEntity（struct，不能用 ?.）
                GameEntity root = GameEntity.CreateFromWeakEntity(weapon.GameEntity).Root;
                return root.WeakEntity;
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// 炮弹碰撞半径（引擎用 _projectileRadiusCached 作为射线厚度，
        /// 见 RangedSiegeWeapon.RayCastForClosestEntityOrTerrainIgnoreEntity 调用）。
        /// 反射读取 protected 字段；失败回退到一个合理的默认厚度。
        /// </summary>
        private static readonly FieldInfo _projectileRadiusField =
            typeof(RangedSiegeWeapon).GetField("_projectileRadiusCached",
                BindingFlags.Instance | BindingFlags.NonPublic);

        public static float GetProjectileRadius(RangedSiegeWeapon weapon)
        {
            if (weapon == null || _projectileRadiusField == null)
                return 0.3f;
            try
            {
                object v = _projectileRadiusField.GetValue(weapon);
                if (v is float f && f > 0f) return f;
            }
            catch { }
            return 0.3f;
        }

        public static float GetShootingSpeed(RangedSiegeWeapon weapon)
        {
            return (float)weapon.GetType()
                .GetProperty("ShootingSpeed", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(weapon);
        }

        public static Vec3 GetShootingDirection(RangedSiegeWeapon weapon)
        {
            return (Vec3)weapon.GetType()
                .GetProperty("ShootingDirection", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(weapon);
        }

        public static Vec3 GetProjectileStartPosition(RangedSiegeWeapon weapon)
        {
            if (weapon == null) return Vec3.Invalid;

            if (weapon is Ballista ballistaInstance)
                return ballistaInstance.ProjectileEntityCurrentGlobalPosition;

            // Mangonel, Trebuchet 等：通过 "clean" -> "projectile_leaving_position" 节点查找
            WeakGameEntity weaponEntity = weapon.GameEntity;
            if (weaponEntity == null) return Vec3.Invalid;

            WeakGameEntity clean = WeakGameEntity.Invalid;
            var children = weaponEntity.GetChildren();
            if (children != null)
                clean = children.FirstOrDefault(x => x != null && x.Name == "clean");

            if (clean == null) return Vec3.Invalid;

            GameEntity launcher = GameEntity.CreateFromWeakEntity(clean);
            if (launcher == null) return Vec3.Invalid;

            var launcherChildren = launcher.GetChildren();
            if (launcherChildren == null) return Vec3.Invalid;

            var projectileNode = launcherChildren.FirstOrDefault(
                x => x != null && (x.Name == "projectile_leaving_position" || x.Name == "use_pos"));
            if (projectileNode == null) return Vec3.Invalid;

            return projectileNode.GlobalPosition;
        }

        public static float GetAirFriction(Agent agent, out EquipmentIndex _)
        {
            _ = EquipmentIndex.None;

            if (!agent.IsHuman) return 0f;

            var weapon = agent.WieldedWeapon;
            if (weapon.IsEmpty) return 0f;

            var item = weapon.Item;
            if (item == null || item.WeaponComponent == null) return 0f;

            var primaryWeapon = item.WeaponComponent.PrimaryWeapon;
            if (primaryWeapon == null) return 0f;

            return ItemObject.GetAirFrictionConstant(primaryWeapon.WeaponClass, primaryWeapon.WeaponFlags);
        }

        /// <summary>
        /// 获取攻城器的空气阻力常数。
        /// 通过反射读取 RangedSiegeWeapon.OriginalMissileItem（protected 字段），
        /// 然后调用引擎的 ItemObject.GetAirFrictionConstant 获取。
        /// 若反射失败或弹丸无 WeaponComponent，返回 0。
        /// </summary>
        private static readonly FieldInfo _originalMissileItemField =
            typeof(RangedSiegeWeapon).GetField("OriginalMissileItem",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        public static float GetSiegeAirFriction(RangedSiegeWeapon weapon)
        {
            if (weapon == null || _originalMissileItemField == null)
                return 0f;

            var missileItem = _originalMissileItemField.GetValue(weapon) as ItemObject;
            if (missileItem == null || missileItem.WeaponComponent == null)
                return 0f;

            var primaryWeapon = missileItem.WeaponComponent.PrimaryWeapon;
            if (primaryWeapon == null)
                return 0f;

            return ItemObject.GetAirFrictionConstant(
                primaryWeapon.WeaponClass, primaryWeapon.WeaponFlags);
        }
    }
}
