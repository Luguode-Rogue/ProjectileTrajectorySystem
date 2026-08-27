// GameEntity 对象池：避免频繁创建/销毁实体
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProjectileTrajectorySystem
{
    internal static class GameEntityPool
    {
        private static readonly Stack<GameEntity> _pool = new();
        public static readonly Dictionary<GameEntity, uint> ColorCache = new();

        public static GameEntity GetOrCreate()
        {
            if (_pool.Count > 0)
            {
                var entity = _pool.Pop();
                entity.SetLocalPosition(new Vec3(0, 0, 0));
                ColorCache.Remove(entity);
                PerformanceDebugger.OnPoolReuse();
                return entity;
            }
            PerformanceDebugger.OnPoolCreate();
            return GameEntity.CreateEmpty(Mission.Current.Scene);
        }

        public static void Return(GameEntity entity)
        {
            if (entity == null) return;
            entity.SetLocalPosition(new Vec3(0, 0, 0));
            ColorCache.Remove(entity);
            _pool.Push(entity);
            PerformanceDebugger.OnPoolReturn();
        }

        public static void Clear()
        {
            ColorCache.Clear();
            _pool.Clear();
        }
    }
}
