// 玩家弹道系统：远程武器 + 攻城武器弹道更新
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Debug = TaleWorlds.Library.Debug;

namespace ProjectileTrajectorySystem
{
    internal static class PlayerTrajectorySystem
    {
        private static bool _hasCollided;
        private static float _airFriction;

        private static readonly uint[] _debugLineColors =
        {
            0xFF00FFFFu, 0xFF0000FFu, 0xFF00FF00u, 0xFFFF0000u,
            0xFF8000FFu, 0xFFFFFF00u, 0xFFFF00FFu, 0xFF00FFFFu,
            0xFFFFA500u, 0xFF808080u, 0xFF000080u, 0xFF008000u,
            0xFF800000u, 0xFF800080u, 0xFF008080u, 0xFF808000u
        };

        public static void Update(Agent player)
        {
            if (player == null) return;

            var settings = ProjectileTrajectorySettingsManager.Settings;
            MissionWeapon weapon = player.WieldedWeapon;
            bool hasValidWeapon = !weapon.IsEmpty;
            bool isUsingRangedWeapon = hasValidWeapon && weapon.CurrentUsageItem.IsRangedWeapon;
            bool enableRestriction = settings.EnableAttackReadyRestriction;
            bool isAttacking = player.GetCurrentActionStage(1) == Agent.ActionStage.AttackReady;
            bool shouldUpdateTrajectory = enableRestriction
                ? (isUsingRangedWeapon && isAttacking)
                : isUsingRangedWeapon;

            // 抬头虚化 - 远程武器
            bool enableBlur = settings.EnableLookUpBlur;
            if (enableBlur)
            {
                bool lookingUp = AlphaBlurSystem.IsLookingUp(player.LookDirection) && isUsingRangedWeapon;
                AlphaBlurSystem.SetAgentTargetAlpha(player, lookingUp ? AlphaBlurSystem.BlurAlpha : AlphaBlurSystem.DefaultAlpha);
            }

            // 远程武器弹道
            if (shouldUpdateTrajectory)
            {
                try { UpdateRangeWeapon(player); } catch { }
            }

            // 攻城武器弹道
            if (!isUsingRangedWeapon)
            {
                try { UpdateSiegeWeapon(player, enableBlur); } catch { }
            }
        }

        private static void UpdateRangeWeapon(Agent agent)
        {
            var settings = ProjectileTrajectorySettingsManager.Settings;
            EquipmentIndex weaponIndex = agent.GetPrimaryWieldedItemIndex();

            bool shouldShowGE = settings.UseGameEntityDisplay
                && settings.UseGameEntityForPlayerRanged;
            bool shouldShowDL = settings.UseDebugLineDisplay;

            if (weaponIndex == EquipmentIndex.None || !shouldShowGE && !shouldShowDL)
            {
                if (shouldShowGE && SkillSystemBehavior.WoW_Line.Count > 0)
                    ProjectileTrajectorySystem.ClearNormalTrajectory();
                return;
            }

            MissionWeapon weapon = agent.Equipment[weaponIndex];
            if (weapon.IsEmpty || !weapon.CurrentUsageItem.IsRangedWeapon)
            {
                if (shouldShowGE && SkillSystemBehavior.WoW_Line.Count > 0)
                    ProjectileTrajectorySystem.ClearNormalTrajectory();
                return;
            }

            ResetFrameState();

            float speed = GetCachedOrLiveSpeed(agent, weapon);
            _airFriction = SiegeWeaponHelper.GetAirFriction(agent, out _);

            Vec3 startPos = agent.GetEyeGlobalPosition();
            Vec3 direction = agent.LookDirection;

            var activeKeys = new HashSet<float>();
            SimulateAndRender(startPos, direction, speed, 0f, 3.0f, 0.15f, 0f,
                customLine: null, baseColor: 0xFF00FFFFu, activeKeys: activeKeys);

            // 裁剪多余实体（timeKey 不再使用了的归还到池）
            if (shouldShowGE)
                TrajectoryRenderer.PruneUnusedEntities(SkillSystemBehavior.WoW_Line, activeKeys);
        }

