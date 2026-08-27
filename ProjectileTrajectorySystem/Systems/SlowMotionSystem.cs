// 慢动作系统：当玩家使用远程武器瞄准敌人时自动降低时间流速
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;


namespace ProjectileTrajectorySystem
{
    internal class SlowMotionSystem
    {
        private bool _slowMoActive;
        private float _currentTimeScale = 1f;

        public void Update()
        {
            var settings = ProjectileTrajectorySettingsManager.Settings;
            if (!settings.EnableSlowMotion)
            {
                RestoreTime();
                return;
            }

            Agent player = Agent.Main;
            if (player == null || !player.IsActive())
            {
                RestoreTime();
                return;
            }

            // 是否远程武器
            var weapon = player.WieldedWeapon;
            bool isRanged = !weapon.IsEmpty && weapon.CurrentUsageItem.IsRangedWeapon;
            if (!isRanged)
            {
                RestoreTime();
                return;
            }

            // 是否在准备攻击
            bool isAiming = player.GetCurrentActionStage(1) == Agent.ActionStage.AttackReady;
            if (!isAiming)
            {
                RestoreTime();
                return;
            }

            // 是否有敌人
            if (!HasNearbyEnemy(player, settings.SlowMoEnemyRadius))
            {
                RestoreTime();
                return;
            }

            // 触发慢动作
            ApplyTimeScale(settings.SlowMoTimeScale);
        }

        private bool HasNearbyEnemy(Agent player, float radius)
        {
            var enemies = new MBList<Agent>();
            Mission.Current.GetNearbyEnemyAgents(
                player.Position.AsVec2, radius, player.Team, enemies);
            return enemies.Count > 0;
        }

        private void ApplyTimeScale(float scale)
        {
            scale = TaleWorlds.Library.MathF.Clamp(scale, 0.05f, 1f);

            if (_slowMoActive && TaleWorlds.Library.MathF.Abs(_currentTimeScale - scale) < 0.001f)
                return;

            var mission = Mission.Current;
            if (mission != null)
                mission.AddTimeSpeedRequest(new Mission.TimeSpeedRequest(scale, 999));

            _slowMoActive = true;
            _currentTimeScale = scale;
        }

        public void RestoreTime()
        {
            if (!_slowMoActive) return;

            var mission = Mission.Current;
            if (mission != null)
                mission.RemoveTimeSpeedRequest(999);

            _slowMoActive = false;
            _currentTimeScale = 1f;
        }
    }
}
