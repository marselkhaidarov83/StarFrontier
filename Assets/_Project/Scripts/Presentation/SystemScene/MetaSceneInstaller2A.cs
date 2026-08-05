using System.Collections;
using UnityEngine;

/// <summary>
/// Установщик системной части MetaScene.
///
/// При старте сцены восстанавливает:
/// - позицию корабля;
/// - направление корабля;
/// - позицию legacy SystemTravelService;
/// - состояние ShipMovementRuntimeState.
///
/// Компонент не управляет перелётом.
/// Владельцем визуального движения остаётся
/// SystemShipMarkerController2.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class MetaSceneInstaller2A : CustomMonoBehaviour
{
    [Header("Scene References")]

    [SerializeField]
    private Transform spawnPoint;

    [SerializeField]
    private ShipMarkerView2 shipMarkerView;

    [SerializeField]
    private SpriteRenderer shipMarkerImage;

    [Header("Initialization")]

    [SerializeField]
    private bool initializeOnStart = true;

    [SerializeField]
    private bool useSavedPlayerPosition = true;

    [SerializeField]
    private bool pushPositionToSystemTravelService = true;

    [SerializeField]
    private bool pushPositionToShipMovementState = true;

    [Tooltip(
        "Максимальное количество кадров ожидания " +
        "готовности GameSessionService и PlayerState.")]
    [SerializeField]
    [Min(1)]
    private int sessionReadyTimeoutFrames = 120;

    [Header("Movement Ownership")]

    [Tooltip(
        "Legacy SystemShipMarkerController2 остаётся " +
        "владельцем визуального движения. " +
        "ShipMovementService не должен менять позицию параллельно.")]
    [SerializeField]
    private bool disableShipMovementServiceTick = true;

    [Header("Debug")]

    [SerializeField]
    private bool logInitialization = true;

    [SerializeField]
    private bool logSessionWait = true;

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

    /// <summary>
    /// Сначала ждёт готовности активной игровой сессии.
    /// Только затем читает Save и инициализирует сцену.
    /// </summary>
    private IEnumerator Start()
    {
        if (!initializeOnStart)
            yield break;

        ResolveSceneReferences();

        int timeout =
            Mathf.Max(
                1,
                sessionReadyTimeoutFrames);

        int waitedFrames = 0;

        while (useSavedPlayerPosition &&
               !IsGameSessionReady() &&
               waitedFrames < timeout)
        {
            ResolveServices();

            waitedFrames++;

            yield return null;
        }

        ResolveServices();

        if (logSessionWait)
        {
            LogCustom(
                "[MetaSceneInstaller2A] " +
                $"Session wait finished after {waitedFrames} frames. " +
                $"Session ready: {IsGameSessionReady()}.");
        }

        InitializeScene();
    }

    /// <summary>
    /// Восстанавливает позицию и направление,
    /// затем синхронизирует связанные сервисы.
    /// </summary>
    public void InitializeScene()
    {
        ResolveSceneReferences();
        ResolveServices();

        Vector3 initialPosition =
            ResolveInitialPosition();

        Vector2 initialDirection =
            ResolveInitialDirection();

        initialDirection =
            MetaSceneShipTransformUtility2A
                .NormalizeDirectionOrUp(
                    initialDirection);

        /*
         * Сначала устанавливаем позицию
         * в legacy travel-сервис.
         */
        if (pushPositionToSystemTravelService &&
            _systemTravelService != null)
        {
            _systemTravelService.SetCurrentPosition(
                initialPosition);
        }

        /*
         * Затем устанавливаем визуальную позицию.
         */
        if (shipMarkerView != null)
        {
            shipMarkerView.SetPosition(
                initialPosition);
        }

        /*
         * После позиции устанавливаем сохранённое
         * визуальное направление.
         */
        ApplyVisualDirection(
            initialDirection);

        /*
         * Синхронизируем Movement Runtime State.
         */
        if (_shipMovementService != null)
        {
            if (disableShipMovementServiceTick)
            {
                _shipMovementService.SetEnabled(
                    false);
            }

            if (pushPositionToShipMovementState)
            {
                _shipMovementService.SetPosition(
                    MetaSceneShipTransformUtility2A
                        .ToVector2(
                            initialPosition));

                _shipMovementService.SetFacingDirection(
                    initialDirection);
            }
        }

        /*
         * Записываем нормализованное направление
         * обратно в активный PlayerState.
         *
         * Это не выполняет сохранение на диск,
         * а только согласует runtime-состояние.
         */
        if (IsGameSessionReady())
        {
            _gameSessionService
                .State
                .Player
                .SystemMapShipDirection =
                    new Vector3(
                        initialDirection.x,
                        initialDirection.y,
                        0f);
        }

        _isInitialized = true;

        if (logInitialization)
        {
            LogCustom(
                "[MetaSceneInstaller2A] Initialized. " +
                $"Position = {initialPosition} | " +
                $"Direction = {initialDirection} | " +
                $"Rotation Z = " +
                $"{GetCurrentVisualRotationZ()}");
        }
    }

    public bool HasRequiredSceneReferences()
    {
        return
            spawnPoint != null &&
            shipMarkerView != null &&
            shipMarkerImage != null;
    }

    public bool IsInitialized()
    {
        return _isInitialized;
    }

    /// <summary>
    /// Определяет стартовую позицию.
    ///
    /// Приоритет:
    /// 1. активный PlayerState;
    /// 2. SystemTravelService;
    /// 3. spawn point;
    /// 4. текущая позиция визуального корабля;
    /// 5. Vector3.zero.
    /// </summary>
    private Vector3 ResolveInitialPosition()
    {
        if (useSavedPlayerPosition &&
            IsGameSessionReady())
        {
            Vector3 savedPosition =
                _gameSessionService
                    .State
                    .Player
                    .SystemMapShipPosition;

            if (MetaSceneShipTransformUtility2A
                    .IsFinite(
                        savedPosition))
            {
                return savedPosition;
            }
        }

        if (_systemTravelService != null &&
            _systemTravelService.State != null)
        {
            Vector3 travelPosition =
                _systemTravelService
                    .State
                    .GetCurrentPosition();

            if (MetaSceneShipTransformUtility2A
                    .IsFinite(
                        travelPosition))
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
            return shipMarkerView
                .transform
                .position;
        }

        return Vector3.zero;
    }

    /// <summary>
    /// Определяет стартовое направление.
    ///
    /// Приоритет:
    /// 1. сохранённое направление PlayerState;
    /// 2. направление ShipMovementRuntimeState;
    /// 3. строго Vector2.up.
    ///
    /// Текущий поворот SpriteRenderer намеренно
    /// не используется как источник.
    /// </summary>
    private Vector2 ResolveInitialDirection()
    {
        if (useSavedPlayerPosition &&
            IsGameSessionReady())
        {
            Vector3 savedDirection3D =
                _gameSessionService
                    .State
                    .Player
                    .SystemMapShipDirection;

            Vector2 savedDirection =
                new Vector2(
                    savedDirection3D.x,
                    savedDirection3D.y);

            if (IsValidDirection(
                    savedDirection))
            {
                return savedDirection.normalized;
            }
        }

        if (_shipMovementService != null &&
            _shipMovementService.State != null)
        {
            Vector2 runtimeDirection =
                _shipMovementService
                    .State
                    .FacingDirection;

            if (IsValidDirection(
                    runtimeDirection))
            {
                return runtimeDirection.normalized;
            }
        }

        /*
         * Детерминированный fallback.
         *
         * Больше не читаем localRotation картинки,
         * потому что её могут изменять несколько
         * legacy-компонентов во время запуска сцены.
         */
        return Vector2.up;
    }

    private void ApplyVisualDirection(
        Vector2 direction)
    {
        if (shipMarkerImage == null)
            return;

        Vector2 normalizedDirection =
            MetaSceneShipTransformUtility2A
                .NormalizeDirectionOrUp(
                    direction);

        shipMarkerImage
            .transform
            .localRotation =
                MetaSceneShipTransformUtility2A
                    .RotationFromDirection(
                        normalizedDirection);
    }

    private bool IsGameSessionReady()
    {
        return
            _gameSessionService != null &&
            _gameSessionService.HasActiveSession &&
            _gameSessionService.State != null &&
            _gameSessionService.State.Player != null;
    }

    private static bool IsValidDirection(
        Vector2 direction)
    {
        return
            MetaSceneShipTransformUtility2A
                .IsFinite(
                    direction) &&
            direction.sqrMagnitude >
            0.0001f;
    }

    private float GetCurrentVisualRotationZ()
    {
        if (shipMarkerImage == null)
            return 0f;

        return shipMarkerImage
            .transform
            .localEulerAngles
            .z;
    }

    private void ResolveSceneReferences()
    {
        if (shipMarkerView == null)
        {
            shipMarkerView =
                GetComponentInChildren<ShipMarkerView2>(
                    true);
        }

        if (shipMarkerImage == null &&
            shipMarkerView != null)
        {
            shipMarkerImage =
                shipMarkerView
                    .GetComponentInChildren<SpriteRenderer>(
                        true);
        }

        if (spawnPoint == null)
        {
            Transform foundSpawn =
                transform.Find(
                    "PlayerRoot/ShipSpawnPoint");

            if (foundSpawn != null)
            {
                spawnPoint =
                    foundSpawn;
            }
        }
    }

    private void ResolveServices()
    {
        TryResolveService(
            out _gameSessionService);

        TryResolveService(
            out _systemTravelService);

        TryResolveService(
            out _shipMovementService);
    }

    private static bool TryResolveService<TService>(
        out TService service)
        where TService : class
    {
        service = null;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
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