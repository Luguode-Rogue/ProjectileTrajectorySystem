using System;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;

namespace ProjectileTrajectorySystem
{
    /// <summary>
    /// 攻城武器落点视角相机系统 + 落点红边高亮目标标记。
    /// 相机接管逻辑严格对齐 New_ZZZF/TacticalMap/Core/CameraController 的成熟实现：
    /// 全程只写自己的 Camera 对象（Camera.CreateCamera），退出时把进入前记录的玩家视角原样
    /// 还原给 CombatCamera，再交还引擎控制权，保证进/出前后视角完全一致、不发生突变。
    /// 并按需显式 ReleaseCamera() 释放原生资源，防止跨帧访问已释放内存导致 AccessViolationException。
    ///
    /// 与战术地图的差异点（本功能需求）：
    /// - 切换后进入持续跟随模式：调整工程器落点时镜头自动跟着变动，再次按键才飞回（不自动飞回）。
    /// - 落点处额外放置红色描边（SetContourColor）目标标记实体。
    /// </summary>
    public class SiegeLandingCameraSystem
    {
        private enum CameraState
        {
            Idle,
            FlyingOut,
            Holding
        }

        public MissionScreen _ms;
        public Mission Mission => Mission.Current;

        private CameraState _state = CameraState.Idle;

        // 观察参数：本视角固定为"正上方垂直俯视"（从上到下、不加额外倾斜角度）。
        private float _viewBearing = 3.14159265f;  // 俯视时的水平旋转方位（取工程器当前朝向，保证旋转与工程器方向一致）
        private float _viewDistance = 0f;          // 水平后退距离：正上方俯视不需要水平偏移
        private float _viewHeight = 60f;           // 落点正上方抬高高度（决定俯视高度，越大越能看清落点范围）
        private const float _viewHeightMin = 8f;   // 滚轮可下调的最小俯视高度
        private const float _viewHeightMax = 400f; // 滚轮可上调的最大俯视高度
        private const float _scrollSensitivity = 0.12f; // 鼠标滚轮每格调整的高度量（约为原 12 的百分之一）
        private bool _viewHeightDirty = true;      // 俯视高度被滚轮改动后需要重建观察帧
        private float _currentViewHeight = 60f;    // 渲染实际使用的俯视高度（朝 _viewHeight 平滑插值）
        private const float _heightLerpRate = 6f;  // 高度平滑插值速率（越大越快贴合目标）
        private const float _flightDuration = 0.6f; // 飞行时长（秒）

        private MatrixFrame _originalFrame;      // 进入新模式前抓取的玩家视角矩阵（退出时原样复原）
        private bool _hasSavedPlayerFrame = false;
        private MatrixFrame _fromFrame;
        private MatrixFrame _toFrame;
        private MatrixFrame _currentFrame;
        private Camera _camera;                  // 接管用的独立 Camera（必须显式 ReleaseCamera）

        private float _timer;

        // 落点红边高亮目标标记
        private GameEntity _landingMarker;
        private Vec3 _cachedLandingPoint = Vec3.Invalid;
        private bool _hasLandingPoint;

        private const string MarkerPrefabPath = "mangonel_mapicon_projectile";

        /// <summary>当前是否处于接管相机状态（用于屏蔽普通相机逻辑）</summary>
        public bool IsActive => _state != CameraState.Idle;

        public SiegeLandingCameraSystem(MissionScreen ms)
        {
            _ms = ms;
        }

        /// <summary>
        /// 设置当前计算出的预期落点。由 PlayerTrajectorySystem 在每帧攻城武器弹道更新后回填。
        /// </summary>
        public void SetLandingPoint(Vec3 point)
        {
            // 严格以 IsFinite 判定有效性：NULL/Invalid/NaN/Inf 一律视为无效。
            // 注意不能用 "point != Vec3.Invalid" 参与判断——NaN 与任何值比较都不等，
            // 单独靠它无法屏蔽 NaN，必须依赖 IsFinite。
            bool valid = IsFinite(point);
            _cachedLandingPoint = valid ? point : Vec3.Invalid;
            _hasLandingPoint = valid;
        }