        private static void UpdateSiegeWeapon(Agent player, bool enableBlur)
        {
            WeakGameEntity wge = player.CurrentlyUsedGameObject?.GameEntity
                ?? player.GetSteppedEntity();
            RangedSiegeWeapon siege = null;

            while (wge != null && !wge.HasScriptOfType<RangedSiegeWeapon>())
                wge = wge.Parent;

            if (wge != null)
                siege = wge.GetFirstScriptOfType<RangedSiegeWeapon>();

            if (siege == null)
            {
                // 玩家未操作工程器：通知落点系统，使其自动退出新模式（若正处于激活态）
                SkillSystemBehavior.SiegeLanding?.NotifyPlayerSiegeState(false);
                return;
            }

            // 玩家正在操作工程器
            SkillSystemBehavior.SiegeLanding?.NotifyPlayerSiegeState(true);

            // 无论是否开启弹道显示，只要玩家在操作攻城武器，就回填预期落点给落点视角系统。
            // （落点视角是独立功能，不能依赖弹道渲染开关）
            SkillSystemBehavior.SiegeLanding?.SetSiegeType(SiegeClassName(siege));

            Vec3 lpStart = SiegeWeaponHelper.GetProjectileStartPosition(siege);
            Vec3 lpDir = SiegeWeaponHelper.GetShootingDirection(siege);
            float lpSpeed = SiegeWeaponHelper.GetShootingSpeed(siege);
            float lpAir = SiegeWeaponHelper.GetSiegeAirFriction(siege);

            Vec3 siegeLanding = ComputeSiegeLandingPoint(siege, lpStart, lpDir, lpSpeed, lpAir);
            SkillSystemBehavior.SiegeLanding?.SetLandingPoint(siegeLanding);

            var settings = ProjectileTrajectorySettingsManager.Settings;
            bool shouldShowGE = settings.UseGameEntityDisplay
                && settings.UseGameEntityForPlayerSiege;
            bool shouldShowDL = settings.UseDebugLineDisplay;

            if (!shouldShowGE && !shouldShowDL) return;

            ResetFrameState();

            // 抬头虚化 - 攻城武器
            if (enableBlur)
            {
                try
                {
                    Vec3 siegeDir = SiegeWeaponHelper.GetShootingDirection(siege);
                    bool lookingUp = AlphaBlurSystem.IsLookingUp(siegeDir);
                    AlphaBlurSystem.SetSiegeTargetAlpha(siege,
                        lookingUp ? AlphaBlurSystem.BlurAlpha : AlphaBlurSystem.DefaultAlpha);
                }
                catch { }
            }

            float speed = SiegeWeaponHelper.GetShootingSpeed(siege);
            Vec3 direction = SiegeWeaponHelper.GetShootingDirection(siege);
            Vec3 startPos = SiegeWeaponHelper.GetProjectileStartPosition(siege);
            _airFriction = SiegeWeaponHelper.GetSiegeAirFriction(siege);

            var activeKeys = new HashSet<float>();
            SimulateAndRender(startPos, direction, speed, 0.3f, 4.8f, 0.15f, 0f,
                customLine: null, baseColor: 0xFF00FFFFu, activeKeys: activeKeys);

            // 裁剪多余实体
            if (shouldShowGE)
                TrajectoryRenderer.PruneUnusedEntities(SkillSystemBehavior.WoW_Line, activeKeys);

            // 注意：落点计算与回填已上移到方法开头（不受弹道显示开关影响）。
        }

        /// <summary>
        /// 计算攻城武器的预期落点。
        /// 关键原则：落点计算必须与弹道显示（SimulateAndRender）使用完全相同的一套弹道积分参数，
        /// 因为你已确认"弹道显示与实际落点重合"。因此这里直接复用 TrajectoryPhysics.CalculatePosition
        /// 的积分逻辑（start / dir / speed / airFriction 与 SimulateAndRender 内部一致），
        /// 沿弹道曲线逐点推进，当弹道点高度低于该 (x,y) 处的地形高度时判定落地。
        ///
        /// 之所以改用"地形高度判定"而非 Scene.RayCastForClosestEntityOrTerrainIgnoreEntity：
        /// 在攻城武器场景下那套射线长期全程 miss（日志实证飞到 z=913 仍不命中），
        /// 导致落点算不出来（返回 Invalid/NaN）。而地形高度由 Scene.GetTerrainHeight 直接求得，
        /// 与弹道曲线配合即可精确得到弹道首次触地位置，必然与显示重合。
        /// </summary>
        private static Vec3 ComputeSiegeLandingPoint(RangedSiegeWeapon weapon, Vec3 startPos, Vec3 direction, float speed, float airFriction)
        {
            // 方向无效（零向量 / NaN）直接放弃，避免产生 NaN 落点。
            if (IsNonFinite(direction.x) || IsNonFinite(direction.y) || IsNonFinite(direction.z)
                || direction.Length < 1e-4f)
            {
                return Vec3.Invalid;
            }
            if (IsNonFinite(startPos.x) || IsNonFinite(startPos.y) || IsNonFinite(startPos.z))
            {
                return Vec3.Invalid;
            }

            Vec3 dir = direction.NormalizedCopy();

            // 与 SimulateAndRender 完全一致的积分区间与步长（timeStart=0.3, timeEnd=4.8, timeStep=0.15）。
            const float timeStart = 0.3f;
            const float timeEnd = 4.8f;
            const float timeStep = 0.15f;
            const float groundOffset = 0.3f; // 弹道点低于地形高度 + 该容差即判定触地

            Scene scene = Mission.Current.Scene;
            if (scene == null)
            {
                return Vec3.Invalid;
            }

            Vec3 previousPos = startPos;
            for (float t = timeStart; t <= timeEnd; t += timeStep)
            {
                Vec3 currentPos = TrajectoryPhysics.CalculatePosition(startPos, dir, speed, t, airFriction);
                if (IsNonFinite(currentPos.x) || IsNonFinite(currentPos.y) || IsNonFinite(currentPos.z))
                {
                    return Vec3.Invalid;
                }

                // 用引擎地形高度接口直接取 (currentPos.x, currentPos.y) 处的地面 z。
                Vec2 groundSample = new Vec2(currentPos.x, currentPos.y);
                float groundZ = scene.GetTerrainHeight(groundSample, true);

                if (currentPos.z <= groundZ + groundOffset)
                {
                    // 落地点：将 x,y 处的 z 吸附到地面，保证与显示/实际落点重合。
                    Vec3 landing = new Vec3(currentPos.x, currentPos.y, groundZ + groundOffset);
                    return landing;
                }

                previousPos = currentPos;
            }

            return Vec3.Invalid;
        }

