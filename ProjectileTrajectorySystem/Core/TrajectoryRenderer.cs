// 弹道渲染：GameEntity 图标 + DebugLine 双管线
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProjectileTrajectorySystem
{
    internal static class TrajectoryRenderer
    {
        #region 预缓存模板

        /// <summary>共享模板实体：只 Instantiate 一次，后续 AddAllMeshesOfGameEntity 从此拷贝网格</summary>
        private static GameEntity _markerTemplate;

        /// <summary>模板名称（需与 Instantiate 参数一致）</summary>
        private const string TemplatePrefab = "mangonel_mapicon_projectile";

        public static void Initialize(Mission mission)
        {
            if (_markerTemplate != null) return;
            _markerTemplate = GameEntity.Instantiate(mission.Scene, TemplatePrefab, true);
        }

        public static void Shutdown()
        {
            _markerTemplate = null;  // 由场景销毁负责清理
            _lastRaycastFrame = 0;
            _raycastFrameCounter = 0;
        }

        #endregion

        #region GameEntity 渲染

        /// <summary>射线检测跳帧计数器</summary>
        private static int _raycastFrameCounter;
        private static int _lastRaycastFrame;

        /// <summary>每多少帧做一次射线检测（=5 即每5帧1次，减少80%开销）</summary>
        private const int RaycastFrameSkip = 5;

        public static void PlaceGameEntityMarker(
            Vec3 pos, float timeKey,
            Dictionary<float, GameEntity> line, uint color)
        {
            line ??= SkillSystemBehavior.WoW_Line;

            if (!line.TryGetValue(timeKey, out var entity))
            {
                entity = GameEntityPool.GetOrCreate();
                entity.AddAllMeshesOfGameEntity(_markerTemplate);
                PerformanceDebugger.OnAddAllMeshes();
                line[timeKey] = entity;
            }

            entity.SetLocalPosition(pos);
            PerformanceDebugger.OnSetLocalPosition();

            // 遮挡检测（降频：每 RaycastFrameSkip 帧才真正做射线）
            Agent player = Agent.Main;
            if (player == null || !player.IsActive()) return;

            _raycastFrameCounter++;
            bool shouldRaycast = (_raycastFrameCounter - _lastRaycastFrame) >= RaycastFrameSkip;

            if (shouldRaycast)
            {
                _lastRaycastFrame = _raycastFrameCounter;
                Vec3 playerEyePos = player.GetEyeGlobalPosition();
                float distanceToEntity = (pos - playerEyePos).Length;

                PerformanceDebugger.OnRayCast();
                bool isOccluded = Mission.Current.Scene.RayCastForClosestEntityOrTerrain(
                    playerEyePos, pos, out float hitDistance, out _, out _, 0.1f);

                bool isVisible = !(isOccluded && hitDistance < distanceToEntity);
                uint effectiveColor = isVisible ? color : 0x00000000u;

                if (!GameEntityPool.ColorCache.TryGetValue(entity, out var cachedColor)
                    || cachedColor != effectiveColor)
                {
                    entity.SetContourColor(isVisible ? color : null, true);
                    PerformanceDebugger.OnSetContourColor();
                    GameEntityPool.ColorCache[entity] = effectiveColor;
                }
            }
        }

        /// <summary>裁剪字典中不在 activeKeys 集合中的实体（归还到池）</summary>
        public static void PruneUnusedEntities(
            Dictionary<float, GameEntity> line, HashSet<float> activeKeys)
        {
            if (line == null || activeKeys == null) return;

            var toRemove = new List<float>();
            foreach (var key in line.Keys)
            {
                if (!activeKeys.Contains(key))
                    toRemove.Add(key);
            }
            foreach (var key in toRemove)
            {
                if (line.TryGetValue(key, out var e))
                {
                    GameEntityPool.Return(e);
                    line.Remove(key);
                }
            }
        }

        public static void ClearTrajectoryAfterCollision(float collisionKey)
        {
            foreach (var kv in SkillSystemBehavior.WoW_Line.ToList())
            {
                if (kv.Key > collisionKey && kv.Key < 100f)
                {
                    GameEntityPool.Return(kv.Value);
                    SkillSystemBehavior.WoW_Line.Remove(kv.Key);
                }
            }
        }

        public static void GenerateImpactCircle(Vec3 center)
        {
            const int count = 8;
            const float radius = 2f;

            for (int i = 0; i < count; i++)
            {
                float key = 100 + i;
                float angle = TaleWorlds.Library.MathF.PI * 2f * i / count;
                Vec3 pos = new Vec3(
                    center.x + TaleWorlds.Library.MathF.Cos(angle) * radius,
                    center.y + TaleWorlds.Library.MathF.Sin(angle) * radius,
                    center.z);

                PlaceGameEntityMarker(pos, key, SkillSystemBehavior.WoW_Line, 0xFF00FFFFu);
            }
        }

        #endregion

        #region DebugLine 渲染

        public static void DrawDebugLineSegment(Vec3 position, Vec3 direction, uint color, float time)
        {
            RenderDebugLine(position, direction, color, true, time);
        }

        public static void RenderDebugLine(
            Vec3 position, Vec3 direction, uint color, bool depthCheck, float time)
        {
            try
            {
                Type t = Type.GetType("TaleWorlds.Engine.EngineApplicationInterface, TaleWorlds.Engine");
                object debug = t?.GetField("IDebug",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null);
                debug?.GetType().GetMethod("RenderDebugLine")
                    ?.Invoke(debug, new object[] { position, direction, color, depthCheck, time });
            }
            catch { }
        }

        public static void DrawDebugLineTrajectory(List<Vec3> points, uint color)
        {
            if (points == null || points.Count < 2) return;

            for (int i = 1; i < points.Count; i++)
            {
                Vec3 prev = points[i - 1];
                Vec3 curr = points[i];
                RenderDebugLine(prev, curr - prev, color, true, 0f);
            }
        }

        #endregion

        #region 碰撞检测

        /// <returns>是否碰撞</returns>
        public static bool TryDetectCollision(Vec3 pos, float timeKey)
        {
            if (Mission.Current.Scene.RayCastForClosestEntityOrTerrain(
                    pos, pos + Vec3.Up * 5000f,
                    out float dist, out Vec3 hitPoint, out _, 1f) && dist < 5f)
            {
                if (ProjectileTrajectorySettingsManager.Settings.UseGameEntityDisplay)
                {
                    GenerateImpactCircle(hitPoint);
                    ClearTrajectoryAfterCollision(timeKey);
                }
                return true;
            }
            return false;
        }

        #endregion
    }
}
