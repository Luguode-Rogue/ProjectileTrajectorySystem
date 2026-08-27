using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using System;
using System.Collections;
using System.Reflection;

namespace ProjectileTrajectorySystem
{
    public class NavalDLCmBehavior : MissionLogic
    {
        public override void OnMissionTick(float dt)
        {
            if (ProjectileTrajectorySettingsManager.Settings.EnableTrajectory &&
                ProjectileTrajectorySettingsManager.Settings.PlayerTrajectory &&
                (ProjectileTrajectorySettingsManager.Settings.UseGameEntityDisplay ||
                 ProjectileTrajectorySettingsManager.Settings.UseDebugLineDisplay))
            {
                NavalDLCTrajectorySupport.UpdatePlayerShipTrajectory();
            }
        }
    }

    public static class NavalDLCTrajectorySupport
    {
        private static bool _initialized;
        private static bool _navalAvailable;

        private static Type _agentNavalComponentType;
        private static PropertyInfo _steppedShipProperty;
        private static PropertyInfo _shipSiegeWeaponProperty;
        private static FieldInfo _agentComponentsField;

        private static void Init()
        {
            if (_initialized)
                return;

            _initialized = true;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType("NavalDLC.Missions.AgentNavalComponent");
                if (type != null)
                {
                    _agentNavalComponentType = type;

                    var missionShipType =
                        asm.GetType("NavalDLC.Missions.Objects.MissionShip");

                    _steppedShipProperty =
                        _agentNavalComponentType.GetProperty("SteppedShip",
                            BindingFlags.Instance | BindingFlags.Public);

                    _shipSiegeWeaponProperty =
                        missionShipType?.GetProperty("ShipSiegeWeapon",
                            BindingFlags.Instance | BindingFlags.Public);

                    _agentComponentsField =
                        typeof(Agent).GetField("_components",
                            BindingFlags.Instance | BindingFlags.NonPublic);

                    _navalAvailable =
                        _steppedShipProperty != null &&
                        _shipSiegeWeaponProperty != null &&
                        _agentComponentsField != null;
                    return;
                }
            }

            _navalAvailable = false;
        }
        public static void UpdatePlayerShipTrajectory()
        {
            Init();

            if (!_navalAvailable)
                return;

            Agent player = Agent.Main;
            if (player == null || !player.IsActive())
                return;

            object navalComponent = GetNavalComponent(player);
            if (navalComponent == null)
                return;

            object missionShip = _steppedShipProperty.GetValue(navalComponent);
            if (missionShip == null)
                return;

            object siegeObj = _shipSiegeWeaponProperty.GetValue(missionShip);
            if (siegeObj == null)
                return;

            if (siegeObj is RangedSiegeWeapon siegeWeapon)
            {
                if (siegeObj != null)
                {
                    bool siegeLookingUp = false;
                    try
                    {
                        Vec3 siegeDir = SiegeWeaponHelper.GetShootingDirection(
                            (RangedSiegeWeapon)siegeObj);
                        siegeLookingUp = AlphaBlurSystem.IsLookingUp(siegeDir);
                    }
                    catch { siegeLookingUp = false; }

                    bool enableLookUpBlur = ProjectileTrajectorySettingsManager.Settings.EnableLookUpBlur;

                    // 海战 DLC：攻城器抬高时虚化模型以显示弹道
                    if (enableLookUpBlur)
                    {
                        AlphaBlurSystem.SetSiegeTargetAlpha(
                            (RangedSiegeWeapon)siegeObj,
                            siegeLookingUp ? AlphaBlurSystem.BlurAlpha : AlphaBlurSystem.DefaultAlpha);
                    }
                }
                ProjectileTrajectorySystem.UpdateTrajectory(player, siegeWeapon);
            }
        }

        private static object GetNavalComponent(Agent agent)
        {
            var list = _agentComponentsField.GetValue(agent) as IEnumerable;
            if (list == null)
                return null;

            foreach (var comp in list)
            {
                if (comp == null)
                    continue;

                if (comp.GetType() == _agentNavalComponentType)
                    return comp;
            }

            return null;
        }
    }
}