        /// <summary>
        /// 获取某 (x,y) 处的地形高度（真实地面 z）。
        /// 严格使用官方做法：构造 WorldPosition 后调用 GetGroundVec3()，取返回值 z。
        /// 注意 GetGroundZ() 在 WorldPosition 无效(State&lt;Valid)时返回 NaN，
        /// <summary>net472 无 float.IsFinite，用 IsNaN/IsInfinity 组合判断是否为非有限值（NaN 或 ±∞）。</summary>
        private static bool IsNonFinite(float v) => float.IsNaN(v) || float.IsInfinity(v);

        /// <summary>识别攻城武器大类：投石车 Mangonel / 投石器 Trebuchet / 其他。</summary>
        private static string SiegeClassName(RangedSiegeWeapon siege)
        {
            if (siege == null) return "Unknown";
            string name = siege.GetType().Name;
            if (name.Contains("Mangonel")) return "Mangonel";
            if (name.Contains("Trebuchet")) return "Trebuchet";
            if (name.Contains("Catapult")) return "Mangonel"; // 投石车类
            // 兜底：通过预制体名称判断
            try
            {
                string prefab = siege.GameEntity.IsValid ? siege.GameEntity.Name : string.Empty;
                if (prefab.Contains("mangonel") || prefab.Contains("catapult")) return "Mangonel";
                if (prefab.Contains("trebuchet")) return "Trebuchet";
            }
            catch { }
            return name;
        }

        private static float GetCachedOrLiveSpeed(Agent agent, MissionWeapon weapon)
        {
            var ammoItem = weapon.AmmoWeapon;
            string weaponId = weapon.Item?.Name?.ToString() ?? string.Empty;
            string ammoId = ammoItem.Item?.Name?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(ammoId)) ammoId = weaponId;

            var key = (agent.Index, weaponId, ammoId);

            if (SkillSystemBehavior.ProjectileSpeedCache.TryGetValue(key, out float cached))
                return cached;

            return weapon.GetModifiedMissileSpeedForCurrentUsage()
                * agent.AgentDrivenProperties.MissileSpeedMultiplier;
        }

        public static void SimulateAndRender(
            Vec3 startPos, Vec3 direction, float speed,
            float timeStart, float timeEnd, float timeStep, float timeKeyOffset,
            Dictionary<float, GameEntity> customLine, uint baseColor,
            HashSet<float> activeKeys = null)
        {
            var settings = ProjectileTrajectorySettingsManager.Settings;
            if (!settings.UseGameEntityDisplay && !settings.UseDebugLineDisplay)
                return;

            direction.Normalize();
            Vec3 previousPos = startPos;
            int colorIndex = 0;

            for (float t = timeStart; t <= timeEnd; t += timeStep)
            {
                if (_hasCollided) break;

                Vec3 currentPos = TrajectoryPhysics.CalculatePosition(
                    startPos, direction, speed, t, _airFriction);

                if (settings.UseGameEntityDisplay)
                {
                    float key = t + timeKeyOffset;
                    activeKeys?.Add(key);
                    TrajectoryRenderer.PlaceGameEntityMarker(
                        currentPos, key, customLine, baseColor);
                }

                if (settings.UseDebugLineDisplay)
                {
                    uint segColor = _debugLineColors[colorIndex % _debugLineColors.Length];
                    TrajectoryRenderer.DrawDebugLineSegment(
                        previousPos, currentPos - previousPos, segColor, 0f);
                    colorIndex++;
                }

                previousPos = currentPos;
            }
        }

        public static void ResetFrameState()
        {
            _hasCollided = false;
        }
    }
}
