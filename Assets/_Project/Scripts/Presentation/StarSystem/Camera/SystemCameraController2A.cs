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
/// Компонент отвечает только за представление:
/// - положение камеры;
/// - поворот камеры;
/// - zoom;
/// - Follow Ship;
/// - Free Look;
/// - Return To Ship;
/// - ограничение камеры границами карты;
/// - центрирование солнца, когда вся карта помещается в кадр.
///
/// Координаты и движение корабля рассчитываются
/// исключительно в сервисах.
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
        "Корневой объект содержимого системной карты. " +
        "Внутри него будет найден созданный SunNodeView.")]
    [SerializeField]
    private Transform systemMapContentRoot;

    [Header("State")]

    [SerializeField]
    private SystemCameraMode2A mode =
        SystemCameraMode2A.FollowShip;

    [Header("Overview Centering")]

    [FormerlySerializedAs("centerSunAtMaximumZoom")]
    [Tooltip(
        "Центрировать реальное солнце сразу после того, " +
        "как вся карта начинает помещаться в кадр. " +
        "Точного достижения Max Zoom ждать не требуется.")]
    [SerializeField]
    private bool centerSunWhenWholeMapFits = true;

    [Tooltip(
        "Дополнительный отступ границ карты от краёв viewport. " +
        "0 означает включать центрирование сразу после того, " +
        "как вся карта вошла в кадр.")]
    [Range(0f, 0.1f)]
    [SerializeField]
    private float overviewViewportPadding = 0f;

    [FormerlySerializedAs("blockDragAtMaximumZoom")]
    [Tooltip(
        "Запретить drag, пока вся карта помещается " +
        "и солнце зафиксировано в центре.")]
    [SerializeField]
    private bool blockDragWhenWholeMapFits = true;

    [Header("Diagnostics")]

    [SerializeField]
    private bool logInitialization = true;

    [SerializeField]
    private bool logModeChanges = false;

    [FormerlySerializedAs("logMaximumZoomCentering")]
    [SerializeField]
    private bool logSunCenteringChanges = false;

    private SimpleEventBus _eventBus;
    private IGameSessionService _gameSessionService;
    private IConfigService _configService;
    private ISystemTravelService _systemTravelService;

    private bool _isInitialized;
    private bool _isSystemCameraActive;
    private bool _isSubscribedToEvents;
    private bool _ownershipErrorReported;

    /*
     * true означает:
     * - вся карта помещается;
     * - солнце является focus point;
     * - обычный bounds correction отключён.
     */
    private bool _isSunCenteredForOverview;

    private bool _missingSunWarningLogged;

    private SunNodeView _cachedSunNodeView;

    private Vector3 _currentFocusPoint;
    private Vector3 _targetFocusPoint;
    private Vector3 _focusVelocity;

    /*
     * Perspective:
     * zoom = расстояние от камеры до focus point.
     *
     * Orthographic:
     * zoom = Orthographic Size.
     */
    private float _currentZoom;
    private float _targetZoom;
    private float _zoomVelocity;

    /*
     * Угол Perspective-камеры рассчитывается
     * только один раз из конфигурации.
     *
     * Zoom меняет только расстояние.
     */
    private Vector3 _fixedPerspectiveOffsetDirection;
    private Quaternion _fixedPerspectiveRotation;
    private bool _fixedPerspectiveOrientationReady;

    public SystemCameraMode2A Mode =>
        mode;

    public bool IsSystemCameraActive =>
        _isSystemCameraActive;

    public bool IsSunCenteredForOverview =>
        _isSunCenteredForOverview;

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
                bounds.width *
                0.5f /
                aspect;

            return Mathf.Min(
                cameraConfig.MaxOrthographicSize,
                maximumByHeight,
                maximumByWidth);
        }
    }

    private void Reset()
    {
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
                "[SystemCameraController2A] " +
                "Initialized. Perspective angle is locked.",
                this);
        }
    }

    private void ResolveReferences()
    {
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

    /// <summary>
    /// Рассчитывает и фиксирует направление камеры.
    ///
    /// После этого zoom не может изменить угол.
    /// </summary>
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

        if (!IsFinite(offset) ||
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

        SetSunCenteredForOverview(false);
        InvalidateSunReference();

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
            ClampZoom(
                defaultZoom);

        _targetZoom =
            _currentZoom;

        _focusVelocity =
            Vector3.zero;

        _zoomVelocity =
            0f;

        SetMode(
            SystemCameraMode2A.FollowShip);

        ApplyCameraPoseAndClamp();
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

        UpdateTargetsForCurrentMode();
        SmoothCurrentCameraState();
        ApplyCameraPoseAndClamp();

        if (mode ==
            SystemCameraMode2A.ReturningToShip)
        {
            TryFinishReturningToShip();
        }
    }

    private void UpdateTargetsForCurrentMode()
    {
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
        /*
         * Пока вся карта помещается,
         * солнце остаётся focus point.
         */
        if (_isSunCenteredForOverview)
            return;

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

    private void SmoothCurrentCameraState()
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

    private float GetFocusSmoothTime()
    {
        switch (mode)
        {
            case SystemCameraMode2A.ReturningToShip:
                return
                    cameraConfig.ReturnSmoothTime;

            case SystemCameraMode2A.FreeLook:
                return
                    cameraConfig.FreeLookSmoothTime;

            default:
                return
                    cameraConfig.FollowSmoothTime;
        }
    }

    public void EnterFreeLook()
    {
        if (!_isSystemCameraActive)
            return;

        if (_isSunCenteredForOverview &&
            blockDragWhenWholeMapFits)
        {
            return;
        }

        SetMode(
            SystemCameraMode2A.FreeLook);

        _targetFocusPoint =
            _currentFocusPoint;

        _focusVelocity =
            Vector3.zero;
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

        SetSunCenteredForOverview(false);

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

        if (!IsFinite(zoomInInput))
            return;

        NotifyManualZoomStarted();

        _targetZoom -=
            zoomInInput *
            cameraConfig.ZoomUnitsPerInput;

        _targetZoom =
            ClampZoom(
                _targetZoom);
    }

    public void SetTargetZoom(
        float value)
    {
        _targetZoom =
            ClampZoom(
                value);

        _zoomVelocity =
            0f;
    }

    public void SetZoomImmediate(
        float value)
    {
        float clampedValue =
            ClampZoom(
                value);

        _currentZoom =
            clampedValue;

        _targetZoom =
            clampedValue;

        _zoomVelocity =
            0f;

        ApplyCameraPoseAndClamp();
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

        if (_isSunCenteredForOverview &&
            blockDragWhenWholeMapFits)
        {
            return;
        }

        if (!IsFinite(screenDelta))
            return;

        if (Screen.width <= 0 ||
            Screen.height <= 0)
        {
            return;
        }

        EnterFreeLook();

        if (_isSunCenteredForOverview &&
            blockDragWhenWholeMapFits)
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

        _targetFocusPoint +=
            worldMovement;

        _targetFocusPoint.z =
            cameraConfig.GameplayPlaneZ;
    }

    public void ReclampCurrentPosition()
    {
        if (!_isSystemCameraActive)
            return;

        ApplyCameraPoseAndClamp();
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

    private void ApplyCameraPoseAndClamp()
    {
        if (targetCamera == null ||
            cameraConfig == null)
        {
            return;
        }

        ApplyProjectionSettings();

        /*
         * Проверка выполняется на каждом кадре.
         *
         * Как только вся карта начинает помещаться
         * при фокусе на солнце, солнце сразу становится
         * центром экрана.
         *
         * Точного Max Zoom ждать не требуется.
         */
        if (TryCenterSunWhenWholeMapFits())
        {
            return;
        }

        SetSunCenteredForOverview(false);

        ApplyCameraPoseAtFocus(
            _currentFocusPoint);

        if (!TryGetViewportFootprint(
                out Vector2 footprintMinimum,
                out Vector2 footprintMaximum))
        {
            return;
        }

        Vector2 correction =
            SystemCameraMath2A
                .CalculateBoundsCorrection(
                    cameraConfig.WorldBoundsRect,
                    footprintMinimum,
                    footprintMaximum);

        if (correction.sqrMagnitude <=
            0.000001f)
        {
            return;
        }

        _currentFocusPoint.x +=
            correction.x;

        _currentFocusPoint.y +=
            correction.y;

        _targetFocusPoint.x +=
            correction.x;

        _targetFocusPoint.y +=
            correction.y;

        ApplyCameraPoseAtFocus(
            _currentFocusPoint);
    }

    /// <summary>
    /// Центрирует солнце сразу после того,
    /// как вся карта начинает помещаться в viewport.
    ///
    /// Метод не ждёт достижения Max Zoom.
    /// </summary>
    private bool TryCenterSunWhenWholeMapFits()
    {
        if (!centerSunWhenWholeMapFits)
        {
            SetSunCenteredForOverview(false);
            return false;
        }

        if (!TryGetSunWorldPosition(
                out Vector3 sunWorldPosition))
        {
            SetSunCenteredForOverview(false);
            return false;
        }

        sunWorldPosition =
            ToGameplayPlane(
                sunWorldPosition);

        /*
         * Временно ставим камеру так,
         * чтобы солнце было focus point.
         *
         * Рендер между этим действием
         * и проверкой границ не выполняется.
         */
        ApplyCameraPoseAtFocus(
            sunWorldPosition);

        bool wholeMapFits =
            AreWorldBoundsInsideViewport(
                cameraConfig.WorldBoundsRect,
                overviewViewportPadding);

        if (!wholeMapFits)
        {
            /*
             * Карта ещё не помещается.
             * Возвращаем обычную позицию камеры.
             */
            SetSunCenteredForOverview(false);

            ApplyCameraPoseAtFocus(
                _currentFocusPoint);

            return false;
        }

        /*
         * Вся карта уже помещается.
         *
         * Солнце становится точным focus point.
         * Обычный bounds correction после этого
         * не выполняется и не может сдвинуть карту вверх.
         */
        _currentFocusPoint =
            sunWorldPosition;

        _targetFocusPoint =
            sunWorldPosition;

        _focusVelocity =
            Vector3.zero;

        SetSunCenteredForOverview(true);

        ApplyCameraPoseAtFocus(
            sunWorldPosition);

        return true;
    }

    /// <summary>
    /// Проверяет, находятся ли все четыре угла
    /// World Bounds внутри viewport камеры.
    /// </summary>
    private bool AreWorldBoundsInsideViewport(
        Rect bounds,
        float padding)
    {
        if (targetCamera == null ||
            cameraConfig == null)
        {
            return false;
        }

        float safePadding =
            Mathf.Clamp(
                padding,
                0f,
                0.1f);

        float viewportMinimum =
            safePadding;

        float viewportMaximum =
            1f - safePadding;

        Vector3[] worldCorners =
        {
            new Vector3(
                bounds.xMin,
                bounds.yMin,
                cameraConfig.GameplayPlaneZ),

            new Vector3(
                bounds.xMin,
                bounds.yMax,
                cameraConfig.GameplayPlaneZ),

            new Vector3(
                bounds.xMax,
                bounds.yMin,
                cameraConfig.GameplayPlaneZ),

            new Vector3(
                bounds.xMax,
                bounds.yMax,
                cameraConfig.GameplayPlaneZ)
        };

        foreach (
            Vector3 worldCorner
            in worldCorners)
        {
            Vector3 viewportPoint =
                targetCamera.WorldToViewportPoint(
                    worldCorner);

            if (!IsFinite(viewportPoint))
                return false;

            /*
             * Точка должна находиться перед камерой.
             */
            if (viewportPoint.z <= 0f)
                return false;

            if (viewportPoint.x <
                    viewportMinimum ||
                viewportPoint.x >
                    viewportMaximum)
            {
                return false;
            }

            if (viewportPoint.y <
                    viewportMinimum ||
                viewportPoint.y >
                    viewportMaximum)
            {
                return false;
            }
        }

        return true;
    }

    private bool TryGetSunWorldPosition(
        out Vector3 worldPosition)
    {
        worldPosition =
            Vector3.zero;

        if (_cachedSunNodeView == null ||
            !_cachedSunNodeView
                .gameObject
                .activeInHierarchy)
        {
            _cachedSunNodeView =
                FindSunNodeView();
        }

        if (_cachedSunNodeView == null)
        {
            if (!_missingSunWarningLogged)
            {
                _missingSunWarningLogged =
                    true;

                Debug.LogWarning(
                    "[SystemCameraController2A] " +
                    "SunNodeView was not found. " +
                    "Overview centering was skipped.",
                    this);
            }

            return false;
        }

        _missingSunWarningLogged =
            false;

        worldPosition =
            _cachedSunNodeView
                .transform
                .position;

        return IsFinite(
            worldPosition);
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

    private void SetSunCenteredForOverview(
        bool centered)
    {
        if (_isSunCenteredForOverview ==
            centered)
        {
            return;
        }

        _isSunCenteredForOverview =
            centered;

        if (logSunCenteringChanges)
        {
            Debug.Log(
                "[SystemCameraController2A] " +
                $"Sun centered for overview: {centered}.",
                this);
        }
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
        Vector3 focusPoint)
    {
        focusPoint =
            ToGameplayPlane(
                focusPoint);

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
                _currentZoom;

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
            ClampZoom(
                _currentZoom);
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
            IsFinite(footprintMinimum) &&
            IsFinite(footprintMaximum);
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
        /*
         * При входе в систему карта пересоздаёт солнце.
         * Старую ссылку необходимо сбросить.
         */
        InvalidateSunReference();
        ActivateSystemCameraSafely();
    }

    private void OnGalaxyEntered(
        GalaxyEnteredEvent evt)
    {
        _isSystemCameraActive =
            false;

        SetSunCenteredForOverview(false);
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

        SetSunCenteredForOverview(false);
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

    private static bool IsFinite(
        float value)
    {
        return
            !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }

    private static bool IsFinite(
        Vector2 value)
    {
        return
            IsFinite(value.x) &&
            IsFinite(value.y);
    }

    private static bool IsFinite(
        Vector3 value)
    {
        return
            IsFinite(value.x) &&
            IsFinite(value.y) &&
            IsFinite(value.z);
    }
}