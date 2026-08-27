// Mission 卸载期安全校验工具。
//
// 背景：Bannerlord 的 Agent / AgentVisuals / GameEntity / Camera / MissionScreen 等都是
// 原生（非托管）对象的托管包装器。当 Mission 结束或卸载时，引擎会释放底层原生对象，
// 但托管侧的引用仍然存在且不为 null。此时再调用 SetContourColor / SetAlpha / Remove /
// SetFrame 等方法，会直接读写已释放的内存，抛出 System.AccessViolationException。
//
// 关键点：AccessViolationException 属于 CorruptedStateException（损坏状态异常），
// 在 .NET 默认策略下 **无法被 try/catch 捕获**（除非给方法标注
// [HandleProcessCorruptedStateExceptions]，而这只是掩盖问题、进程状态已不可信）。
// 因此唯一正确的做法是：在触碰原生对象之前先做存活性校验，从源头避免访问。
//
// 使用原则：
//   1. Mission 卸载路径（OnEndMission / OnRemoveBehavior / Destroy / 终结器）上，
//      只清理托管状态（字典、列表、引用置空），绝不回写引擎对象。
//   2. 运行期（Tick 中）操作原生对象前，先用本类的方法校验。
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace ProjectileTrajectorySystem
{
    internal static class MissionSafety
    {
        /// <summary>
        /// 当前 Mission 与其场景是否仍然存活、可安全操作原生对象。
        /// Mission 为 null、正在结束（MissionIsEnding）、已卸载（Scene == null）时返回 false。
        /// </summary>
        public static bool IsSceneAlive()
        {
            try
            {
                Mission mission = Mission.Current;
                return mission != null
                    && !mission.MissionIsEnding
                    && !mission.IsFinalized
                    && mission.Scene != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 判断 Agent 的视觉对象此刻是否可安全操作（描边、Alpha 等）。
        /// </summary>
        public static bool CanTouchVisuals(Agent agent)
        {
            if (agent == null) return false;
            if (!IsSceneAlive()) return false;

            try
            {
                if (!agent.IsActive()) return false;
                return agent.AgentVisuals != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 判断 GameEntity 此刻是否可安全操作。
        /// </summary>
        public static bool CanTouchEntity(GameEntity entity)
        {
            if (entity == null) return false;
            if (!IsSceneAlive()) return false;

            try
            {
                // 已从场景摘除的实体不可再操作
                return entity.Scene != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 判断 WeakGameEntity 此刻是否可安全操作。
        /// </summary>
        public static bool CanTouchEntity(WeakGameEntity entity)
        {
            if (entity == null) return false;
            if (!IsSceneAlive()) return false;

            try
            {
                return !WeakGameEntity.Invalid.Equals(entity);
            }
            catch
            {
                return false;
            }
        }
    }
}