        /// <summary>
        /// 设置当前操作的攻城武器大类（投石车 Mangonel / 投石器 Trebuchet），
        /// 用于按武器特性微调落点视角（投石器射程更远，悬停更高以覆盖更大落点范围）。
        /// </summary>
        private string _lastSiegeType; // 记录上次设定的武器类型，避免每帧覆盖用户滚轮调整后的高度

        public void SetSiegeType(string typeName)
        {
            // 仅在武器类型真正变化时才写入默认俯视高度/距离。
            // 否则每帧调用会不断把用户滚轮调整后的 _viewHeight 打回默认值（"移动落点高度回去了"的根因）。
            if (typeName == _lastSiegeType) return;
            _lastSiegeType = typeName;

            switch (typeName)
            {
                case "Trebuchet":
                    _viewDistance = 42f;
                    _viewHeight = 36f;
                    break;
                case "Mangonel":
                    _viewDistance = 30f;
                    _viewHeight = 26f;
                    break;
                default:
                    _viewDistance = 34f;
                    _viewHeight = 30f;
                    break;
            }
        }

        /// <summary>
        /// 鼠标滚轮调整俯视高度：scrollDelta > 0 降低（看得更近），< 0 抬高（看范围更大）。
        /// 调整后置 _viewHeightDirty，下一帧重建观察帧即可生效。
        /// </summary>
        public void AdjustHeight(float scrollDelta)
        {
            if (scrollDelta == 0f) return;
            _viewHeight -= scrollDelta * _scrollSensitivity;
            if (_viewHeight < _viewHeightMin) _viewHeight = _viewHeightMin;
            if (_viewHeight > _viewHeightMax) _viewHeight = _viewHeightMax;
            _viewHeightDirty = true;
        }

        /// <summary>
        /// 记录"玩家是否正在操作工程器"的状态。
        /// 新模式功能仅在玩家操作工程器时生效：一旦检测到玩家脱离工程器（usingSiege=false）
        /// 且当前正处于落点视角，则自动退出（Release）新模式。
        /// </summary>
        public bool IsPlayerUsingSiege { get; private set; }

        public void NotifyPlayerSiegeState(bool usingSiege)
        {
            bool wasUsing = IsPlayerUsingSiege;
            IsPlayerUsingSiege = usingSiege;
            if (!usingSiege && wasUsing && _state != CameraState.Idle)
            {
                // 玩家意外脱离工程器：自动退出落点视角新模式
                Release();
            }
        }

        /// <summary>
        /// 切换落点视角（飞往落点 / 飞回原位）。
        /// </summary>
        public void Toggle()
        {
            if (_state == CameraState.Idle)
            {
                // 新模式仅在玩家正在操作工程器时允许进入
                if (!IsPlayerUsingSiege)
                {
                    return;
                }
                if (!_hasLandingPoint || _cachedLandingPoint == Vec3.Invalid || !IsFinite(_cachedLandingPoint))
                {
                    return;
                }
                // 进入新模式前，抓取玩家当前视角矩阵并记录下来。
                // 退出时只把这个记录的视角原样还原给 CombatCamera，不做任何飞回动画、不改动其他状态。
                if (_ms != null && _ms.CombatCamera != null)
                {
                    _originalFrame = _ms.CombatCamera.Frame;
                    _hasSavedPlayerFrame = true;
                }
                StartFlyingOut();
            }
            else
            {
                // FlyingOut / Holding 状态下按切换键 = 退出落点模式。
                // 直接把进入前记录的视角还原给引擎相机，然后交还控制权，不飞回、不改动其他。
                Release();
            }
        }

