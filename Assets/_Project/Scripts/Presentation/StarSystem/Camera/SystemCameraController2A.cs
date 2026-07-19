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
    [SerializeField]
    private SystemCameraMode2A mode =
        SystemCameraMode2A.FollowShip;

    private SimpleEventBus _eventBus;
    private IGameSessionService _gameSessionService;
    private IConfigService _configService;
    private ISystemTravelService _systemTravelService;

    private MapCameraController _mapCameraController;

    private bool _isInitialized;
    private bool _isSystemCameraActive;
    private bool _isSubscribedToEvents;

    private Vector3 _cameraVelocity;
    private float _zoomVelocity;

    private float
        _defaultOrthographicSizeForCurrentSystem;

    public SystemCameraMode2A Mode => mode;

    public bool IsSystemCameraActive =>
        _isSystemCameraActive;

    /*
     * Свойство сохранено для совместимости с другим кодом.
     * Фактическое значение берётся из MapCameraController.
     */
    public float MaxOrthographicSizeWithoutEmptySpace
    {
        get
        {
            ResolveMapCameraController();

            if (_mapCameraController != null)
            {
                return _mapCameraController
                    .MaxOrthographicSizeWithoutEmptySpace;
            }

            if (cameraConfig != null)
                return cameraConfig.MaxOrthographicSize;

            return 2000f;
        }
    }

    public void Initialize()
    {
        Debug.Log(
            "[SystemCameraController2A] Initialize started"
        );

        if (_isInitialized)
        {
            Debug.Log(
                "[SystemCameraController2A] Already initialized"
            );

            return;
        }

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
        {
            Debug.LogError(
                "[SystemCameraController2A] " +
                "Target Camera is null and Camera.main not found"
            );

            return;
        }

        if (cameraConfig == null)
        {
            Debug.LogError(
                "[SystemCameraController2A] " +
                "Camera Config is not assigned"
            );

            return;
        }

        if (Bootstrapper.Instance == null)
        {
            Debug.LogError(
                "[SystemCameraController2A] " +
                "Bootstrapper.Instance is null"
            );

            return;
        }

        if (Bootstrapper.Instance.ServiceRegistry == null)
        {
            Debug.LogError(
                "[SystemCameraController2A] " +
                "ServiceRegistry is null"
            );

            return;
        }

        Bootstrapper.Instance.ServiceRegistry
            .TryGet<SimpleEventBus>(
                out _eventBus
            );

        Bootstrapper.Instance.ServiceRegistry
            .TryGet<IGameSessionService>(
                out _gameSessionService
            );

        Bootstrapper.Instance.ServiceRegistry
            .TryGet<IConfigService>(
                out _configService
            );

        Bootstrapper.Instance.ServiceRegistry
            .TryGet<ISystemTravelService>(
                out _systemTravelService
            );

        if (_eventBus == null)
        {
            Debug.LogError(
                "[SystemCameraController2A] " +
                "SimpleEventBus not found"
            );

            return;
        }

        if (_gameSessionService == null)
        {
            Debug.LogWarning(
                "[SystemCameraController2A] " +
                "IGameSessionService not found"
            );
        }

        if (_configService == null)
        {
            Debug.LogWarning(
                "[SystemCameraController2A] " +
                "IConfigService not found"
            );
        }

        if (_systemTravelService == null)
        {
            Debug.LogWarning(
                "[SystemCameraController2A] " +
                "ISystemTravelService not found"
            );
        }

        ResolveMapCameraController();

        SubscribeToEvents();
        ActivateSystemCameraSafely();

        _isInitialized = true;

        Debug.Log(
            "[SystemCameraController2A] " +
            "Initialize finished safely"
        );
    }

    private void ResolveMapCameraController()
    {
        if (_mapCameraController != null)
            return;

        if (targetCamera == null)
            return;

        _mapCameraController =
            targetCamera.GetComponent<MapCameraController>();
    }

    private void ActivateSystemCameraSafely()
    {
        Debug.Log(
            "[SystemCameraController2A] " +
            "ActivateSystemCameraSafely started"
        );

        _isSystemCameraActive = true;

        if (targetCamera == null)
        {
            Debug.LogError(
                "[SystemCameraController2A] " +
                "Cannot activate: targetCamera is null"
            );

            return;
        }

        ResolveMapCameraController();

        ApplyCameraSizeForCurrentSystem();

        Vector3 shipPosition =
            GetShipTargetPosition();

        Vector3 initialCameraPosition =
            new Vector3(
                shipPosition.x,
                shipPosition.y,
                targetCamera.transform.position.z
            );

        /*
         * Корабль центрируется только настолько,
         * насколько позволяют заданные размеры карты.
         */
        MoveCameraTo(initialCameraPosition);

        mode = SystemCameraMode2A.FollowShip;

        _cameraVelocity = Vector3.zero;
        _zoomVelocity = 0f;

        Debug.Log(
            "[SystemCameraController2A] " +
            "ActivateSystemCameraSafely finished. " +
            "ShipPosition = " +
            shipPosition
        );
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
                /*
                 * При изменении зума в FreeLook камера
                 * также должна оставаться внутри карты.
                 */
                ReclampCurrentPosition();
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
        if (targetCamera == null)
        {
            Debug.LogError(
                "[SystemCameraController2A] " +
                "ReturnToShip failed: targetCamera is null"
            );

            return;
        }

        if (!_isSystemCameraActive)
        {
            Debug.LogWarning(
                "[SystemCameraController2A] " +
                "ReturnToShip called while camera is inactive. " +
                "Reactivating."
            );

            ActivateSystemCameraSafely();
        }

        ResolveMapCameraController();

        mode = SystemCameraMode2A.ReturningToShip;

        _cameraVelocity = Vector3.zero;
        _zoomVelocity = 0f;

        float targetZoom =
            GetDefaultOrthographicSizeForCurrentSystem();

        if (_mapCameraController != null)
        {
            _mapCameraController.SetTargetZoom(
                targetZoom
            );
        }
    }

    public void NotifyManualZoomStarted()
    {
        if (!_isSystemCameraActive)
            return;

        if (mode != SystemCameraMode2A.ReturningToShip)
            return;

        /*
         * Ручной зум получает приоритет.
         * Камера продолжает следовать за кораблём.
         */
        mode = SystemCameraMode2A.FollowShip;
        _zoomVelocity = 0f;
    }

    public void MoveFreeLookByScreenDelta(
        Vector2 screenDelta
    )
    {
        if (!_isSystemCameraActive)
            return;

        if (targetCamera == null)
            return;

        EnterFreeLook();

        float unitsPerPixel =
            GetWorldUnitsPerScreenPixel();

        Vector3 worldDelta = new Vector3(
            screenDelta.x * unitsPerPixel,
            screenDelta.y * unitsPerPixel,
            0f
        );

        Vector3 currentPosition =
            targetCamera.transform.position;

        Vector3 nextPosition =
            currentPosition -
            worldDelta * cameraConfig.DragSensitivity;

        MoveCameraTo(nextPosition);
    }

    public void ReclampCurrentPosition()
    {
        if (!_isSystemCameraActive)
            return;

        if (targetCamera == null)
            return;

        MoveCameraTo(
            targetCamera.transform.position
        );
    }

    private void UpdateFollowShip()
    {
        Vector3 cameraPosition =
            targetCamera.transform.position;

        Vector3 shipPosition =
            GetShipTargetPosition();

        Vector3 desiredPosition =
            new Vector3(
                shipPosition.x,
                shipPosition.y,
                cameraPosition.z
            );

        /*
         * Сначала ограничиваем желаемую позицию.
         * Если корабль находится у края, камера не пытается
         * постоянно ехать в недостижимую точку за границей.
         */
        Vector3 clampedDesiredPosition =
            ClampCameraPosition(desiredPosition);

        float distance = Vector2.Distance(
            new Vector2(
                cameraPosition.x,
                cameraPosition.y
            ),
            new Vector2(
                clampedDesiredPosition.x,
                clampedDesiredPosition.y
            )
        );

        if (distance <= cameraConfig.FollowDeadZone)
        {
            _cameraVelocity = Vector3.zero;

            /*
             * Даже стоящую камеру повторно ограничиваем:
             * это необходимо после изменения зума.
             */
            MoveCameraTo(cameraPosition);
            return;
        }

        Vector3 smoothedPosition =
            Vector3.SmoothDamp(
                cameraPosition,
                clampedDesiredPosition,
                ref _cameraVelocity,
                cameraConfig.FollowSmoothTime
            );

        MoveCameraTo(smoothedPosition);
    }

    private void UpdateReturnToShip()
    {
        Vector3 cameraPosition =
            targetCamera.transform.position;

        Vector3 shipPosition =
            GetShipTargetPosition();

        Vector3 desiredPosition =
            new Vector3(
                shipPosition.x,
                shipPosition.y,
                cameraPosition.z
            );

        Vector3 clampedDesiredPosition =
            ClampCameraPosition(desiredPosition);

        Vector3 smoothedPosition =
            Vector3.SmoothDamp(
                cameraPosition,
                clampedDesiredPosition,
                ref _cameraVelocity,
                cameraConfig.ReturnSmoothTime
            );

        MoveCameraTo(smoothedPosition);

        bool zoomReached;

        if (_mapCameraController != null)
        {
            /*
             * MapCameraController является владельцем зума.
             */
            zoomReached =
                _mapCameraController.IsZoomAtTarget(0.5f);
        }
        else
        {
            /*
             * Запасной режим на случай отсутствия
             * MapCameraController на объекте камеры.
             */
            float targetZoom =
                GetDefaultOrthographicSizeForCurrentSystem();

            float smoothedZoom =
                Mathf.SmoothDamp(
                    targetCamera.orthographicSize,
                    targetZoom,
                    ref _zoomVelocity,
                    cameraConfig.ReturnSmoothTime
                );

            targetCamera.orthographicSize =
                ClampOrthographicSize(smoothedZoom);

            zoomReached =
                Mathf.Abs(
                    targetCamera.orthographicSize -
                    targetZoom
                ) <= 0.5f;
        }

        /*
         * Позицию не проверяем, потому что корабль
         * может продолжать движение.
         */
        if (zoomReached)
        {
            mode = SystemCameraMode2A.FollowShip;
            _zoomVelocity = 0f;
        }
    }

    private float GetDefaultOrthographicSizeForCurrentSystem()
    {
        if (_defaultOrthographicSizeForCurrentSystem > 0f)
        {
            return ClampOrthographicSize(
                _defaultOrthographicSizeForCurrentSystem
            );
        }

        if (cameraConfig == null)
        {
            return Mathf.Min(
                1200f,
                MaxOrthographicSizeWithoutEmptySpace
            );
        }

        return ClampOrthographicSize(
            cameraConfig.DefaultOrthographicSize
        );
    }

    private float ClampOrthographicSize(float value)
    {
        float maximum =
            MaxOrthographicSizeWithoutEmptySpace;

        if (cameraConfig == null)
        {
            return Mathf.Clamp(
                value,
                0.01f,
                maximum
            );
        }

        float minimum =
            Mathf.Min(
                cameraConfig.MinOrthographicSize,
                maximum
            );

        return Mathf.Clamp(
            value,
            minimum,
            maximum
        );
    }

    private void SetZoomImmediate(float value)
    {
        float clampedValue =
            ClampOrthographicSize(value);

        ResolveMapCameraController();

        if (_mapCameraController != null)
        {
            _mapCameraController.SetZoomImmediate(
                clampedValue
            );

            return;
        }

        if (targetCamera != null)
        {
            targetCamera.orthographicSize =
                clampedValue;
        }
    }

    private void MoveCameraTo(Vector3 position)
    {
        Vector3 clampedPosition =
            ClampCameraPosition(position);

        targetCamera.transform.position =
            clampedPosition;
    }

    private Vector3 ClampCameraPosition(
        Vector3 position
    )
    {
        ResolveMapCameraController();

        if (_mapCameraController == null ||
            targetCamera == null)
        {
            return position;
        }

        return _mapCameraController.ClampPositionToMap(
            position,
            targetCamera.orthographicSize
        );
    }

    private Vector3 GetShipTargetPosition()
    {
        if (shipTarget != null)
            return shipTarget.position;

        if (_systemTravelService != null &&
            _systemTravelService.State != null)
        {
            return _systemTravelService.State
                .GetCurrentPosition();
        }

        return Vector3.zero;
    }

    private float GetWorldUnitsPerScreenPixel()
    {
        if (targetCamera == null)
            return 1f;

        if (Screen.height <= 0)
            return 1f;

        return
            targetCamera.orthographicSize *
            2f /
            Screen.height;
    }

    private void SubscribeToEvents()
    {
        if (_isSubscribedToEvents)
        {
            Debug.LogWarning(
                "[SystemCameraController2A] " +
                "SubscribeToEvents skipped: already subscribed"
            );

            return;
        }

        if (_eventBus == null)
        {
            Debug.LogError(
                "[SystemCameraController2A] " +
                "Cannot subscribe: eventBus is null"
            );

            return;
        }

        _eventBus.Subscribe<GalaxyEnteredEvent>(
            OnGalaxyEntered
        );

        _eventBus.Subscribe<PlanetEnteredEvent>(
            OnPlanetEntered
        );

        _eventBus.Subscribe<StarSystemEnteredEvent>(
            OnSystemEntered
        );

        _isSubscribedToEvents = true;
    }

    private void UnsubscribeFromEvents()
    {
        if (!_isSubscribedToEvents)
            return;

        if (_eventBus == null)
            return;

        _eventBus.Unsubscribe<GalaxyEnteredEvent>(
            OnGalaxyEntered
        );

        _eventBus.Unsubscribe<PlanetEnteredEvent>(
            OnPlanetEntered
        );

        _eventBus.Unsubscribe<StarSystemEnteredEvent>(
            OnSystemEntered
        );

        _isSubscribedToEvents = false;
    }

    private void OnSystemEntered(
        StarSystemEnteredEvent evt
    )
    {
        ActivateSystemCameraSafely();
    }

    private void OnGalaxyEntered(
        GalaxyEnteredEvent evt
    )
    {
        _isSystemCameraActive = false;

        _cameraVelocity = Vector3.zero;
        _zoomVelocity = 0f;
    }

    private void OnPlanetEntered(
        PlanetEnteredEvent evt
    )
    {
        _isSystemCameraActive = false;
        mode = SystemCameraMode2A.FreeLook;

        _cameraVelocity = Vector3.zero;
        _zoomVelocity = 0f;
    }

    private void ApplyCameraSizeForCurrentSystem()
    {
        if (targetCamera == null)
            return;

        if (cameraConfig == null)
            return;

        float desiredSize =
            cameraConfig.DefaultOrthographicSize;

        StarSystemConfig currentSystem =
            GetCurrentSystemConfig();

        if (currentSystem != null)
        {
            float largestObjectRadius =
                GetLargestObjectRadius(
                    currentSystem
                );

            desiredSize = Mathf.Max(
                desiredSize,
                largestObjectRadius * 4.5f
            );
        }

        targetCamera.orthographic = true;

        _defaultOrthographicSizeForCurrentSystem =
            ClampOrthographicSize(desiredSize);

        SetZoomImmediate(
            _defaultOrthographicSizeForCurrentSystem
        );
    }

    private float GetLargestObjectRadius(
        StarSystemConfig systemConfig
    )
    {
        float largestRadius = 0f;

        if (systemConfig.Sun != null)
        {
            largestRadius = Mathf.Max(
                largestRadius,
                systemConfig.Sun.VisualSize * 0.5f
            );
        }

        if (systemConfig.PlanetRefs != null)
        {
            foreach (
                PlanetConfig planet
                in systemConfig.PlanetRefs
            )
            {
                if (planet == null ||
                    planet.PlanetOrbit == null)
                {
                    continue;
                }

                largestRadius = Mathf.Max(
                    largestRadius,
                    planet.PlanetOrbit
                        .PlanetVisualSize * 0.5f
                );
            }
        }

        if (systemConfig.Station != null)
        {
            largestRadius = Mathf.Max(
                largestRadius,
                systemConfig.Station.VisualSize * 0.5f
            );
        }

        return largestRadius;
    }

    private StarSystemConfig GetCurrentSystemConfig()
    {
        if (_gameSessionService == null ||
            _gameSessionService.State == null ||
            _gameSessionService.State.Player == null)
        {
            return null;
        }

        string currentSystemId =
            _gameSessionService.State.Player
                .CurrentSystemId;

        if (string.IsNullOrWhiteSpace(
                currentSystemId
            ))
        {
            return null;
        }

        if (_configService == null)
            return null;

        if (_configService.TryGetStarSystem(
                currentSystemId,
                out StarSystemConfig systemConfig
            ))
        {
            return systemConfig;
        }

        return null;
    }
}