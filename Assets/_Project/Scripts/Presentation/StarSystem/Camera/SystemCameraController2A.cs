using UnityEngine;

public enum SystemCameraMode2A
{
    FollowShip,
    FreeLook,
    ReturningToShip
}

public sealed class SystemCameraController2A : CustomMonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform shipTarget;
    [SerializeField] private SystemCameraConfig cameraConfig;

    [Header("State")]
    [SerializeField] private SystemCameraMode2A mode = SystemCameraMode2A.FollowShip;

    private SimpleEventBus _eventBus;
    private IGameSessionService _gameSessionService;
    private IConfigService _configService;
    private ISystemTravelService _systemTravelService;

    private bool _isInitialized;
    private bool _isSystemCameraActive;

    private Vector3 _cameraVelocity;
    private float _defaultOrthographicSizeForCurrentSystem;
    private float _zoomVelocity;
    private Bounds _currentSystemBounds;
    private bool _hasSystemBounds;

    public SystemCameraMode2A Mode => mode;
    public bool IsSystemCameraActive => _isSystemCameraActive;

    public void Initialize()
    {
        if (_isInitialized)
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _systemTravelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();

        SubscribeToEvents();

        _isInitialized = true;
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void LateUpdate()
    {
        if (!_isInitialized)
            return;

        if (!_isSystemCameraActive)
            return;

        if (targetCamera == null)
            return;

        switch (mode)
        {
            case SystemCameraMode2A.FollowShip:
                UpdateFollowShip();
                break;

            case SystemCameraMode2A.ReturningToShip:
                UpdateReturnToShip();
                break;

            case SystemCameraMode2A.FreeLook:
                break;
        }
    }

    public void EnterFreeLook()
    {
        if (!_isSystemCameraActive)
            return;

        mode = SystemCameraMode2A.FreeLook;
        _cameraVelocity = Vector3.zero;
    }

    public void ReturnToShip()
    {
        if (!_isSystemCameraActive)
            return;

        mode = SystemCameraMode2A.ReturningToShip;
        _cameraVelocity = Vector3.zero;
        _zoomVelocity = 0f;
    }

    public void MoveFreeLookByScreenDelta(Vector2 screenDelta)
    {
        if (!_isSystemCameraActive)
            return;

        if (targetCamera == null)
            return;

        EnterFreeLook();

        float unitsPerPixel = GetWorldUnitsPerScreenPixel();
        Vector3 worldDelta = new Vector3(
            screenDelta.x * unitsPerPixel,
            screenDelta.y * unitsPerPixel,
            0f
        );

        Vector3 currentPosition = targetCamera.transform.position;

        // Палец/мышь вправо — карта визуально едет вправо,
        // значит камера должна сдвинуться влево.
        Vector3 nextPosition = currentPosition - worldDelta * cameraConfig.DragSensitivity;

        MoveCameraTo(nextPosition);
    }

    private void UpdateFollowShip()
    {
        Vector3 shipPosition = GetShipTargetPosition();
        Vector3 cameraPosition = targetCamera.transform.position;

        Vector3 desiredPosition = new Vector3(
            shipPosition.x,
            shipPosition.y,
            cameraPosition.z
        );

        float distance = Vector2.Distance(
            new Vector2(cameraPosition.x, cameraPosition.y),
            new Vector2(desiredPosition.x, desiredPosition.y)
        );

        if (distance <= cameraConfig.FollowDeadZone)
            return;

        Vector3 smoothed = Vector3.SmoothDamp(
            cameraPosition,
            desiredPosition,
            ref _cameraVelocity,
            cameraConfig.FollowSmoothTime
        );

        MoveCameraTo(smoothed);
    }

    private void UpdateReturnToShip()
    {
        Vector3 shipPosition = GetShipTargetPosition();
        Vector3 cameraPosition = targetCamera.transform.position;

        Vector3 desiredPosition = new Vector3(
            shipPosition.x,
            shipPosition.y,
            cameraPosition.z
        );

        Vector3 smoothedPosition = Vector3.SmoothDamp(
            cameraPosition,
            desiredPosition,
            ref _cameraVelocity,
            cameraConfig.ReturnSmoothTime
        );

        float targetZoom = GetDefaultOrthographicSizeForCurrentSystem();

        float smoothedZoom = Mathf.SmoothDamp(
            targetCamera.orthographicSize,
            targetZoom,
            ref _zoomVelocity,
            cameraConfig.ReturnSmoothTime
        );

        targetCamera.orthographicSize = Mathf.Clamp(
            smoothedZoom,
            cameraConfig.MinOrthographicSize,
            cameraConfig.MaxOrthographicSize
        );

        MoveCameraTo(smoothedPosition);

        float positionDistance = Vector2.Distance(
            new Vector2(targetCamera.transform.position.x, targetCamera.transform.position.y),
            new Vector2(desiredPosition.x, desiredPosition.y)
        );

        float zoomDistance = Mathf.Abs(targetCamera.orthographicSize - targetZoom);

        if (positionDistance <= cameraConfig.FollowDeadZone &&
            zoomDistance <= 0.5f)
        {
            targetCamera.orthographicSize = targetZoom;
            mode = SystemCameraMode2A.FollowShip;
            _cameraVelocity = Vector3.zero;
            _zoomVelocity = 0f;
        }
    }

    private float GetDefaultOrthographicSizeForCurrentSystem()
    {
        if (_defaultOrthographicSizeForCurrentSystem > 0f)
            return _defaultOrthographicSizeForCurrentSystem;

        if (cameraConfig == null)
            return 1200f;

        return Mathf.Clamp(
            GetDefaultOrthographicSizeForCurrentSystem(),
            cameraConfig.MinOrthographicSize,
            cameraConfig.MaxOrthographicSize
        );
    }

    private void MoveCameraTo(Vector3 position)
    {
        Vector3 clampedPosition = ClampCameraPosition(position);
        targetCamera.transform.position = clampedPosition;
    }

    private Vector3 ClampCameraPosition(Vector3 position)
    {
        if (!_hasSystemBounds)
            return position;

        float verticalHalfSize = targetCamera.orthographicSize;
        float horizontalHalfSize = verticalHalfSize * targetCamera.aspect;

        float minX = _currentSystemBounds.min.x + horizontalHalfSize;
        float maxX = _currentSystemBounds.max.x - horizontalHalfSize;

        float minY = _currentSystemBounds.min.y + verticalHalfSize;
        float maxY = _currentSystemBounds.max.y - verticalHalfSize;

        Vector3 result = position;

        if (minX > maxX)
            result.x = _currentSystemBounds.center.x;
        else
            result.x = Mathf.Clamp(result.x, minX, maxX);

        if (minY > maxY)
            result.y = _currentSystemBounds.center.y;
        else
            result.y = Mathf.Clamp(result.y, minY, maxY);

        return result;
    }

    private Vector3 GetShipTargetPosition()
    {
        if (shipTarget != null)
            return shipTarget.position;

        if (_systemTravelService != null && _systemTravelService.State != null)
            return _systemTravelService.State.GetCurrentPosition();

        return Vector3.zero;
    }

    private float GetWorldUnitsPerScreenPixel()
    {
        if (targetCamera == null)
            return 1f;

        if (Screen.height <= 0)
            return 1f;

        return targetCamera.orthographicSize * 2f / Screen.height;
    }

    private void SubscribeToEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Subscribe<StarSystemEnteredEvent>(OnSystemEntered);
        _eventBus.Subscribe<GalaxyEnteredEvent>(OnGalaxyEntered);
        _eventBus.Subscribe<PlanetEnteredEvent>(OnPlanetEntered);
    }

    private void UnsubscribeFromEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Unsubscribe<StarSystemEnteredEvent>(OnSystemEntered);
        _eventBus.Unsubscribe<GalaxyEnteredEvent>(OnGalaxyEntered);
        _eventBus.Unsubscribe<PlanetEnteredEvent>(OnPlanetEntered);
    }

    private void OnSystemEntered(StarSystemEnteredEvent evt)
    {
        _isSystemCameraActive = true;

        RebuildSystemBounds();
        ApplyCameraSizeForCurrentSystem();

        Vector3 shipPosition = GetShipTargetPosition();

        targetCamera.transform.position = ClampCameraPosition(new Vector3(
            shipPosition.x,
            shipPosition.y,
            targetCamera.transform.position.z
        ));

        mode = SystemCameraMode2A.FollowShip;
        _cameraVelocity = Vector3.zero;
        _zoomVelocity = 0f;
    }

    private void OnGalaxyEntered(GalaxyEnteredEvent evt)
    {
        _isSystemCameraActive = false;
        _cameraVelocity = Vector3.zero;
    }

    private void OnPlanetEntered(PlanetEnteredEvent evt)
    {
        _isSystemCameraActive = false;
        _cameraVelocity = Vector3.zero;
    }

    private void ApplyCameraSizeForCurrentSystem()
    {
        if (targetCamera == null)
            return;

        float desiredSize = GetDefaultOrthographicSizeForCurrentSystem();

        StarSystemConfig currentSystem = GetCurrentSystemConfig();

        if (currentSystem != null)
        {
            float largestObjectRadius = GetLargestObjectRadius(currentSystem);
            desiredSize = Mathf.Max(desiredSize, largestObjectRadius * 4.5f);
        }

        targetCamera.orthographic = true;

        _defaultOrthographicSizeForCurrentSystem = Mathf.Clamp(
            desiredSize,
            cameraConfig.MinOrthographicSize,
            cameraConfig.MaxOrthographicSize
        );

        targetCamera.orthographicSize = _defaultOrthographicSizeForCurrentSystem;
    }

    private float GetLargestObjectRadius(StarSystemConfig systemConfig)
    {
        float largestRadius = 0f;

        if (systemConfig.Sun != null)
            largestRadius = Mathf.Max(largestRadius, systemConfig.Sun.VisualSize * 0.5f);

        if (systemConfig.PlanetRefs != null)
        {
            foreach (PlanetConfig planet in systemConfig.PlanetRefs)
            {
                if (planet == null || planet.PlanetOrbit == null)
                    continue;

                largestRadius = Mathf.Max(
                    largestRadius,
                    planet.PlanetOrbit.PlanetVisualSize * 0.5f
                );
            }
        }

        if (systemConfig.Station != null)
            largestRadius = Mathf.Max(largestRadius, systemConfig.Station.VisualSize * 0.5f);

        return largestRadius;
    }

    private void RebuildSystemBounds()
    {
        _hasSystemBounds = false;

        StarSystemConfig currentSystem = GetCurrentSystemConfig();

        if (currentSystem == null)
            return;

        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasAnyPoint = false;

        AddSunBounds(currentSystem, ref bounds, ref hasAnyPoint);
        AddPlanetBounds(currentSystem, ref bounds, ref hasAnyPoint);
        AddStationBounds(currentSystem, ref bounds, ref hasAnyPoint);
        AddRouteExitBounds(currentSystem, ref bounds, ref hasAnyPoint);
        AddShipBounds(ref bounds, ref hasAnyPoint);

        if (!hasAnyPoint)
            return;

        bounds.Expand(cameraConfig.BoundsPadding * 2f);

        _currentSystemBounds = bounds;
        _hasSystemBounds = true;
    }

    private StarSystemConfig GetCurrentSystemConfig()
    {
        if (_gameSessionService == null ||
            _gameSessionService.State == null ||
            _gameSessionService.State.Player == null)
        {
            return null;
        }

        string currentSystemId = _gameSessionService.State.Player.CurrentSystemId;

        if (string.IsNullOrWhiteSpace(currentSystemId))
            return null;

        if (_configService == null)
            return null;

        if (_configService.TryGetStarSystem(currentSystemId, out StarSystemConfig systemConfig))
            return systemConfig;

        return null;
    }

    private void AddSunBounds(
        StarSystemConfig systemConfig,
        ref Bounds bounds,
        ref bool hasAnyPoint
    )
    {
        if (systemConfig.Sun == null)
            return;

        Vector3 center = new Vector3(
            systemConfig.Sun.LocalOffset.x,
            systemConfig.Sun.LocalOffset.y,
            0f
        );

        float radius = systemConfig.Sun.VisualSize * 0.5f + cameraConfig.SunExtraPadding;

        EncapsulateCircle(ref bounds, ref hasAnyPoint, center, radius);
    }

    private void AddPlanetBounds(
        StarSystemConfig systemConfig,
        ref Bounds bounds,
        ref bool hasAnyPoint
    )
    {
        if (systemConfig.PlanetRefs == null)
            return;

        foreach (PlanetConfig planet in systemConfig.PlanetRefs)
        {
            if (planet == null || planet.PlanetOrbit == null)
                continue;

            PlanetOrbitConfig orbit = planet.PlanetOrbit;

            Vector3 center = orbit.OrbitCenterOffset;
            float radius =
                orbit.OrbitRadius +
                orbit.PlanetVisualSize * 0.5f +
                cameraConfig.PlanetExtraPadding;

            EncapsulateCircle(ref bounds, ref hasAnyPoint, center, radius);
        }
    }

    private void AddStationBounds(
        StarSystemConfig systemConfig,
        ref Bounds bounds,
        ref bool hasAnyPoint
    )
    {
        if (systemConfig.Station == null)
            return;

        Vector3 center = new Vector3(
            systemConfig.Station.LocalOffset.x,
            systemConfig.Station.LocalOffset.y,
            0f
        );

        float radius = systemConfig.Station.VisualSize * 0.5f + cameraConfig.StationExtraPadding;

        EncapsulateCircle(ref bounds, ref hasAnyPoint, center, radius);
    }

    private void AddRouteExitBounds(
        StarSystemConfig systemConfig,
        ref Bounds bounds,
        ref bool hasAnyPoint
    )
    {
        if (systemConfig.Routes == null)
            return;

        foreach (RouteConfig routeConfig in systemConfig.Routes)
        {
            if (routeConfig == null)
                continue;

            RouteEndpointConfig endpointConfig = routeConfig.GetEndPointForSystem(systemConfig.Id);

            if (endpointConfig == null)
                continue;

            EncapsulateCircle(
                ref bounds,
                ref hasAnyPoint,
                endpointConfig.ExitPoint,
                cameraConfig.ExitExtraPadding
            );

            EncapsulateCircle(
                ref bounds,
                ref hasAnyPoint,
                endpointConfig.EntryPoint,
                cameraConfig.ExitExtraPadding
            );
        }
    }

    private void AddShipBounds(ref Bounds bounds, ref bool hasAnyPoint)
    {
        Vector3 shipPosition = GetShipTargetPosition();

        EncapsulateCircle(
            ref bounds,
            ref hasAnyPoint,
            shipPosition,
            cameraConfig.PlanetExtraPadding
        );
    }

    private void EncapsulateCircle(
        ref Bounds bounds,
        ref bool hasAnyPoint,
        Vector3 center,
        float radius
    )
    {
        Vector3 min = new Vector3(center.x - radius, center.y - radius, 0f);
        Vector3 max = new Vector3(center.x + radius, center.y + radius, 0f);

        if (!hasAnyPoint)
        {
            bounds = new Bounds(center, Vector3.zero);
            bounds.Encapsulate(min);
            bounds.Encapsulate(max);
            hasAnyPoint = true;
            return;
        }

        bounds.Encapsulate(min);
        bounds.Encapsulate(max);
    }
}
