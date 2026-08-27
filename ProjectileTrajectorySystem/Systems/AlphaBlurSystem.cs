// Alpha 虚化系统：高角度仰射时虚化玩家/攻城武器模型，防止遮挡视野
using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProjectileTrajectorySystem
{
    internal static class AlphaBlurSystem
    {
        public const float LookUpThreshold = 0.2f;
        public const float DefaultAlpha = 1.0f;
        public const float BlurAlpha = 0.03f;
        private const float AlphaEpsilon = 0.01f;
        private const float SmoothFactor = 0.15f;

        private static readonly Dictionary<int, WeakGameEntity> _entityById = new();
        private static readonly Dictionary<int, float> _currentAlphaById = new();
        private static readonly Dictionary<int, float> _targetAlphaById = new();
        private static readonly Dictionary<RangedSiegeWeapon, List<WeakGameEntity>> _siegeChildEntities = new();

        #region 注册与设置

        private static int RegisterEntity(WeakGameEntity ge)
        {
            if (ge == null) return 0;
            int id = ge.GetHashCode();
            if (!_entityById.ContainsKey(id))
            {
                _entityById[id] = ge;
                if (!_currentAlphaById.ContainsKey(id)) _currentAlphaById[id] = DefaultAlpha;
                if (!_targetAlphaById.ContainsKey(id)) _targetAlphaById[id] = DefaultAlpha;
            }
            return id;
        }

        public static void SetEntityTargetAlpha(WeakGameEntity ge, float targetAlpha)
        {
            if (ge == null) return;
            _targetAlphaById[RegisterEntity(ge)] = targetAlpha;
        }

        public static void SetAgentTargetAlpha(Agent agent, float targetAlpha)
        {
            if (agent == null) return;
            try
            {
                var av = agent.AgentVisuals;
                if (av == null) return;
                var ge = av.GetEntity().WeakEntity;
                if (ge != null) SetEntityTargetAlpha(ge, targetAlpha);
            }
            catch { }
        }

        public static void SetSiegeTargetAlpha(RangedSiegeWeapon siege, float targetAlpha)
        {
            if (siege == null) return;
            try
            {
                if (!_siegeChildEntities.ContainsKey(siege))
                    _siegeChildEntities[siege] = CollectAllChildren(siege);

                foreach (var entity in _siegeChildEntities[siege])
                {
                    try
                    {
                        if (entity != null && !WeakGameEntity.Invalid.Equals(entity))
                            entity.SetAlpha(targetAlpha);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static List<WeakGameEntity> CollectAllChildren(RangedSiegeWeapon siege)
        {
            var entities = new List<WeakGameEntity>();
            WeakGameEntity highest = GetHighestParent(siege.GameEntity);
            if (highest != null)
            {
                entities.Add(highest);
                CollectChildrenRecursive(highest, entities);
            }
            return entities;
        }

        private static void CollectChildrenRecursive(WeakGameEntity parent, List<WeakGameEntity> children)
        {
            if (parent == null || WeakGameEntity.Invalid.Equals(parent)) return;
            try
            {
                var ge = GameEntity.CreateFromWeakEntity(parent);
                if (ge == null) return;

                if (!children.Contains(parent))
                    children.Add(parent);

                foreach (var child in ge.GetChildren())
                {
                    if (ge != null)
                    {
                        var weak = child.WeakEntity;
                        if (!children.Contains(weak))
                        {
                            children.Add(weak);
                            CollectChildrenRecursive(weak, children);
                        }
                    }
                }
            }
            catch { }
        }

        private static WeakGameEntity GetHighestParent(WeakGameEntity entity)
        {
            if (entity == null || WeakGameEntity.Invalid.Equals(entity))
                return WeakGameEntity.Invalid;

            while (entity.Parent != null && !WeakGameEntity.Invalid.Equals(entity.Parent))
                entity = entity.Parent;

            return entity;
        }

        #endregion

        #region 平滑更新

        public static void UpdateSmoothing()
        {
            var ids = new List<int>(_targetAlphaById.Keys);
            foreach (int id in ids)
            {
                if (!_entityById.TryGetValue(id, out var ge) || ge == null)
                {
                    _targetAlphaById.Remove(id);
                    _currentAlphaById.Remove(id);
                    _entityById.Remove(id);
                    continue;
                }

                float target = _targetAlphaById.ContainsKey(id) ? _targetAlphaById[id] : DefaultAlpha;
                float current = _currentAlphaById.ContainsKey(id) ? _currentAlphaById[id] : DefaultAlpha;
                float next = current + (target - current) * SmoothFactor;

                if (Math.Abs(next - current) > AlphaEpsilon)
                {
                    try
                    {
                        if (!ge.IsGhostObject())
                            ge.SetAlpha(next);
                    }
                    catch { }
                    _currentAlphaById[id] = next;
                }
                else
                {
                    _currentAlphaById[id] = target;
                    if (Math.Abs(target - DefaultAlpha) <= AlphaEpsilon)
                    {
                        _targetAlphaById.Remove(id);
                        _currentAlphaById.Remove(id);
                        _entityById.Remove(id);
                    }
                }
            }
        }

        /// <summary>
        /// 还原并清空所有 Alpha 记录。
        /// </summary>
        /// <param name="restoreVisuals">
        /// 是否真正回写 Alpha。Mission 结束（OnEndMission）时必须传 false：
        /// 此时原生 GameEntity 已被引擎释放，回写会访问已释放内存触发
        /// AccessViolationException（无法被 try/catch 捕获）。
        /// </param>
        public static void RestoreAll(bool restoreVisuals = true)
        {
            if (restoreVisuals && MissionSafety.IsSceneAlive())
            {
                var ids = new List<int>(_entityById.Keys);
                foreach (var id in ids)
                {
                    if (_entityById.TryGetValue(id, out var ge)
                        && ge != null
                        && !WeakGameEntity.Invalid.Equals(ge))
                    {
                        try { ge.SetAlpha(DefaultAlpha); } catch { }
                    }
                }
            }

            _entityById.Clear();
            _currentAlphaById.Clear();
            _targetAlphaById.Clear();
            _siegeChildEntities.Clear();
        }

        #endregion

        #region 工具

        public static bool IsLookingUp(Vec3 lookDir)
            => lookDir.z > LookUpThreshold;

        #endregion
    }
}
