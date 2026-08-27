// ProjectileTrajectorySettingsData.cs
using System;
using System.Xml.Serialization;
using TaleWorlds.InputSystem;

namespace ProjectileTrajectorySystem
{
    [Serializable]
    [XmlRoot("ProjectileTrajectorySettings")]
    public class ProjectileTrajectorySettingsData
    {
        // 总开关
        public bool EnableTrajectory = true;

        // 弹道显示
        public bool PlayerTrajectory = true;
        public bool EnemyTrajectory = true;
        public bool MissileTrajectory = true;

        // 视觉增强
        public bool EnemyHighlight = true;

        // 显示方式
        public bool UseGameEntityDisplay = false;
        public bool UseDebugLineDisplay = true;

        public bool UseGameEntityForPlayerRanged = true;
        public bool UseGameEntityForPlayerSiege = true;

        // 敌人弹道
        public int MaxTrackedEnemiesLegacy = 10;

        // ===== 新增：抬头虚化和攻击准备阶段限制设置 =====
        public bool EnableLookUpBlur = true;
        public bool EnableAttackReadyRestriction = true;

        // ===== Slow Motion =====
        public bool EnableSlowMotion = false;
        public int SlowMoEnemyRadius = 10;
        public float SlowMoTimeScale = 0.35f;
        // ===== Lead Prediction =====
        public bool EnableLeadPrediction = true;

        // ===== Siege Shoot Debug =====
        public bool SiegeWeaponShootFix = false;     // 主开关：移除散布+固定速度
        public bool SiegeWeaponSmartSkip = false;    // 智能跳过多弹片（散弹类保留原生随机性）
        public bool SiegeWeaponDebugText = false;    // 信息栏输出射击参数

        // ===== Siege Landing View（落点视角 + 红边高亮目标标记）=====
        public bool EnableSiegeLandingView = true;            // 开关：攻城武器落点视角
        public InputKey SiegeLandingViewKey = InputKey.V;     // 触发按键（默认 V）
    }
}