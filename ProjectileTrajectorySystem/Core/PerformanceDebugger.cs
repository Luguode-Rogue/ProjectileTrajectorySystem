// 性能诊断工具：统计 GameEntity 相关热点操作的每帧调用次数
// 使用方式：设置 EnablePerfDebug = true 开启，false 关闭
using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProjectileTrajectorySystem
{
    internal static class PerformanceDebugger
    {
        // ===== 开关：true 开启诊断统计 =====
        public static bool EnablePerfDebug = false;

        // ===== 单帧计数器 =====
        public static int InstantiateCount;
        public static int AddAllMeshesCount;
        public static int SetLocalPositionCount;
        public static int RayCastCount;
        public static int SetContourColorCount;
        public static int PoolCreateCount;
        public static int PoolReuseCount;
        public static int PoolReturnCount;
        public static int ClearNormalTrajectoryCount;
        public static int ClearEnemyTrajectoryCount;

        // ===== 累计计数器 =====
        public static int TotalInstantiate;
        public static int TotalAddAllMeshes;
        public static int TotalSetLocalPosition;
        public static int TotalRayCast;
        public static int TotalSetContourColor;
        public static int TotalPoolCreate;
        public static int TotalPoolReuse;
        public static int TotalPoolReturn;
        public static int TotalClearNormal;
        public static int TotalClearEnemy;

        // ===== 有效实体追踪（检测泄漏）=====
        private static readonly HashSet<UIntPtr> _aliveInstantiatedEntities = new();
        public static int LeakedEntityCount => _aliveInstantiatedEntities.Count;

        // ===== 帧计数 =====
        private static int _frameCount;

        /// <summary>记录 GameEntity.Instantiate 调用</summary>
        public static void OnInstantiate(GameEntity entity)
        {
            if (!EnablePerfDebug) return;
            InstantiateCount++;
            if (entity != null && entity.Pointer != UIntPtr.Zero)
                _aliveInstantiatedEntities.Add(entity.Pointer);
        }

        /// <summary>记录 AddAllMeshesOfGameEntity 调用</summary>
        public static void OnAddAllMeshes()
        {
            if (!EnablePerfDebug) return;
            AddAllMeshesCount++;
        }

        /// <summary>记录 SetLocalPosition 调用</summary>
        public static void OnSetLocalPosition()
        {
            if (!EnablePerfDebug) return;
            SetLocalPositionCount++;
        }

        /// <summary>记录 RayCast 调用</summary>
        public static void OnRayCast()
        {
            if (!EnablePerfDebug) return;
            RayCastCount++;
        }

        /// <summary>记录 SetContourColor 调用</summary>
        public static void OnSetContourColor()
        {
            if (!EnablePerfDebug) return;
            SetContourColorCount++;
        }

        /// <summary>记录从池中复用实体</summary>
        public static void OnPoolReuse()
        {
            if (!EnablePerfDebug) return;
            PoolReuseCount++;
        }

        /// <summary>记录新创建空实体</summary>
        public static void OnPoolCreate()
        {
            if (!EnablePerfDebug) return;
            PoolCreateCount++;
        }

        /// <summary>记录归还实体到池</summary>
        public static void OnPoolReturn()
        {
            if (!EnablePerfDebug) return;
            PoolReturnCount++;
        }

        /// <summary>记录 ClearNormalTrajectory</summary>
        public static void OnClearNormalTrajectory()
        {
            if (!EnablePerfDebug) return;
            ClearNormalTrajectoryCount++;
        }

        /// <summary>记录 ClearEnemyTrajectory</summary>
        public static void OnClearEnemyTrajectory()
        {
            if (!EnablePerfDebug) return;
            ClearEnemyTrajectoryCount++;
        }

        /// <summary>每帧结束时调用，累加统计</summary>
        public static void EndOfFrame()
        {
            if (!EnablePerfDebug) return;

            TotalInstantiate += InstantiateCount;
            TotalAddAllMeshes += AddAllMeshesCount;
            TotalSetLocalPosition += SetLocalPositionCount;
            TotalRayCast += RayCastCount;
            TotalSetContourColor += SetContourColorCount;
            TotalPoolCreate += PoolCreateCount;
            TotalPoolReuse += PoolReuseCount;
            TotalPoolReturn += PoolReturnCount;
            TotalClearNormal += ClearNormalTrajectoryCount;
            TotalClearEnemy += ClearEnemyTrajectoryCount;

            _frameCount++;

            ResetFrameCounters();
        }

        private static void ResetFrameCounters()
        {
            InstantiateCount = 0;
            AddAllMeshesCount = 0;
            SetLocalPositionCount = 0;
            RayCastCount = 0;
            SetContourColorCount = 0;
            PoolCreateCount = 0;
            PoolReuseCount = 0;
            PoolReturnCount = 0;
            ClearNormalTrajectoryCount = 0;
            ClearEnemyTrajectoryCount = 0;
        }

        /// <summary>重置所有累计统计</summary>
        public static void ResetAll()
        {
            TotalInstantiate = 0;
            TotalAddAllMeshes = 0;
            TotalSetLocalPosition = 0;
            TotalRayCast = 0;
            TotalSetContourColor = 0;
            TotalPoolCreate = 0;
            TotalPoolReuse = 0;
            TotalPoolReturn = 0;
            TotalClearNormal = 0;
            TotalClearEnemy = 0;
            _frameCount = 0;
            _aliveInstantiatedEntities.Clear();
            ResetFrameCounters();
        }
    }
}
