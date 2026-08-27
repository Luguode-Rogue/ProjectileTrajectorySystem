// ProjectileTrajectorySettings.cs
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Localization;

namespace ProjectileTrajectorySystem
{
    public class ProjectileTrajectorySettings : AttributeGlobalSettings<ProjectileTrajectorySettings>
    {
        public override string Id => "ProjectileTrajectorySystem";

        // 原文：弹道轨迹系统
        public override string DisplayName => new TextObject("{=PTS_DisplayName}Projectile Trajectory System").ToString();
        public override string FolderName => "ProjectileTrajectorySystem";
        public override string FormatType => "xml";

        // 原文：启用弹道系统
        // 原文 Hint：总开关：是否启用弹道可视化和敌人高亮。
        [SettingPropertyBool(
            "{=PTS_001}Enable Trajectory System",
            RequireRestart = false,
            HintText = "{=PTS_H001}Master switch: enable trajectory visualization and enemy highlighting.",
            Order = 0)]
        public bool EnableTrajectory { get; set; } = true;

        // 原文 Group：弹道显示设置
        [SettingPropertyGroup("{=PTS_G001}Trajectory Display")]
        // 原文：玩家弹道
        // 原文 Hint：显示玩家远程武器的预测弹道。
        [SettingPropertyBool(
            "{=PTS_002}Player Trajectory",
            RequireRestart = false,
            HintText = "{=PTS_H002}Show predicted trajectory for player's ranged weapons.",
            Order = 1)]
        public bool PlayerTrajectory { get; set; } = true;

        [SettingPropertyGroup("{=PTS_G001}Trajectory Display")]
        // 原文：敌人弹道
        // 原文 Hint：当敌人瞄准玩家时，显示其预测的射击线。
        [SettingPropertyBool(
            "{=PTS_003}Enemy Trajectory",
            RequireRestart = false,
            HintText = "{=PTS_H003}Show predicted firing line when enemies aim at the player.",
            Order = 2)]
        public bool EnemyTrajectory { get; set; } = true;

        [SettingPropertyGroup("{=PTS_G001}Trajectory Display")]
        // 原文：实时投射物轨迹
        // 原文 Hint：显示已射出的箭矢、弩箭等投射物的实际飞行路径。
        [SettingPropertyBool(
            "{=PTS_004}In-flight Missile Trajectory",
            RequireRestart = false,
            HintText = "{=PTS_H004}Show actual flight path of fired arrows, bolts, and other projectiles.",
            Order = 3)]
        public bool MissileTrajectory { get; set; } = true;

        // ===== 移动目标预瞄 (Lead Prediction) =====
        [SettingPropertyGroup("{=PTS_G001}Trajectory Display")]
        [SettingPropertyBool(
            "{=PTS_015}Enable Lead Prediction",
            RequireRestart = false,
            HintText = "{=PTS_H015}Enable trajectory prediction for moving targets (lead targeting). EXPERIMENTAL FEATURE: Not recommended for general use.",
            Order = 12)]
        public bool EnableLeadPrediction { get; set; } = true;

        // 原文 Group：视觉效果
        [SettingPropertyGroup("{=PTS_G002}Visual Effects")]
        // 原文：敌人高亮
        // 原文 Hint：对正在瞄准玩家的敌方远程单位进行红色轮廓高亮。
        [SettingPropertyBool(
            "{=PTS_005}Enemy Highlight",
            RequireRestart = false,
            HintText = "{=PTS_H005}Highlight enemy ranged units aiming at the player with red outline.",
            Order = 4)]
        public bool EnemyHighlight { get; set; } = true;

        // 原文 Group：渲染方式
        [SettingPropertyGroup("{=PTS_G003}Rendering Method")]
        // 原文：使用实体图标显示 (GameEntity)
        // 原文 Hint：使用游戏物体图标（视觉效果差，游戏画面设置中开启DLSS时现实正常）。
        [SettingPropertyBool(
            "{=PTS_006}Use GameEntity Icons",
            RequireRestart = false,
            HintText = "{=PTS_H006}Use game object icons (poor visuals, but displays correctly when DLSS is enabled in video settings).",
            Order = 5)]
        public bool UseGameEntityDisplay { get; set; } = false;

        [SettingPropertyGroup("{=PTS_G003}Rendering Method")]
        // 原文：使用调试线段显示 (DebugLine)
        // 原文 Hint：使用简单的线段渲染（性能极高，但游戏画面设置中开启DLSS会导致显示异常）。
        [SettingPropertyBool(
            "{=PTS_007}Use DebugLine",
            RequireRestart = false,
            HintText = "{=PTS_H007}Use simple line rendering (extremely high performance, but enabling DLSS in video settings will cause display anomalies).",
            Order = 6)]
        public bool UseDebugLineDisplay { get; set; } = true;

        [SettingPropertyGroup("{=PTS_G003}Rendering Method")]
        // 原文：GE 显示 - 玩家远程
        // 原文 Hint：是否为玩家的普通远程武器启用实体图标显示。
        [SettingPropertyBool(
            "{=PTS_008}GE Display - Player Ranged",
            RequireRestart = false,
            HintText = "{=PTS_H008}Enable GameEntity icons for player's handheld ranged weapons.",
            Order = 7)]
        public bool UseGameEntityForPlayerRanged { get; set; } = true;

        [SettingPropertyGroup("{=PTS_G003}Rendering Method")]
        // 原文：GE 显示 - 玩家攻城武器
        // 原文 Hint：是否为玩家操作的攻城器械启用实体图标显示。
        [SettingPropertyBool(
            "{=PTS_009}GE Display - Player Siege",
            RequireRestart = false,
            HintText = "{=PTS_H009}Enable GameEntity icons for player-controlled siege engines.",
            Order = 8)]
        public bool UseGameEntityForPlayerSiege { get; set; } = true;

