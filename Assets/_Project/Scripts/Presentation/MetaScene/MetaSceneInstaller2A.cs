using UnityEngine;

/// <summary>
/// Установщик системной части MetaScene.
///
/// Задача:
/// при старте сцены согласовать существующий визуальный корабль,
/// legacy SystemTravelService и новый ShipMovementRuntimeState.
///
/// Важно:
/// этот компонент не заменяет SystemShipMarkerController2
/// и не управляет перелётами.
/// </summary>
[DisallowMultipleComponent]
public sealed class MetaSceneInstaller2A : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private ShipMarkerView2 shipMarkerView;
    [SerializeField] private SpriteRenderer shipMarkerImage;

    [Header("Initialization")]
    [SerializeField] private bool initializeOnStart = true;
    [SerializeField] private bool useSavedPlayerPosition = true;
    [SerializeField] private bool pushPositionToSystemTravelService = true;
    [SerializeField] private bool pushPositionToShipMovementState = true;

    [Header("Movement Ownership")]
    [Tooltip(
        "В S3-16 legacy SystemShipMarkerController2 остаётся владельцем визуального движения. " +
        "Поэтому новый ShipMovementService лучше выключить, чтобы он не двигал state параллельно.")]
    [SerializeField] private bool disableShipMovementServiceTick = true;

    [Header("Debug")]
    [SerializeField] private bool logInitialization = true;

    private IGameSessionService _gameSessionService;
    private ISystemTravelService _systemTravelService;
    private IShipMovementService _shipMovementService;

    private bool _isInitialized;

    private void Reset()
    {
        ResolveSceneReferences();
    }

    private void Awake()
    {
        ResolveSceneReferences();
        ResolveServices();
    }

    private void Start()
    {
        if (initializeOnStart)
        {
            InitializeScene();
        }
    }

    public void InitializeScene()
    {
        ResolveSceneReferences();
        ResolveServices();

        Vector3 spawnPosition =
            ResolveInitialPosition();

        Vector2 spawnDirection =
            ResolveInitialDirection();

        if (pushPositionToSystemTravelService
            && _systemTravelService != null)
        {
            _systemTravelService.SetCurrentPosition(
                spawnPosition);
        }

        if (shipMarkerView != null)
        {
            shipMarkerView.SetPosition(
                spawnPosition);
        }

        if (shipMarkerImage != null)
        {
            shipMarkerImage.transform.localRotation =
                MetaSceneShipTransformUtility2A
                    .RotationFromDirection(spawnDirection);
        }

        if (_shipMovementService != null)
        {
            if (disableShipMovementServiceTick)
            {
                _shipMovementService.SetEnabled(false);
            }

            if (pushPositionToShipMovementState)
            {
                _shipMovementService.SetPosition(
                    MetaSceneShipTransformUtility2A
                        .ToVector2(spawnPosition));

                _shipMovementService.SetFacingDirection(
                    spawnDirection);
            }
        }

        _isInitialized = true;

        if (logInitialization)
        {
            Debug.Log(
                "[MetaSceneInstaller2A] Initialized. "
                + "Position = " + spawnPosition
                + " | Direction = " + spawnDirection,
                this);
        }
    }

    public bool HasRequiredSceneReferences()
    {
        return spawnPoint != null
            && shipMarkerView != null
            && shipMarkerImage != null;
    }

    public bool IsInitialized()
    {
        return _isInitialized;
    }

    private Vector3 ResolveInitialPosition()
    {
        if (useSavedPlayerPosition
            && _gameSessionService != null
            && _gameSessionService.HasActiveSession
            && _gameSessionService.State != null
            && _gameSessionService.State.Player != null)
        {
            Vector3 savedPosition =
                _gameSessionService
                    .State
                    .Player
                    .SystemMapShipPosition;

            if (MetaSceneShipTransformUtility2A
                    .IsFinite(savedPosition))
            {
                return savedPosition;
            }
        }

        if (_systemTravelService != null
            && _systemTravelService.State != null)
        {
            Vector3 travelPosition =
                _systemTravelService
                    .State
                    .GetCurrentPosition();

            if (MetaSceneShipTransformUtility2A
                    .IsFinite(travelPosition))
            {
                return travelPosition;
            }
        }

        if (spawnPoint != null)
        {
            return spawnPoint.position;
        }

        if (shipMarkerView != null)
        {
            return shipMarkerView.transform.position;
        }

        return Vector3.zero;
    }

    private Vector2 ResolveInitialDirection()
    {
        if (useSavedPlayerPosition
            && _gameSessionService != null
            && _gameSessionService.HasActiveSession
            && _gameSessionService.State != null
            && _gameSessionService.State.Player != null)
        {
            Vector3 savedDirection =
                _gameSessionService
                    .State
                    .Player
                    .SystemMapShipDirection;

            Vector2 savedDirection2D =
                new Vector2(
                    savedDirection.x,
                    savedDirection.y);

            if (MetaSceneShipTransformUtility2A
                    .IsFinite(savedDirection2D)
                && savedDirection2D.sqrMagnitude > 0.0001f)
            {
                return savedDirection2D.normalized;
            }
        }

        if (shipMarkerImage != null)
        {
            return MetaSceneShipTransformUtility2A
                .DirectionFromRotation(
                    shipMarkerImage.transform.localRotation);
        }

        return Vector2.up;
    }

    private void ResolveSceneReferences()
    {
        if (shipMarkerView == null)
        {
            shipMarkerView =
                GetComponentInChildren<ShipMarkerView2>(
                    true);
        }

        if (shipMarkerImage == null
            && shipMarkerView != null)
        {
            shipMarkerImage =
                shipMarkerView.GetComponentInChildren<SpriteRenderer>(
                    true);
        }

        if (spawnPoint == null)
        {
            Transform foundSpawn =
                transform.Find("PlayerRoot/ShipSpawnPoint");

            if (foundSpawn != null)
            {
                spawnPoint = foundSpawn;
            }
        }
    }

    private void ResolveServices()
    {
        TryResolveService(out _gameSessionService);
        TryResolveService(out _systemTravelService);
        TryResolveService(out _shipMovementService);
    }

    private static bool TryResolveService<TService>(
        out TService service)
        where TService : class
    {
        service = null;

        if (Bootstrapper.Instance == null
            || Bootstrapper.Instance.ServiceRegistry == null)
        {
            return false;
        }

        try
        {
            service =
                Bootstrapper.Instance
                    .ServiceRegistry
                    .Get<TService>();

            return service != null;
        }
        catch
        {
            service = null;
            return false;
        }
    }
}