// SubModule.cs
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace ProjectileTrajectorySystem
{
    public class SubModule : MBSubModuleBase
    {
        private static Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            // 先加载 XML 设置（或者生成默认 XML）
            ProjectileTrajectorySettingsManager.Load();

            // 初始化 Harmony 补丁
            _harmony = new Harmony("com.projectiletrajectory.siegedebug");
            _harmony.PatchAll();
        }

        protected override void OnSubModuleUnloaded()
        {
            _harmony?.UnpatchAll(_harmony.Id);
            base.OnSubModuleUnloaded();
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);

            // 添加自定义的 MissionBehavior 到当前任务
            mission.AddMissionBehavior(new SkillSystemBehavior());
            mission.AddMissionBehavior(new NavalDLCmBehavior());
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
        }
    }
}