        private void StartFlyingOut()
        {
            if (_ms == null || _ms.CombatCamera == null) return;

            // 优先使用 Toggle 进入前抓取的玩家视角矩阵（此时 CombatCamera 尚未被接管，帧最准确）
            if (!_hasSavedPlayerFrame)
            {
                _originalFrame = _ms.CombatCamera.Frame;
                _hasSavedPlayerFrame = true;
            }
            _currentFrame = _originalFrame;

            // 俯视旋转方位跟随工程器（玩家当前相机）的水平朝向，保证切换后旋转与工程器方向一致。
            Vec3 camF = _originalFrame.rotation.f;
            _viewBearing = (float)Math.Atan2(-camF.x, camF.y);

            if (_camera == null)
                _camera = Camera.CreateCamera();

            _fromFrame = _originalFrame;
            _currentViewHeight = _viewHeight; // 进入时高度立即对齐目标，避免从默认高度平滑飞入
            _toFrame = BuildViewFrame(_cachedLandingPoint);

            _ms.AllowInputWithCustomCamera = false;
            _ms.CustomCamera = _camera;

            _timer = 0f;
            _state = CameraState.FlyingOut;

            ShowLandingMarker(_cachedLandingPoint);
        }



        /// <summary>
        /// 由落点构造一个俯视观察位姿（贴地 + 防穿地）。直接复用 CameraController.BuildViewFrame 的思路。
        /// </summary>
        private MatrixFrame BuildViewFrame(Vec3 target)
        {
            Scene scene = Mission?.Scene;
            if (scene != null)
            {
                try
                {
                    float ground = scene.GetGroundHeightAtPosition(new Vec3(target.x, target.y, target.z + 100f));
                    if (ground > -1000f && ground < 9999f)
                        target.z = Math.Max(target.z, ground);
                }
                catch (Exception) { }
            }

            // 正上方垂直俯视：相机在落点正上方，朝正下方看（从上到下、无额外倾斜角度）。
            // 用平滑插值后的 _currentViewHeight，使滚轮调整高度时镜头平滑升降而非瞬跳。
            Vec3 eye = new Vec3(target.x, target.y, target.z + _currentViewHeight);

            if (scene != null)
            {
                try
                {
                    float eyeGround = scene.GetGroundHeightAtPosition(new Vec3(eye.x, eye.y, eye.z + 100f));
                    if (eyeGround > -1000f && eyeGround < 9999f && eye.z < eyeGround + 1.5f)
                        eye.z = eyeGround + 1.5f;
                }
                catch (Exception) { }
            }

            if (_camera == null)
                _camera = Camera.CreateCamera();
            // 垂直俯视时视线方向是 -z，up 不能与视线平行，否则 LookAt 退化。
            // 用工程器当前水平朝向（_viewBearing）构造水平 up 向量，使旋转与工程器方向一致。
            // 需求：落点镜头沿 z 轴额外旋转 180°，故 up 方位角叠加 π（ViewBearingOffset）。
            const float ViewBearingOffset = 3.14159265f; // 绕 z 轴旋转 180°
            float upBearing = _viewBearing + ViewBearingOffset;
            Vec3 up = new Vec3((float)Math.Sin(upBearing), -(float)Math.Cos(upBearing), 0f);
            _camera.LookAt(eye, target, up);
            return _camera.Frame;
        }

        private Vec3 _lastBuiltLanding = Vec3.Invalid;

        private static bool IsFinite(Vec3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                     float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }

        private bool MissionValid()
        {
            // 任何一项失效立即放弃接管，避免在已释放的原生对象上继续操作导致 AV
            if (_ms == null || _camera == null) return false;
            if (_ms.CombatCamera == null) return false;
            Mission m = Mission;
            if (m == null || m.Scene == null || m.MissionIsEnding) return false;
            return true;
        }