        // 原文 Group：性能设置
        [SettingPropertyGroup("{=PTS_G004}Performance")]
        // 原文：最大追踪敌人数量
        // 原文 Hint：同时计算并显示预测弹道的最大敌人数量。
        [SettingPropertyInteger(
            "{=PTS_010}Max Tracked Enemies",
            0, 50,
            HintText = "{=PTS_H010}Maximum number of enemies to calculate and display trajectories for simultaneously.",
            Order = 9)]
        public int MaxTrackedEnemiesLegacy { get; set; } = 10;

        [SettingPropertyGroup("{=PTS_G002}Visual Effects")]
        // 原文：启用抬头虚化
        // 原文 Hint：高角度仰射时虚化玩家模型，防止遮挡视野。
        [SettingPropertyBool(
            "{=PTS_011}Enable Look-up Blur",
            RequireRestart = false,
            HintText = "{=PTS_H011}Blur player model when aiming at high angles to avoid obstructing view.",
            Order = 10)]
        public bool EnableLookUpBlur { get; set; } = true;

        [SettingPropertyGroup("{=PTS_G002}Visual Effects")]
        // 原文：仅在准备攻击时显示
        // 原文 Hint：只在拉弓或瞄准阶段显示弹道，平时隐藏。
        [SettingPropertyBool(
            "{=PTS_012}Show Only When Ready",
            RequireRestart = false,
            HintText = "{=PTS_H012}Only show trajectory during draw or aim phase, hidden otherwise.",
            Order = 11)]
        public bool EnableAttackReadyRestriction { get; set; } = true;
        // ===== 近敌慢动作 =====
        [SettingPropertyGroup("{=PTS_G005}Slow Motion")]
        [SettingPropertyBool(
            "{=PTS_100}Enable Slow Motion",
            RequireRestart = false,
            HintText = "{=PTS_H100}Slow down time when aiming with enemies nearby.",
            Order = 20)]
        public bool EnableSlowMotion { get; set; } = false;

        [SettingPropertyGroup("{=PTS_G005}Slow Motion")]
        [SettingPropertyInteger(
            "{=PTS_101}Enemy Radius",
            1, 50,
            HintText = "{=PTS_H101}Radius to detect nearby enemies.",
            Order = 21)]
        public int SlowMoEnemyRadius { get; set; } = 10;

        [SettingPropertyGroup("{=PTS_G005}Slow Motion")]
        [SettingPropertyFloatingInteger(
            "{=PTS_102}Time Scale",
            0.05f, 1f,
            HintText = "{=PTS_H102}Slow motion multiplier.",
            Order = 22)]
        public float SlowMoTimeScale { get; set; } = 0.35f;

        // ===== 攻城器射击修正 =====
        [SettingPropertyGroup("{=PTS_G006}Siege Weapon Shoot")]
        [SettingPropertyBool(
            "{=PTS_031}Siege Weapon Shoot Fix",
            RequireRestart = false,
            HintText = "{=PTS_H031}Remove spread and fix speed for siege engines. Projectiles fire along precise direction at fixed speed.",
            Order = 30)]
        public bool SiegeWeaponShootFix { get; set; } = false;

        [SettingPropertyGroup("{=PTS_G006}Siege Weapon Shoot")]
        [SettingPropertyBool(
            "{=PTS_032}Smart Skip Scatter",
            RequireRestart = false,
            HintText = "{=PTS_H032}Skip fix for multi-projectile siege engines (scatter type). They keep native random behavior.",
            Order = 31)]
        public bool SiegeWeaponSmartSkip { get; set; } = false;

        [SettingPropertyGroup("{=PTS_G006}Siege Weapon Shoot")]
        [SettingPropertyBool(
            "{=PTS_033}Debug Text Output",
            RequireRestart = false,
            HintText = "{=PTS_H033}Display shooting parameters in the info panel when a siege engine fires.",
            Order = 32)]
        public bool SiegeWeaponDebugText { get; set; } = false;

        // 当 MCM GUI 中某项被修改时，MCM 会调用该覆盖方法
        public override void OnPropertyChanged(string propertyName)
        {
            base.OnPropertyChanged(propertyName);

            // 同步到 XML 后端并保存（使非 MCM 玩家也能使用修改）
            ProjectileTrajectorySettingsManager.SyncFromMCM(this);
        }

        // 可选：构造时从 XML 初始化 MCM UI 显示值（防止 UI 默认值与 XML 不一致）
        public ProjectileTrajectorySettings()
        {
            var data = ProjectileTrajectorySettingsManager.Settings;
            if (data != null)
            {
                EnableTrajectory = data.EnableTrajectory;
                PlayerTrajectory = data.PlayerTrajectory;
                EnemyTrajectory = data.EnemyTrajectory;
                MissileTrajectory = data.MissileTrajectory;
                EnemyHighlight = data.EnemyHighlight;
                UseGameEntityDisplay = data.UseGameEntityDisplay;
                UseDebugLineDisplay = data.UseDebugLineDisplay;
                UseGameEntityForPlayerRanged = data.UseGameEntityForPlayerRanged;
                UseGameEntityForPlayerSiege = data.UseGameEntityForPlayerSiege;
                MaxTrackedEnemiesLegacy = data.MaxTrackedEnemiesLegacy;
                EnableLookUpBlur = data.EnableLookUpBlur;
                EnableAttackReadyRestriction = data.EnableAttackReadyRestriction;
                SiegeWeaponShootFix = data.SiegeWeaponShootFix;
                SiegeWeaponSmartSkip = data.SiegeWeaponSmartSkip;
                SiegeWeaponDebugText = data.SiegeWeaponDebugText;
            }
        }
    }
}