using UnityEngine;
using UnityEngine.Serialization;

public enum SystemCameraMode2A
{
    FollowShip,
    FreeLook,
    ReturningToShip
}

/// <summary>
/// Единственный владелец системной камеры.
///
/// Главный принцип ограничения:
/// - сначала рассчитывается допустимый диапазон focus point;
/// - затем ограничивается target focus;
/// - только после этого выполняется SmoothDamp;
/// - старый post-correction больше не двигает карту
///   против пользовательского drag.
/// </summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class SystemCameraController2A :
    CustomMonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private Camera targetCamera;

    [SerializeField]
    private Transform shipTarget;

    [SerializeField]
    private SystemCameraConfig cameraConfig;

    [Tooltip(
        "SystemMapRoot. Используется для поиска SunNodeView " +
        "и преобразования Sun.LocalOffset в мировые координаты.")]
    [SerializeField]
    private Transform systemMapContentRoot;

    [Header("State")]

    [SerializeField]
    private SystemCameraMode2A mode =
        SystemCameraMode2A.FollowShip;

    [Header("Bounds")]

    [Tooltip(
        "WorldBoundsRect из SystemCameraConfig считается локальным " +
        "прямоугольником относительно центра текущего солнца. " +
        "Для систем, построенных вокруг Sun.LocalOffset, должно быть включено.")]
    [SerializeField]
    private bool worldBoundsAreRelativeToSun = true;

    [FormerlySerializedAs("centerSunWhenWholeMapFits")]
    [FormerlySerializedAs("centerSunAtMaximumZoom")]
    [Tooltip(
        "Когда viewport становится больше карты по оси, " +
        "эта ось фиксируется на центре солнца.")]
    [SerializeField]
    private bool centerOversizedAxesOnSun = true;

    [Tooltip(
        "На максимальном Target Zoom обе оси плавно возвращаются " +
        "к центру солнца, даже если одна из осей ещё имеет диапазон drag.")]
    [SerializeField]
    private bool forceFullCenterAtMaximumZoom = true;

    [Min(0.01f)]
    [SerializeField]
    private float maximumZoomTolerance = 1f;

    [FormerlySerializedAs("blockDragWhenWholeMapFits")]
    [FormerlySerializedAs("blockDragAtMaximumZoom")]
    [Tooltip(
        "На максимальном zoom запрещает drag, пока камера " +
        "автоматически удерживает солнце в центре.")]
    [SerializeField]
    private bool blockDragAtMaximumZoom = true;

    [Min(0f)]
    [Tooltip(
        "Внутренний отступ от World Bounds в мировых единицах. " +
        "Обычно оставьте 0.")]
    [SerializeField]
    private float worldBoundsPadding = 0f;

    [Header("Zoom Center Preservation")]

    [Tooltip(
        "Если перед изменением zoom солнце уже находится в центре, " +
        "контроллер сохраняет его в центре на всём пути zoom. " +
        "Это устраняет уход карты вверх перед overview-режимом.")]
    [SerializeField]
    private bool preserveSunCenterDuringZoom = true;

    [Min(0f)]
    [Tooltip(
        "Допуск в мировых единицах, в пределах которого focus " +
        "считается установленным на солнце.")]
    [SerializeField]
    private float sunCenterPreservationTolerance = 8f;

    [Header("Diagnostics")]

    [SerializeField]
    private bool logInitialization = true;

    [SerializeField]
    private bool logModeChanges = false;

    [FormerlySerializedAs("logSunCenteringChanges")]
    [FormerlySerializedAs("logMaximumZoomCentering")]
    [SerializeField]
    private bool logBoundsStateChanges = false;

    private SimpleEventBus _eventBus;
    private IGameSessionService _gameSessionService;
    private IConfigService _configService;
    private ISystemTravelService _systemTravelService;

    private bool _isInitialized;
    private bool _isSystemCameraActive;
    private bool _isSubscribedToEvents;
    private bool _ownershipErrorReported;
    private bool _missingSunWarningLogged;

    private bool _horizontalAxisLocked;
    private bool _verticalAxisLocked;
    private bool _maximumZoomCenteringActive;
    private bool _sunCenterPreservationActive;
    private bool _forceCenterXActive;
    private bool _forceCenterYActive;
    private bool _lastLoggedHorizontalLock;
    private bool _lastLoggedVerticalLock;
    private bool _lastLoggedMaximumCentering;

    private SunNodeView _cachedSunNodeView;

    private Vector3 _currentFocusPoint;
    private Vector3 _targetFocusPoint;
    private Vector3 _focusVelocity;

    private float _currentZoom;
    private float _targetZoom;
    private float _zoomVelocity;

    private Vector3 _fixedPerspectiveOffsetDirection;
    private Quaternion _fixedPerspectiveRotation;
    private bool _fixedPerspectiveOrientationReady;

    public SystemCameraMode2A Mode =>
        mode;

    public bool IsSystemCameraActive =>
        _isSystemCameraActive;

    public bool IsSunCenteredForOverview =>
        _maximumZoomCenteringActive ||
        (_horizontalAxisLocked &&
         _verticalAxisLocked);

    public bool IsHorizontalAxisLocked =>
        _horizontalAxisLocked;

    public bool IsVerticalAxisLocked =>
        _verticalAxisLocked;

    public Camera TargetCamera =>
        targetCamera;

    public SystemCameraConfig CameraConfig =>
        cameraConfig;

    public float CurrentZoom =>
        _currentZoom;

    public float TargetZoom =>
        _targetZoom;

    public float MaxOrthographicSizeWithoutEmptySpace
    {
        get
        {
            if (cameraConfig == null)
                return 2000f;

            Rect bounds =
                cameraConfig.WorldBoundsRect;

            float aspect =
                targetCamera != null
                    ? Mathf.Max(
                        0.01f,
                        targetCamera.aspect)
                    : 1f;

            float maximumByHeight =
                bounds.height * 0.5f;

            float maximumByWidth =
                bounds.width * 0.5f / aspect;

            return Mathf.Min(
                cameraConfig.MaxOrthographicSize,
                maximumByHeight,
                maximumByWidth);
        }
    }

    private void Reset()
    {
        targetCamera =
            GetComponent<Camera>();

        systemMapContentRoot =
            transform;
    }

    public void Initialize()
    {
        if (_isInitialized)
            return;

        ResolveReferences();

        if (targetCamera == null)
        {
            Debug.LogError(
                "[SystemCameraController2A] " +
                "Target Camera is not assigned.",
                this);

            return;
        }

        if (cameraConfig == null)
        {
            Debug.LogError(
                "[SystemCameraController2A] " +
                "SystemCameraConfig is not assigned.",
                this);

            return;
        }

        if (!ValidateCameraOwnership())
            return;

        RefreshFixedPerspectiveOrientation();
        ResolveServices();
        SubscribeToEvents();

        _isInitialized = true;

        ActivateSystemCameraSafely();

        if (logInitialization)
        {
            Debug.Log(
                "[SystemCameraController2A] Initialized. " +
                "Target-first bounds clamp is active.",
                this);
        }
    }

    private void ResolveReferences()
    {
        if (targetCamera == null)
        {
            targetCamera =
                GetComponent<Camera>();
        }

        if (targetCamera == null)
        {
            targetCamera =
                Camera.main;
        }

        if (systemMapContentRoot == null)
        {
            systemMapContentRoot =
                transform;
        }
    }

    private void ResolveServices()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            Debug.LogWarning(
                "[SystemCameraController2A] " +
                "Bootstrapper or ServiceRegistry is not ready. " +
                "Camera will use scene references.",
                this);

            return;
        }

        Bootstrapper.Instance
            .ServiceRegistry
            .TryGet(out _eventBus);

        Bootstrapper.Instance
            .ServiceRegistry
            .TryGet(out _gameSessionService);

        Bootstrapper.Instance
            .ServiceRegistry
            .TryGet(out _configService);

        Bootstrapper.Instance
            .ServiceRegistry
            .TryGet(out _systemTravelService);
    }

    private bool ValidateCameraOwnership()
    {
        if (targetCamera == null)
            return false;

        MapCameraController legacyWriter =
            targetCamera.GetComponent<
                MapCameraController>();

        if (legacyWriter != null &&
            legacyWriter.enabled)
        {
            ReportOwnershipError(
                "[SystemCameraController2A] " +
                "MapCameraController is enabled on " +
                $"'{targetCamera.gameObject.name}'. " +
                "Disable it before starting System Scene.");

            return false;
        }

        SystemCameraController2A[] controllers =
            FindObjectsByType<
                SystemCameraController2A>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

        foreach (
            SystemCameraController2A controller
            in controllers)
        {
            if (controller == null ||
                controller == this ||
                !controller.isActiveAndEnabled)
            {
                continue;
            }

            if (controller.targetCamera ==
                targetCamera)
            {
                ReportOwnershipError(
                    "[SystemCameraController2A] " +
                    "More than one active controller targets " +
                    $"camera '{targetCamera.gameObject.name}'.");

                return false;
            }
        }

        return true;
    }

    private void ReportOwnershipError(
        string message)
    {
        if (_ownershipErrorReported)
            return;

        _ownershipErrorReported = true;

        Debug.LogError(
            message,
            this);
    }

    private void RefreshFixedPerspectiveOrientation()
    {
        if (cameraConfig == null)
        {
            _fixedPerspectiveOffsetDirection =
                Vector3.back;

            _fixedPerspectiveRotation =
                Quaternion.identity;

            _fixedPerspectiveOrientationReady =
                true;

            return;
        }

        Vector3 offset =
            SystemCameraMath2A
                .CreatePerspectiveOffset(
                    1f,
                    cameraConfig
                        .PerspectiveTiltFromTop,
                    cameraConfig
                        .PerspectiveYaw);

        if (!SystemCameraMath2A.IsFinite(offset) ||
            offset.sqrMagnitude < 0.000001f)
        {
            offset =
                Vector3.back;
        }

        _fixedPerspectiveOffsetDirection =
            offset.normalized;

        Vector3 fixedForward =
            -_fixedPerspectiveOffsetDirection;

        _fixedPerspectiveRotation =
            Quaternion.LookRotation(
                fixedForward,
                Vector3.up);

        _fixedPerspectiveOrientationReady =
            true;
    }

    private void ActivateSystemCameraSafely()
    {
        if (targetCamera == null ||
            cameraConfig == null)
        {
            return;
        }

        RefreshFixedPerspectiveOrientation();

        _isSystemCameraActive =
            true;

        InvalidateSunReference();
        ResetBoundsRuntimeState();
        _sunCenterPreservationActive = false;

        Vector3 shipPosition =
            GetShipTargetPosition();

        _currentFocusPoint =
            ToGameplayPlane(
                shipPosition);

        _targetFocusPoint =
            _currentFocusPoint;

        float defaultZoom =
            GetDefaultZoomForCurrentSystem();

        _currentZoom =
            ClampZoom(defaultZoom);

        _targetZoom =
            _currentZoom;

        _focusVelocity =
            Vector3.zero;

        _zoomVelocity =
            0f;

        SetMode(
            SystemCameraMode2A.FollowShip);

        ApplyProjectionSettings();
        ConstrainTargetAndCurrentFocus(
            snapCurrentToTarget: true);
        ApplyCameraPoseAtFocus(
            _currentFocusPoint,
            _currentZoom);
    }

    private void LateUpdate()
    {
        if (!_isInitialized ||
            !_isSystemCameraActive ||
            targetCamera == null ||
            cameraConfig == null)
        {
            return;
        }

        ApplyProjectionSettings();
        UpdateTargetsForCurrentMode();
        SmoothZoom();
        ConstrainTargetFocus();
        SmoothFocus();
        ConstrainCurrentFocusWithoutOversizedSnap();
        ApplyCameraPoseAtFocus(
            _currentFocusPoint,
            _currentZoom);
        LogBoundsStateIfChanged();

        if (mode ==
            SystemCameraMode2A.ReturningToShip)
        {
            TryFinishReturningToShip();
        }
    }

    private void UpdateTargetsForCurrentMode()
    {
        /*
         * Пока выполняется zoom, начатый из центрированного
         * положения, не разрешаем FollowShip заменить центр
         * солнца позицией корабля.
         *
         * После завершения обычного zoom FollowShip снова
         * работает, если overview-lock по осям не активен.
         */
        if (_sunCenterPreservationActive &&
            (IsZoomTransitionActive() ||
             _forceCenterXActive ||
             _forceCenterYActive) &&
            TryGetSystemCenterWorld(
                out Vector3 preservedCenter))
        {
            _targetFocusPoint =
                ToGameplayPlane(
                    preservedCenter);

            return;
        }

        switch (mode)
        {
            case SystemCameraMode2A.FollowShip:
                UpdateFollowTarget();
                break;

            case SystemCameraMode2A.ReturningToShip:
                _targetFocusPoint =
                    ToGameplayPlane(
                        GetShipTargetPosition());

                _targetZoom =
                    ClampZoom(
                        GetDefaultZoomForCurrentSystem());
                break;

            case SystemCameraMode2A.FreeLook:
                break;
        }
    }

    private void UpdateFollowTarget()
    {
        Vector3 shipPosition =
            ToGameplayPlane(
                GetShipTargetPosition());

        float distance =
            Vector2.Distance(
                new Vector2(
                    _currentFocusPoint.x,
                    _currentFocusPoint.y),
                new Vector2(
                    shipPosition.x,
                    shipPosition.y));

        if (distance <=
            cameraConfig.FollowDeadZone)
        {
            _targetFocusPoint =
                _currentFocusPoint;

            return;
        }

        _targetFocusPoint =
            shipPosition;
    }

    private void SmoothZoom()
    {
        float deltaTime =
            Mathf.Max(
                0.00001f,
                Time.unscaledDeltaTime);

        _currentZoom =
            Mathf.SmoothDamp(
                _currentZoom,
                _targetZoom,
                ref _zoomVelocity,
                cameraConfig.ZoomSmoothTime,
                Mathf.Infinity,
                deltaTime);

        _currentZoom =
            ClampZoom(
                _currentZoom);
    }

    private void SmoothFocus()
    {
        float deltaTime =
            Mathf.Max(
                0.00001f,
                Time.unscaledDeltaTime);

        _currentFocusPoint =
            Vector3.SmoothDamp(
                _currentFocusPoint,
                _targetFocusPoint,
                ref _focusVelocity,
                GetFocusSmoothTime(),
                Mathf.Infinity,
                deltaTime);

        _currentFocusPoint.z =
            cameraConfig.GameplayPlaneZ;
    }

    private bool IsZoomTransitionActive()
    {
        return
            Mathf.Abs(
                _currentZoom -
                _targetZoom) > 0.01f ||
            Mathf.Abs(
                _zoomVelocity) > 0.01f;
    }

    private float GetFocusSmoothTime()
    {
        switch (mode)
        {
            case SystemCameraMode2A.ReturningToShip:
                return cameraConfig.ReturnSmoothTime;

            case SystemCameraMode2A.FreeLook:
                return cameraConfig.FreeLookSmoothTime;

            default:
                return cameraConfig.FollowSmoothTime;
        }
    }

    public void EnterFreeLook()
    {
        if (!_isSystemCameraActive)
            return;

        if (_maximumZoomCenteringActive &&
            blockDragAtMaximumZoom)
        {
            return;
        }

        /*
         * Явный drag означает, что игрок хочет выйти
         * из автоматического удержания солнца.
         * На максимальном zoom это по-прежнему запрещено
         * настройкой blockDragAtMaximumZoom.
         */
        _sunCenterPreservationActive = false;

        SetMode(
            SystemCameraMode2A.FreeLook);

        _targetFocusPoint =
            _currentFocusPoint;

        _focusVelocity =
            Vector3.zero;

        /*
         * Пересчитываем lock-состояние сразу, чтобы первый
         * кадр drag не использовал устаревшие overview-locks.
         */
        ConstrainTargetFocus();
    }

    public void ReturnToShip()
    {
        if (!_isInitialized)
        {
            Initialize();
        }

        if (!_isInitialized)
            return;

        if (!_isSystemCameraActive)
        {
            ActivateSystemCameraSafely();
        }

        _sunCenterPreservationActive = false;

        _targetFocusPoint =
            ToGameplayPlane(
                GetShipTargetPosition());

        _targetZoom =
            ClampZoom(
                GetDefaultZoomForCurrentSystem());

        _focusVelocity =
            Vector3.zero;

        _zoomVelocity =
            0f;

        SetMode(
            SystemCameraMode2A.ReturningToShip);
    }

    public void NotifyManualZoomStarted()
    {
        if (!_isSystemCameraActive)
            return;

        if (mode !=
            SystemCameraMode2A.ReturningToShip)
        {
            return;
        }

        SetMode(
            SystemCameraMode2A.FollowShip);

        _zoomVelocity =
            0f;
    }

    /// <summary>
    /// Положительное значение приближает камеру.
    /// Отрицательное значение отдаляет камеру.
    /// </summary>
    public void AdjustZoom(
        float zoomInInput)
    {
        if (!_isSystemCameraActive ||
            cameraConfig == null)
        {
            return;
        }

        if (!SystemCameraMath2A.IsFinite(
                zoomInInput))
        {
            return;
        }

        NotifyManualZoomStarted();
        PrepareSunCenterPreservationForZoom();

        _targetZoom -=
            zoomInInput *
            cameraConfig.ZoomUnitsPerInput;

        _targetZoom =
            ClampZoom(
                _targetZoom);

        /*
         * Пересчитываем target bounds сразу по новому target zoom.
         * Это не даёт focus начать позднее движение к границе.
         */
        ConstrainTargetFocus();
    }

    public void SetTargetZoom(
        float value)
    {
        PrepareSunCenterPreservationForZoom();

        _targetZoom =
            ClampZoom(
                value);

        _zoomVelocity =
            0f;

        ConstrainTargetFocus();
    }

    public void SetZoomImmediate(
        float value)
    {
        PrepareSunCenterPreservationForZoom();

        float clampedValue =
            ClampZoom(
                value);

        _currentZoom =
            clampedValue;

        _targetZoom =
            clampedValue;

        _zoomVelocity =
            0f;

        ApplyProjectionSettings();
        ConstrainTargetAndCurrentFocus(
            snapCurrentToTarget: false);
        ApplyCameraPoseAtFocus(
            _currentFocusPoint,
            _currentZoom);
    }

    public bool IsZoomAtTarget(
        float tolerance)
    {
        return Mathf.Abs(
            _currentZoom -
            _targetZoom) <=
            Mathf.Max(
                0.001f,
                tolerance);
    }

    public void MoveFreeLookByScreenDelta(
        Vector2 screenDelta)
    {
        if (!_isSystemCameraActive ||
            targetCamera == null)
        {
            return;
        }

        if (_maximumZoomCenteringActive &&
            blockDragAtMaximumZoom)
        {
            return;
        }

        if (!SystemCameraMath2A.IsFinite(
                screenDelta))
        {
            return;
        }

        if (Screen.width <= 0 ||
            Screen.height <= 0)
        {
            return;
        }

        EnterFreeLook();

        if (_maximumZoomCenteringActive &&
            blockDragAtMaximumZoom)
        {
            return;
        }

        Vector2 screenCenter =
            new Vector2(
                Screen.width * 0.5f,
                Screen.height * 0.5f);

        Vector2 movedScreenPoint =
            screenCenter +
            screenDelta;

        if (!TryScreenPointToGameplayPlane(
                screenCenter,
                out Vector3 centerWorld))
        {
            return;
        }

        if (!TryScreenPointToGameplayPlane(
                movedScreenPoint,
                out Vector3 movedWorld))
        {
            return;
        }

        Vector3 worldMovement =
            centerWorld -
            movedWorld;

        worldMovement.z =
            0f;

        worldMovement *=
            cameraConfig.DragSensitivity;

        if (_horizontalAxisLocked)
        {
            worldMovement.x =
                0f;
        }

        if (_verticalAxisLocked)
        {
            worldMovement.y =
                0f;
        }

        _targetFocusPoint +=
            worldMovement;

        _targetFocusPoint.z =
            cameraConfig.GameplayPlaneZ;

        ConstrainTargetFocus();
    }

    public void ReclampCurrentPosition()
    {
        if (!_isSystemCameraActive)
            return;

        ApplyProjectionSettings();
        ConstrainTargetAndCurrentFocus(
            snapCurrentToTarget: false);
        ApplyCameraPoseAtFocus(
            _currentFocusPoint,
            _currentZoom);
    }

    private bool TryScreenPointToGameplayPlane(
        Vector2 screenPoint,
        out Vector3 worldPoint)
    {
        worldPoint =
            Vector3.zero;

        if (targetCamera == null ||
            cameraConfig == null)
        {
            return false;
        }

        Ray ray =
            targetCamera.ScreenPointToRay(
                screenPoint);

        return SystemCameraMath2A
            .TryIntersectRayWithPlaneZ(
                ray,
                cameraConfig.GameplayPlaneZ,
                out worldPoint);
    }

    private void ConstrainTargetFocus()
    {
        /*
         * Target focus ограничивается по TARGET zoom.
         * Благодаря этому контроллер заранее знает будущий
         * размер viewport и не начинает позднюю коррекцию
         * уже во время SmoothDamp zoom.
         */
        if (!TryBuildClampContext(
                _targetZoom,
                out Vector3 systemCenter,
                out Rect effectiveBounds,
                out Vector2 footprintMinimumOffset,
                out Vector2 footprintMaximumOffset))
        {
            ResetBoundsRuntimeState();
            return;
        }

        _maximumZoomCenteringActive =
            forceFullCenterAtMaximumZoom &&
            IsTargetAtMaximumZoom();

        bool centeredViewExceedsBoundsX =
            DoesCenteredViewExceedBoundsX(
                effectiveBounds,
                systemCenter,
                footprintMinimumOffset,
                footprintMaximumOffset);

        bool centeredViewExceedsBoundsY =
            DoesCenteredViewExceedBoundsY(
                effectiveBounds,
                systemCenter,
                footprintMinimumOffset,
                footprintMaximumOffset);

        /*
         * Если zoom начался при уже центрированном солнце,
         * не разрешаем строгому perspective-clamp сдвинуть
         * focus к асимметричной границе карты.
         *
         * Ось удерживается на солнце только с момента,
         * когда центрированный viewport перестаёт полностью
         * помещаться в строгие bounds. До этого обычный
         * диапазон drag сохраняется.
         */
        bool preserveCenterX =
            preserveSunCenterDuringZoom &&
            _sunCenterPreservationActive &&
            centeredViewExceedsBoundsX;

        bool preserveCenterY =
            preserveSunCenterDuringZoom &&
            _sunCenterPreservationActive &&
            centeredViewExceedsBoundsY;

        _forceCenterXActive =
            _maximumZoomCenteringActive ||
            preserveCenterX;

        _forceCenterYActive =
            _maximumZoomCenteringActive ||
            preserveCenterY;

        Vector2 requested =
            new Vector2(
                _targetFocusPoint.x,
                _targetFocusPoint.y);

        Vector2 clamped =
            SystemCameraMath2A.ClampFocusPoint(
                effectiveBounds,
                requested,
                footprintMinimumOffset,
                footprintMaximumOffset,
                new Vector2(
                    systemCenter.x,
                    systemCenter.y),
                centerOversizedAxesOnSun,
                _forceCenterXActive,
                _forceCenterYActive,
                out _horizontalAxisLocked,
                out _verticalAxisLocked);

        _targetFocusPoint =
            new Vector3(
                clamped.x,
                clamped.y,
                cameraConfig.GameplayPlaneZ);
    }

    /// <summary>
    /// Ограничивает current focus только на осях,
    /// где существует допустимый диапазон.
    ///
    /// На оси, где viewport уже больше карты,
    /// current focus плавно идёт к target focus,
    /// а не телепортируется в центр.
    /// </summary>
    private void ConstrainCurrentFocusWithoutOversizedSnap()
    {
        if (!TryBuildClampContext(
                _currentZoom,
                out Vector3 systemCenter,
                out Rect effectiveBounds,
                out Vector2 footprintMinimumOffset,
                out Vector2 footprintMaximumOffset))
        {
            return;
        }

        Vector2 requested =
            new Vector2(
                _currentFocusPoint.x,
                _currentFocusPoint.y);

        Vector2 clamped =
            SystemCameraMath2A.ClampFocusPoint(
                effectiveBounds,
                requested,
                footprintMinimumOffset,
                footprintMaximumOffset,
                new Vector2(
                    systemCenter.x,
                    systemCenter.y),
                centerOversizedAxes: false,
                forceCenterX: false,
                forceCenterY: false,
                out _,
                out _);

        /*
         * Когда target ось удерживается на солнце,
         * current ось должна свободно SmoothDamp-двигаться
         * к этому target. Строгий current-clamp не должен
         * каждый кадр возвращать её к асимметричной границе.
         */
        if (_forceCenterXActive)
        {
            clamped.x =
                requested.x;
        }

        if (_forceCenterYActive)
        {
            clamped.y =
                requested.y;
        }

        _currentFocusPoint =
            new Vector3(
                clamped.x,
                clamped.y,
                cameraConfig.GameplayPlaneZ);
    }

    private void ConstrainTargetAndCurrentFocus(
        bool snapCurrentToTarget)
    {
        ConstrainTargetFocus();

        if (snapCurrentToTarget)
        {
            _currentFocusPoint =
                _targetFocusPoint;

            _focusVelocity =
                Vector3.zero;

            return;
        }

        ConstrainCurrentFocusWithoutOversizedSnap();
    }

    private bool TryBuildClampContext(
        float zoom,
        out Vector3 systemCenter,
        out Rect effectiveBounds,
        out Vector2 footprintMinimumOffset,
        out Vector2 footprintMaximumOffset)
    {
        systemCenter =
            Vector3.zero;

        effectiveBounds =
            default;

        footprintMinimumOffset =
            Vector2.zero;

        footprintMaximumOffset =
            Vector2.zero;

        if (targetCamera == null ||
            cameraConfig == null)
        {
            return false;
        }

        if (!TryGetSystemCenterWorld(
                out systemCenter))
        {
            return false;
        }

        systemCenter =
            ToGameplayPlane(
                systemCenter);

        effectiveBounds =
            GetEffectiveWorldBounds(
                systemCenter);

        if (effectiveBounds.width <= 0f ||
            effectiveBounds.height <= 0f)
        {
            return false;
        }

        if (!TryGetViewportFootprintOffsets(
                systemCenter,
                ClampZoom(zoom),
                out footprintMinimumOffset,
                out footprintMaximumOffset))
        {
            return false;
        }

        return true;
    }

    private bool DoesCenteredViewExceedBoundsX(
        Rect bounds,
        Vector3 systemCenter,
        Vector2 footprintMinimumOffset,
        Vector2 footprintMaximumOffset)
    {
        float centeredMinimum =
            systemCenter.x +
            footprintMinimumOffset.x;

        float centeredMaximum =
            systemCenter.x +
            footprintMaximumOffset.x;

        return
            centeredMinimum < bounds.xMin - 0.001f ||
            centeredMaximum > bounds.xMax + 0.001f;
    }

    private bool DoesCenteredViewExceedBoundsY(
        Rect bounds,
        Vector3 systemCenter,
        Vector2 footprintMinimumOffset,
        Vector2 footprintMaximumOffset)
    {
        float centeredMinimum =
            systemCenter.y +
            footprintMinimumOffset.y;

        float centeredMaximum =
            systemCenter.y +
            footprintMaximumOffset.y;

        return
            centeredMinimum < bounds.yMin - 0.001f ||
            centeredMaximum > bounds.yMax + 0.001f;
    }

    /// <summary>
    /// Если до изменения zoom камера уже смотрит на солнце,
    /// включает сохранение этого визуального якоря.
    /// Состояние снимается первым явным drag или сменой режима.
    /// </summary>
    private void PrepareSunCenterPreservationForZoom()
    {
        if (!preserveSunCenterDuringZoom)
        {
            _sunCenterPreservationActive =
                false;

            return;
        }

        if (_sunCenterPreservationActive)
            return;

        if (!TryGetSystemCenterWorld(
                out Vector3 systemCenter))
        {
            return;
        }

        systemCenter =
            ToGameplayPlane(
                systemCenter);

        float safeTolerance =
            Mathf.Max(
                0f,
                sunCenterPreservationTolerance);

        bool currentCentered =
            Vector2.Distance(
                new Vector2(
                    _currentFocusPoint.x,
                    _currentFocusPoint.y),
                new Vector2(
                    systemCenter.x,
                    systemCenter.y)) <=
            safeTolerance;

        bool targetCentered =
            Vector2.Distance(
                new Vector2(
                    _targetFocusPoint.x,
                    _targetFocusPoint.y),
                new Vector2(
                    systemCenter.x,
                    systemCenter.y)) <=
            safeTolerance;

        if (!currentCentered &&
            !targetCentered)
        {
            return;
        }

        _sunCenterPreservationActive =
            true;

        _targetFocusPoint =
            ToGameplayPlane(
                systemCenter);

        /*
         * Если камера уже практически в центре,
         * убираем остаточную скорость focus, чтобы
         * она не создала небольшой drift при zoom.
         */
        if (currentCentered)
        {
            _currentFocusPoint =
                ToGameplayPlane(
                    systemCenter);

            _focusVelocity =
                Vector3.zero;
        }
    }

    private Rect GetEffectiveWorldBounds(
        Vector3 systemCenter)
    {
        Rect configuredBounds =
            cameraConfig.WorldBoundsRect;

        Vector2 originOffset =
            worldBoundsAreRelativeToSun
                ? new Vector2(
                    systemCenter.x,
                    systemCenter.y)
                : Vector2.zero;

        Rect worldBounds =
            new Rect(
                configuredBounds.x +
                originOffset.x,
                configuredBounds.y +
                originOffset.y,
                configuredBounds.width,
                configuredBounds.height);

        float safePadding =
            Mathf.Max(
                0f,
                worldBoundsPadding);

        if (safePadding > 0f &&
            worldBounds.width > safePadding * 2f &&
            worldBounds.height > safePadding * 2f)
        {
            worldBounds.xMin +=
                safePadding;

            worldBounds.xMax -=
                safePadding;

            worldBounds.yMin +=
                safePadding;

            worldBounds.yMax -=
                safePadding;
        }

        return worldBounds;
    }

    private bool TryGetViewportFootprintOffsets(
        Vector3 referenceFocus,
        float zoom,
        out Vector2 footprintMinimumOffset,
        out Vector2 footprintMaximumOffset)
    {
        footprintMinimumOffset =
            Vector2.zero;

        footprintMaximumOffset =
            Vector2.zero;

        ApplyCameraPoseAtFocus(
            referenceFocus,
            zoom);

        if (!TryGetViewportFootprint(
                out Vector2 footprintMinimum,
                out Vector2 footprintMaximum))
        {
            return false;
        }

        Vector2 reference =
            new Vector2(
                referenceFocus.x,
                referenceFocus.y);

        footprintMinimumOffset =
            footprintMinimum -
            reference;

        footprintMaximumOffset =
            footprintMaximum -
            reference;

        return
            SystemCameraMath2A.IsFinite(
                footprintMinimumOffset) &&
            SystemCameraMath2A.IsFinite(
                footprintMaximumOffset);
    }

    private bool TryGetViewportFootprint(
        out Vector2 footprintMinimum,
        out Vector2 footprintMaximum)
    {
        footprintMinimum =
            new Vector2(
                float.PositiveInfinity,
                float.PositiveInfinity);

        footprintMaximum =
            new Vector2(
                float.NegativeInfinity,
                float.NegativeInfinity);

        Vector2[] viewportCorners =
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f)
        };

        foreach (
            Vector2 viewportCorner
            in viewportCorners)
        {
            Ray ray =
                targetCamera.ViewportPointToRay(
                    viewportCorner);

            if (!SystemCameraMath2A
                    .TryIntersectRayWithPlaneZ(
                        ray,
                        cameraConfig.GameplayPlaneZ,
                        out Vector3 point))
            {
                return false;
            }

            footprintMinimum.x =
                Mathf.Min(
                    footprintMinimum.x,
                    point.x);

            footprintMinimum.y =
                Mathf.Min(
                    footprintMinimum.y,
                    point.y);

            footprintMaximum.x =
                Mathf.Max(
                    footprintMaximum.x,
                    point.x);

            footprintMaximum.y =
                Mathf.Max(
                    footprintMaximum.y,
                    point.y);
        }

        return
            SystemCameraMath2A.IsFinite(
                footprintMinimum) &&
            SystemCameraMath2A.IsFinite(
                footprintMaximum);
    }

    private bool IsTargetAtMaximumZoom()
    {
        if (cameraConfig == null)
            return false;

        float tolerance =
            Mathf.Max(
                0.01f,
                maximumZoomTolerance);

        return _targetZoom >=
               cameraConfig.MaxZoom -
               tolerance;
    }

    private bool TryGetSystemCenterWorld(
        out Vector3 worldCenter)
    {
        worldCenter =
            Vector3.zero;

        if (_cachedSunNodeView == null ||
            !_cachedSunNodeView
                .gameObject
                .activeInHierarchy)
        {
            _cachedSunNodeView =
                FindSunNodeView();
        }

        if (_cachedSunNodeView != null)
        {
            SpriteRenderer rootSprite =
                _cachedSunNodeView
                    .GetComponent<
                        SpriteRenderer>();

            if (rootSprite == null)
            {
                rootSprite =
                    _cachedSunNodeView
                        .GetComponentInChildren<
                            SpriteRenderer>(true);
            }

            if (rootSprite != null &&
                rootSprite.enabled &&
                rootSprite.gameObject.activeInHierarchy)
            {
                worldCenter =
                    rootSprite.bounds.center;

                if (SystemCameraMath2A.IsFinite(
                        worldCenter))
                {
                    _missingSunWarningLogged =
                        false;

                    return true;
                }
            }

            worldCenter =
                _cachedSunNodeView
                    .transform
                    .position;

            if (SystemCameraMath2A.IsFinite(
                    worldCenter))
            {
                _missingSunWarningLogged =
                    false;

                return true;
            }
        }

        StarSystemConfig currentSystem =
            GetCurrentSystemConfig();

        if (currentSystem != null &&
            currentSystem.Sun != null)
        {
            Vector2 localOffset =
                currentSystem
                    .Sun
                    .LocalOffset;

            Vector3 localCenter =
                new Vector3(
                    localOffset.x,
                    localOffset.y,
                    cameraConfig.GameplayPlaneZ);

            worldCenter =
                systemMapContentRoot != null
                    ? systemMapContentRoot
                        .TransformPoint(
                            localCenter)
                    : localCenter;

            if (SystemCameraMath2A.IsFinite(
                    worldCenter))
            {
                _missingSunWarningLogged =
                    false;

                return true;
            }
        }

        if (!_missingSunWarningLogged)
        {
            _missingSunWarningLogged =
                true;

            Debug.LogWarning(
                "[SystemCameraController2A] " +
                "System center was not found. " +
                "Bounds clamp was skipped.",
                this);
        }

        return false;
    }

    private SunNodeView FindSunNodeView()
    {
        if (systemMapContentRoot != null)
        {
            SunNodeView sunInsideMap =
                systemMapContentRoot
                    .GetComponentInChildren<
                        SunNodeView>(true);

            if (sunInsideMap != null)
            {
                return sunInsideMap;
            }
        }

        return FindFirstObjectByType<
            SunNodeView>();
    }

    private void InvalidateSunReference()
    {
        _cachedSunNodeView =
            null;

        _missingSunWarningLogged =
            false;
    }

    private void ResetBoundsRuntimeState()
    {
        _horizontalAxisLocked =
            false;

        _verticalAxisLocked =
            false;

        _maximumZoomCenteringActive =
            false;

        _forceCenterXActive =
            false;

        _forceCenterYActive =
            false;
    }

    private void LogBoundsStateIfChanged()
    {
        if (!logBoundsStateChanges)
            return;

        if (_lastLoggedHorizontalLock ==
                _horizontalAxisLocked &&
            _lastLoggedVerticalLock ==
                _verticalAxisLocked &&
            _lastLoggedMaximumCentering ==
                _maximumZoomCenteringActive)
        {
            return;
        }

        _lastLoggedHorizontalLock =
            _horizontalAxisLocked;

        _lastLoggedVerticalLock =
            _verticalAxisLocked;

        _lastLoggedMaximumCentering =
            _maximumZoomCenteringActive;

        Debug.Log(
            "[SystemCameraController2A] " +
            $"Bounds state: lockX={_horizontalAxisLocked}, " +
            $"lockY={_verticalAxisLocked}, " +
            $"maxCenter={_maximumZoomCenteringActive}, " +
            $"currentZoom={_currentZoom:F2}, " +
            $"targetZoom={_targetZoom:F2}.",
            this);
    }

    private void ApplyProjectionSettings()
    {
        bool usePerspective =
            cameraConfig.ProjectionMode ==
            SystemCameraProjection2A.Perspective;

        targetCamera.orthographic =
            !usePerspective;

        targetCamera.nearClipPlane =
            cameraConfig.NearClipPlane;

        targetCamera.farClipPlane =
            cameraConfig.FarClipPlane;

        targetCamera.lensShift =
            Vector2.zero;

        if (usePerspective)
        {
            targetCamera.fieldOfView =
                cameraConfig
                    .PerspectiveFieldOfView;
        }
        else
        {
            targetCamera.orthographicSize =
                ClampZoom(
                    _currentZoom);
        }
    }

    private void ApplyCameraPoseAtFocus(
        Vector3 focusPoint,
        float zoom)
    {
        focusPoint =
            ToGameplayPlane(
                focusPoint);

        float safeZoom =
            ClampZoom(
                zoom);

        if (cameraConfig.ProjectionMode ==
            SystemCameraProjection2A.Perspective)
        {
            if (!_fixedPerspectiveOrientationReady)
            {
                RefreshFixedPerspectiveOrientation();
            }

            Vector3 cameraPosition =
                focusPoint +
                _fixedPerspectiveOffsetDirection *
                safeZoom;

            targetCamera.transform
                .SetPositionAndRotation(
                    cameraPosition,
                    _fixedPerspectiveRotation);

            return;
        }

        Vector3 orthographicPosition =
            new Vector3(
                focusPoint.x,
                focusPoint.y,
                cameraConfig
                    .OrthographicCameraZ);

        targetCamera.transform
            .SetPositionAndRotation(
                orthographicPosition,
                Quaternion.identity);

        targetCamera.orthographicSize =
            safeZoom;
    }

    private void TryFinishReturningToShip()
    {
        float positionDistance =
            Vector2.Distance(
                new Vector2(
                    _currentFocusPoint.x,
                    _currentFocusPoint.y),
                new Vector2(
                    _targetFocusPoint.x,
                    _targetFocusPoint.y));

        float zoomDistance =
            Mathf.Abs(
                _currentZoom -
                _targetZoom);

        if (positionDistance >
            cameraConfig.ReturnPositionTolerance)
        {
            return;
        }

        if (zoomDistance >
            cameraConfig.ReturnZoomTolerance)
        {
            return;
        }

        SetMode(
            SystemCameraMode2A.FollowShip);

        _focusVelocity =
            Vector3.zero;

        _zoomVelocity =
            0f;
    }

    private void SetMode(
        SystemCameraMode2A newMode)
    {
        if (mode == newMode)
            return;

        SystemCameraMode2A previousMode =
            mode;

        mode =
            newMode;

        if (logModeChanges)
        {
            Debug.Log(
                "[SystemCameraController2A] " +
                $"Mode: {previousMode} -> {newMode}",
                this);
        }
    }

    private float GetDefaultZoomForCurrentSystem()
    {
        if (cameraConfig == null)
            return 1200f;

        float desiredZoom =
            cameraConfig.DefaultZoom;

        StarSystemConfig currentSystem =
            GetCurrentSystemConfig();

        if (currentSystem == null)
        {
            return ClampZoom(
                desiredZoom);
        }

        float largestObjectRadius =
            GetLargestObjectRadius(
                currentSystem);

        desiredZoom =
            Mathf.Max(
                desiredZoom,
                largestObjectRadius *
                cameraConfig
                    .LargestObjectZoomMultiplier);

        return ClampZoom(
            desiredZoom);
    }

    private float ClampZoom(
        float value)
    {
        if (cameraConfig == null)
            return Mathf.Max(1f, value);

        return SystemCameraMath2A
            .ClampZoom(
                value,
                cameraConfig.MinZoom,
                cameraConfig.MaxZoom);
    }

    private Vector3 GetShipTargetPosition()
    {
        if (shipTarget != null)
        {
            return shipTarget.position;
        }

        if (_systemTravelService != null &&
            _systemTravelService.State != null)
        {
            return
                _systemTravelService
                    .State
                    .GetCurrentPosition();
        }

        return new Vector3(
            0f,
            0f,
            cameraConfig != null
                ? cameraConfig.GameplayPlaneZ
                : 0f);
    }

    private Vector3 ToGameplayPlane(
        Vector3 position)
    {
        position.z =
            cameraConfig != null
                ? cameraConfig.GameplayPlaneZ
                : 0f;

        return position;
    }

    private StarSystemConfig GetCurrentSystemConfig()
    {
        if (_gameSessionService == null ||
            _gameSessionService.State == null ||
            _gameSessionService.State.Player == null ||
            _configService == null)
        {
            return null;
        }

        string currentSystemId =
            _gameSessionService
                .State
                .Player
                .CurrentSystemId;

        if (string.IsNullOrWhiteSpace(
                currentSystemId))
        {
            return null;
        }

        return _configService
            .TryGetStarSystem(
                currentSystemId,
                out StarSystemConfig systemConfig)
            ? systemConfig
            : null;
    }

    private static float GetLargestObjectRadius(
        StarSystemConfig systemConfig)
    {
        if (systemConfig == null)
            return 0f;

        float largestRadius =
            0f;

        if (systemConfig.Sun != null)
        {
            largestRadius =
                Mathf.Max(
                    largestRadius,
                    systemConfig
                        .Sun
                        .VisualSize *
                    0.5f);
        }

        if (systemConfig.PlanetRefs != null)
        {
            foreach (
                PlanetConfig planet
                in systemConfig.PlanetRefs)
            {
                if (planet == null ||
                    planet.PlanetOrbit == null)
                {
                    continue;
                }

                largestRadius =
                    Mathf.Max(
                        largestRadius,
                        planet
                            .PlanetOrbit
                            .PlanetVisualSize *
                        0.5f);
            }
        }

        if (systemConfig.Station != null)
        {
            largestRadius =
                Mathf.Max(
                    largestRadius,
                    systemConfig
                        .Station
                        .VisualSize *
                    0.5f);
        }

        return largestRadius;
    }

    private void SubscribeToEvents()
    {
        if (_isSubscribedToEvents ||
            _eventBus == null)
        {
            return;
        }

        _eventBus.Subscribe<
            GalaxyEnteredEvent>(
                OnGalaxyEntered);

        _eventBus.Subscribe<
            PlanetEnteredEvent>(
                OnPlanetEntered);

        _eventBus.Subscribe<
            StarSystemEnteredEvent>(
                OnSystemEntered);

        _isSubscribedToEvents =
            true;
    }

    private void UnsubscribeFromEvents()
    {
        if (!_isSubscribedToEvents ||
            _eventBus == null)
        {
            return;
        }

        _eventBus.Unsubscribe<
            GalaxyEnteredEvent>(
                OnGalaxyEntered);

        _eventBus.Unsubscribe<
            PlanetEnteredEvent>(
                OnPlanetEntered);

        _eventBus.Unsubscribe<
            StarSystemEnteredEvent>(
                OnSystemEntered);

        _isSubscribedToEvents =
            false;
    }

    private void OnSystemEntered(
        StarSystemEnteredEvent evt)
    {
        InvalidateSunReference();
        ActivateSystemCameraSafely();
    }

    private void OnGalaxyEntered(
        GalaxyEnteredEvent evt)
    {
        _isSystemCameraActive =
            false;

        _sunCenterPreservationActive = false;
        ResetBoundsRuntimeState();
        InvalidateSunReference();

        _focusVelocity =
            Vector3.zero;

        _zoomVelocity =
            0f;
    }

    private void OnPlanetEntered(
        PlanetEnteredEvent evt)
    {
        _isSystemCameraActive =
            false;

        _sunCenterPreservationActive = false;
        ResetBoundsRuntimeState();
        InvalidateSunReference();

        _focusVelocity =
            Vector3.zero;

        _zoomVelocity =
            0f;

        SetMode(
            SystemCameraMode2A.FreeLook);
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }
}