        /// <summary>每帧驱动相机状态机与标记。</summary>
        public void Tick(float dt)
        {
            try
            {
                if (_state == CameraState.Idle) return;
                if (!MissionValid()) { Release(); return; }
                if (dt <= 0f) dt = 0.0166f;

                // 落点无效（NaN/Invalid）立即放弃，避免 LookAt 构造非法矩阵触发原生崩溃
                if (_cachedLandingPoint == Vec3.Invalid || !IsFinite(_cachedLandingPoint))
                {
                    Release();
                    return;
                }

                _timer += dt;

                // 俯视高度平滑插值：每帧把实际高度朝目标高度线性逼近，
                // 使滚轮调整 / 落点移动时的高度变化平滑过渡（线性插值）。
                if (Math.Abs(_currentViewHeight - _viewHeight) > 1e-3f)
                {
                    float k = MBMath.ClampFloat(_heightLerpRate * dt, 0f, 1f);
                    _currentViewHeight += (_viewHeight - _currentViewHeight) * k;
                    _viewHeightDirty = true; // 高度在变，需要重建观察帧
                }

                switch (_state)
                {
                    case CameraState.FlyingOut:
                    {
                        // 飞行途中也跟随最新落点，避免调瞄准时镜头追不上
                        _toFrame = BuildViewFrame(_cachedLandingPoint);
                        _lastBuiltLanding = _cachedLandingPoint;
                        RefreshLandingMarker(_cachedLandingPoint); // 标记同步跟随落点
                        _viewHeightDirty = false;
                        float t = MBMath.ClampFloat(_timer / _flightDuration, 0f, 1f);
                        _currentFrame = LerpFrame(_fromFrame, _toFrame, SmoothStep(t));
                        if (t >= 1f)
                        {
                            _timer = 0f;
                            _state = CameraState.Holding;
                        }
                        break;
                    }
                    case CameraState.Holding:
                    {
                        // 持续跟随模式：镜头一直钉在落点上空俯视地面，
                        // 调整工程器落点时视角自动跟着变动，直到再次按键才飞回。
                        // 仅在落点实际移动、或滚轮改了俯视高度后才重建 _toFrame
                        //（避免每帧反复 LookAt 触碰原生对象）。
                        if (_cachedLandingPoint != _lastBuiltLanding || _viewHeightDirty)
                        {
                            _toFrame = BuildViewFrame(_cachedLandingPoint);
                            _lastBuiltLanding = _cachedLandingPoint;
                            RefreshLandingMarker(_cachedLandingPoint); // 落点变动标记同步跟随
                            _viewHeightDirty = false;
                        }
                        _currentFrame = _toFrame;
                        break;
                    }
                }

                ApplyFrame();
            }
            catch (Exception ex)
            {
                // 任何异常都释放相机，避免把 AV 抛回引擎主循环
                try { Release(); } catch (Exception) { }
            }
        }

        private void ApplyFrame()
        {
            if (_camera == null) return;
            _camera.Frame = _currentFrame;
            _camera.SetFovVertical(
                (float)(60.0 * Math.PI / 180.0),
                Screen.AspectRatio,
                0.1f,
                12500f);
        }

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);

        /// <summary>
        /// 对两个相机帧做插值：原点线性插值，基向量插值后重新正交化，
        /// 避免矩阵退化导致画面扭曲。直接复用 CameraController.LerpFrame。
        /// </summary>
        private static MatrixFrame LerpFrame(MatrixFrame a, MatrixFrame b, float t)
        {
            MatrixFrame result = MatrixFrame.Identity;
            result.origin = a.origin * (1f - t) + b.origin * t;

            Vec3 f = a.rotation.f * (1f - t) + b.rotation.f * t;
            Vec3 u = a.rotation.u * (1f - t) + b.rotation.u * t;
            if (f.Length < 0.0001f) f = b.rotation.f;
            f.Normalize();
            if (u.Length < 0.0001f) u = b.rotation.u;

            Vec3 s = Vec3.CrossProduct(f, u);
            if (s.Length < 0.0001f) s = b.rotation.s;
            s.Normalize();
            u = Vec3.CrossProduct(s, f);
            u.Normalize();

            result.rotation.f = f;
            result.rotation.s = s;
            result.rotation.u = u;
            return result;
        }

        /// <summary>
        /// 归还相机控制权：把 CustomCamera 置空交还引擎，并把进入前记录的玩家视角原样还原给
        /// CombatCamera（只还原视角，不改动其他任何状态）。进入新模式时记录的 _originalFrame
        /// 即退出时恢复的目标位置，保证"进/出前后视角完全一致、不发生突变"。
        /// </summary>
        /// <param name="restoreVisuals">
        /// 是否回写引擎相机状态。Mission 卸载路径（Destroy/OnEndMission）必须传 false：
        /// 此时 MissionScreen / CombatCamera 的原生对象可能已释放，回写会触发
        /// AccessViolationException（该异常无法被 try/catch 捕获）。
        /// </param>
        public void Release(bool restoreVisuals = true)
        {
            if (restoreVisuals && _ms != null && _camera != null && MissionSafety.IsSceneAlive())
            {
                // 1) 先把进入前记录的视角原样还原给引擎相机（只动视角，其他状态一律不动）。
                if (_hasSavedPlayerFrame)
                {
                    try
                    {
                        if (_ms.CombatCamera != null)
                        {
                            MatrixFrame restore = _originalFrame;
                            _ms.CombatCamera.Frame = restore;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
                // 2) 再交还控制权，让引擎接手（此时引擎看到的就是进入前记录的视角，不再发生突变）。
                try
                {
                    _ms.CustomCamera = null;
                    _ms.AllowInputWithCustomCamera = false;
                }
                catch (Exception)
                {
                }
            }
            _state = CameraState.Idle;
            _timer = 0f;
            HideLandingMarker(restoreVisuals);
        }

        /// <summary>
        /// 任务结束时彻底释放：必须先 Release 交还控制权，再显式 ReleaseCamera() 释放原生资源，
        /// 否则下一帧原生层可能访问已释放内存，引发 AccessViolationException。
        /// </summary>
        public void Destroy()
        {
            // 场景还活着时才回写引擎状态；Mission 已在卸载则只丢弃托管引用。
            bool sceneAlive = MissionSafety.IsSceneAlive();

            try { Release(sceneAlive); } catch (Exception) { }

            if (_camera != null)
            {
                if (sceneAlive)
                {
                    try { _camera.ReleaseCamera(); } catch (Exception) { }
                }
                _camera = null;
            }

            HideLandingMarker(sceneAlive);
            _ms = null;
        }

        // ---------------- 落点红边高亮目标标记 ----------------

        /// <summary>
        /// 在落点放置一个红色描边的目标标记实体（复用工程器地图图标预制体）。
        /// 红边高亮参考 New_ZZZF EnemyOutlineSystem 的 SetContourColor 用法。
        /// </summary>
        private void ShowLandingMarker(Vec3 point)
        {
            try
            {
                HideLandingMarker();

                var mission = Mission;
                if (mission == null || mission.Scene == null) return;

                _landingMarker = GameEntity.Instantiate(mission.Scene, MarkerPrefabPath, true);
                if (_landingMarker != null)
                {
                    MatrixFrame frame = MatrixFrame.Identity;
                    frame.origin = point;
                    _landingMarker.SetFrame(ref frame);

                    // 红色描边高亮（0xFFFF0000 = 不透明红）
                    _landingMarker.SetContourColor(0xFFFF0000u, true);
                    _landingMarker.SetVisibilityExcludeParents(true);
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 落点变动时同步移动已有标记实体（不重建，避免频繁 Instantiate/Remove 触碰原生对象）。
        /// 标记只在落点实际移动时才刷新，保持与实际落点重合。
        /// </summary>
        private void RefreshLandingMarker(Vec3 point)
        {
            if (_landingMarker == null) return;
            try
            {
                MatrixFrame frame = MatrixFrame.Identity;
                frame.origin = point;
                _landingMarker.SetFrame(ref frame);
            }
            catch (Exception ex)
            {
            }
        }

        /// <param name="removeEntity">
        /// 是否真正从场景移除标记实体。Mission 卸载时传 false：原生实体已随场景销毁，
        /// 再调用 SetContourColor / Remove 会访问已释放内存。
        /// </param>
        private void HideLandingMarker(bool removeEntity = true)
        {
            var marker = _landingMarker;
            _landingMarker = null;   // 先断开引用，确保任何路径都不会重复操作同一实体

            if (!removeEntity || marker == null) return;
            if (!MissionSafety.CanTouchEntity(marker)) return;

            try
            {
                marker.SetContourColor(0u, true); // 关闭描边
                marker.Remove(0);
            }
            catch (Exception) { }
        }

        // 注意：此处刻意不实现终结器。
        // 终结器在 GC 线程上以不可控的时机执行，那时 Mission 场景通常早已销毁，
        // 在其中触碰原生 GameEntity 必然造成访问已释放内存。
        // 标记实体的生命周期由 Destroy() / Release() 在主线程上显式管理。
    }
}
