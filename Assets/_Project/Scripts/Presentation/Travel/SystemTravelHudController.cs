using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Управляет нижним HUD сцены звёздной системы.
///
/// Отображает:
/// - игровой день;
/// - режим игрового времени;
/// - текущую цель;
/// - статус корабля;
/// - текущую скорость корабля.
///
/// Контроллер:
/// - не создаёт gameplay-сервисы;
/// - не рассчитывает движение корабля;
/// - не изменяет Movement State;
/// - получает сервисы через ServiceRegistry;
/// - очищает кнопочные и событийные подписки
///   при отключении или уничтожении.
/// </summary>
[DisallowMultipleComponent]
public sealed class SystemTravelHudController :
    MonoBehaviour
{
    private const string NoTargetText =
        "не определена";

    private const string IdleStatusText =
        "в покое";

    private const string FlyingStatusText =
        "полет";

    private const string UnknownStatusText =
        "—";

    private const float SpeedEpsilon =
        0.0001f;

    [Header("Texts")]

    [SerializeField]
    private TMP_Text dayText;

    [SerializeField]
    private TMP_Text modeText;

    [SerializeField]
    private TMP_Text targetText;

    [SerializeField]
    private TMP_Text statusText;

    [Header("Buttons")]

    [SerializeField]
    private Button playPauseButton;

    [SerializeField]
    private Button stepDayButton;

    [Header("Button Labels")]

    [SerializeField]
    private TMP_Text playPauseButtonText;

    [SerializeField]
    private TMP_Text stepDayButtonText;

    [Header("Ship Speed")]

    [Tooltip(
        "Частота обновления скорости в statusText. " +
        "Значение 0.1 означает примерно десять " +
        "обновлений в секунду.")]
    [SerializeField]
    [Min(0.02f)]
    private float speedRefreshIntervalSeconds =
        0.1f;

    [Header("Diagnostics")]

    [SerializeField]
    private float displayedShipSpeed;

    [SerializeField]
    private bool usesMovementRuntimeState;

    [SerializeField]
    private bool usesLegacyTravelSpeed;

    private IGameTimeService
        _gameTimeService;

    private ISystemTravelService
        _travelService;

    private IConfigService
        _configService;

    private SimpleEventBus
        _eventBus;

    /*
     * Новый movement service.
     *
     * Используется как основной источник скорости,
     * когда именно он является активным владельцем
     * движения корабля.
     */
    private IShipMovementService
        _shipMovementService;

    /*
     * Legacy travel service получает фактическую
     * скорость перелёта из активного корабля.
     *
     * Поэтому IHangarService используется как
     * fallback, пока новым movement service
     * движение не управляется.
     */
    private IHangarService
        _hangarService;

    private string _currentTargetLabel =
        NoTargetText;

    private string _currentStatusLabel =
        string.Empty;

    private string _lastRenderedStatusText =
        string.Empty;

    private float _nextSpeedRefreshTime;

    private bool _isInitialized;

    private bool _buttonsSubscribed;

    private bool _eventsSubscribed;

    private void Start()
    {
        ResolveServices();

        _isInitialized = true;

        SubscribeButtons();
        SubscribeEvents();

        SetNoTargetIdle();
        RefreshAll();

        RefreshStatusTextWithSpeed(
            force: true);
    }

    private void OnEnable()
    {
        /*
         * OnEnable вызывается раньше Start.
         * Поэтому до первоначальной инициализации
         * подписываться ещё не на что.
         */
        if (!_isInitialized)
            return;

        SubscribeButtons();
        SubscribeEvents();

        RefreshAll();

        RefreshStatusTextWithSpeed(
            force: true);
    }

    private void Update()
    {
        if (!_isInitialized)
            return;

        RefreshTime();
        RefreshTravelState();

        if (Time.unscaledTime <
            _nextSpeedRefreshTime)
        {
            return;
        }

        _nextSpeedRefreshTime =
            Time.unscaledTime +
            Mathf.Max(
                0.02f,
                speedRefreshIntervalSeconds);

        RefreshStatusTextWithSpeed();
    }

    private void OnDisable()
    {
        UnsubscribeButtons();
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeButtons();
        UnsubscribeEvents();

        _isInitialized = false;
    }

    private void ResolveServices()
    {
        if (Bootstrapper.Instance == null)
        {
            Debug.LogError(
                "[SystemTravelHudController] " +
                "Bootstrapper.Instance is null.",
                this);

            return;
        }

        if (Bootstrapper.Instance
                .ServiceRegistry == null)
        {
            Debug.LogError(
                "[SystemTravelHudController] " +
                "ServiceRegistry is null.",
                this);

            return;
        }

        IServiceRegistry registry =
            Bootstrapper.Instance
                .ServiceRegistry;

        _gameTimeService =
            registry.Get<IGameTimeService>();

        _travelService =
            registry.Get<ISystemTravelService>();

        _configService =
            registry.Get<IConfigService>();

        _eventBus =
            registry.Get<SimpleEventBus>();

        /*
         * Эти сервисы получаем через TryGet,
         * чтобы HUD мог продолжить работу
         * в legacy-конфигурации сцены.
         */
        registry.TryGet<IShipMovementService>(
            out _shipMovementService);

        registry.TryGet<IHangarService>(
            out _hangarService);

        if (_gameTimeService == null)
        {
            Debug.LogError(
                "[SystemTravelHudController] " +
                "IGameTimeService not found.",
                this);
        }

        if (_travelService == null)
        {
            Debug.LogError(
                "[SystemTravelHudController] " +
                "ISystemTravelService not found.",
                this);
        }

        if (_configService == null)
        {
            Debug.LogError(
                "[SystemTravelHudController] " +
                "IConfigService not found.",
                this);
        }

        if (_eventBus == null)
        {
            Debug.LogError(
                "[SystemTravelHudController] " +
                "SimpleEventBus not found.",
                this);
        }

        if (_shipMovementService == null)
        {
            Debug.LogWarning(
                "[SystemTravelHudController] " +
                "IShipMovementService not found. " +
                "Legacy travel speed will be used.",
                this);
        }

        if (_hangarService == null)
        {
            Debug.LogWarning(
                "[SystemTravelHudController] " +
                "IHangarService not found. " +
                "Legacy travel speed is unavailable.",
                this);
        }
    }

    private void SubscribeButtons()
    {
        if (_buttonsSubscribed)
            return;

        if (playPauseButton != null)
        {
            playPauseButton
                .onClick
                .AddListener(
                    OnPlayPauseClicked);
        }

        if (stepDayButton != null)
        {
            stepDayButton
                .onClick
                .AddListener(
                    OnStepDayClicked);
        }

        _buttonsSubscribed = true;
    }

    private void UnsubscribeButtons()
    {
        if (!_buttonsSubscribed)
            return;

        if (playPauseButton != null)
        {
            playPauseButton
                .onClick
                .RemoveListener(
                    OnPlayPauseClicked);
        }

        if (stepDayButton != null)
        {
            stepDayButton
                .onClick
                .RemoveListener(
                    OnStepDayClicked);
        }

        _buttonsSubscribed = false;
    }

    private void SubscribeEvents()
    {
        if (_eventsSubscribed ||
            _eventBus == null)
        {
            return;
        }

        _eventBus.Subscribe<
            DestinationSelectedEvent>(
                OnDestinationSelected);

        _eventBus.Subscribe<
            SystemTravelStartedEvent>(
                OnTravelStarted);

        _eventBus.Subscribe<
            SystemTravelCompletedEvent>(
                OnTravelCompleted);

        _eventBus.Subscribe<
            SystemTravelCancelledEvent>(
                OnTravelCancelled);

        /*
         * Если будет добавлено production-событие
         * выбора врага, его подписка должна быть
         * добавлена здесь.
         */

        _eventsSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!_eventsSubscribed ||
            _eventBus == null)
        {
            return;
        }

        _eventBus.Unsubscribe<
            DestinationSelectedEvent>(
                OnDestinationSelected);

        _eventBus.Unsubscribe<
            SystemTravelStartedEvent>(
                OnTravelStarted);

        _eventBus.Unsubscribe<
            SystemTravelCompletedEvent>(
                OnTravelCompleted);

        _eventBus.Unsubscribe<
            SystemTravelCancelledEvent>(
                OnTravelCancelled);

        _eventsSubscribed = false;
    }

    private void OnPlayPauseClicked()
    {
        if (_gameTimeService == null)
            return;

        _gameTimeService.TogglePause();

        RefreshTime();

        RefreshStatusTextWithSpeed(
            force: true);
    }

    private void OnStepDayClicked()
    {
        if (_gameTimeService == null)
            return;

        _gameTimeService.TogglePause();

        StartCoroutine(
            Delay(
                _gameTimeService.DelayTime));

        RefreshTime();

        RefreshStatusTextWithSpeed(
            force: true);
    }

    private IEnumerator Delay(
        float delay)
    {
        yield return new WaitForSeconds(
            delay);

        if (_gameTimeService != null)
        {
            _gameTimeService.TogglePause();

            RefreshTime();

            RefreshStatusTextWithSpeed(
                force: true);
        }
    }

    private void OnDestinationSelected(
        DestinationSelectedEvent evt)
    {
        _currentTargetLabel =
            BuildTargetLabel(evt);

        SetTargetText(
            _currentTargetLabel);

        SetStatusText(
            FlyingStatusText);
    }

    private void OnTravelStarted(
        SystemTravelStartedEvent evt)
    {
        if (_currentTargetLabel ==
            NoTargetText)
        {
            _currentTargetLabel =
                BuildTargetLabelFromState();
        }

        SetTargetText(
            _currentTargetLabel);

        SetStatusText(
            FlyingStatusText);
    }

    private void OnTravelCompleted(
        SystemTravelCompletedEvent evt)
    {
        SetNoTargetIdle();
    }

    private void OnTravelCancelled(
        SystemTravelCancelledEvent evt)
    {
        SetNoTargetIdle();
    }

    private void RefreshAll()
    {
        RefreshTime();
        RefreshTravelState();

        RefreshStatusTextWithSpeed(
            force: true);
    }

    private void RefreshTime()
    {
        if (_gameTimeService == null)
            return;

        SetTextSafe(
            dayText,
            "День " +
            _gameTimeService
                .CurrentQuantTick);

        if (_gameTimeService.IsPaused)
        {
            SetTextSafe(
                modeText,
                "Пауза");

            SetTextSafe(
                playPauseButtonText,
                "Старт");
        }
        else
        {
            SetTextSafe(
                modeText,
                "Игра");

            SetTextSafe(
                playPauseButtonText,
                "Пауза");
        }

        SetTextSafe(
            stepDayButtonText,
            "Шаг");
    }

    private void RefreshTravelState()
    {
        if (_travelService == null)
        {
            SetNoTargetIdle();
            return;
        }

        SystemTravelState state =
            _travelService.State;

        if (state == null)
        {
            SetNoTargetIdle();
            return;
        }

        bool isIdle =
            !state.HasDestination &&
            state.Status !=
                SystemTravelStatus.Flying &&
            state.Status !=
                SystemTravelStatus
                    .DestinationSelected;

        if (isIdle)
        {
            SetNoTargetIdle();
            return;
        }

        bool hasActiveTravel =
            state.HasDestination ||
            state.Status ==
                SystemTravelStatus.Flying ||
            state.Status ==
                SystemTravelStatus
                    .DestinationSelected;

        if (!hasActiveTravel)
            return;

        if (_currentTargetLabel ==
            NoTargetText)
        {
            _currentTargetLabel =
                BuildTargetLabelFromState();
        }

        SetTargetText(
            _currentTargetLabel);

        SetStatusText(
            FlyingStatusText);
    }

    private string BuildTargetLabel(
     DestinationSelectedEvent evt)
    {
        switch (evt.DestinationType)
        {
            case TravelDestinationType.Planet:
                return
                    "планета " +
                    GetPlanetDisplayName(
                        evt.PlanetId);

            case TravelDestinationType.MapPoint:
                return "космос";

            case TravelDestinationType.SystemExit:
                return
                    "система " +
                    GetSystemDisplayName(
                        evt.TargetSystemId);

            default:
                return NoTargetText;
        }
    }

    private string BuildTargetLabelFromState()
    {
        if (_travelService == null)
            return NoTargetText;

        SystemTravelState state =
            _travelService.State;

        if (state == null ||
            state.Destination == null)
        {
            return NoTargetText;
        }

        switch (state.Destination.Type)
        {
            case TravelDestinationType.Planet:
                return
                    "планета " +
                    GetPlanetDisplayName(
                        state.Destination
                            .PlanetId);

            case TravelDestinationType.MapPoint:
                return "космос";

            case TravelDestinationType.SystemExit:
                return
                    "система " +
                    GetSystemDisplayName(
                        state.Destination
                            .TargetSystemId);

            default:
                return NoTargetText;
        }
    }

    private string GetPlanetDisplayName(
        string planetId)
    {
        if (string.IsNullOrWhiteSpace(
                planetId))
        {
            return "неизвестная";
        }

        PlanetConfig planetConfig =
            FindPlanetConfig(
                planetId);

        if (planetConfig == null)
            return planetId;

        if (!string.IsNullOrWhiteSpace(
                planetConfig.DisplayName))
        {
            return planetConfig.DisplayName;
        }

        return planetConfig.Id;
    }

    private string GetSystemDisplayName(
        string systemId)
    {
        if (string.IsNullOrWhiteSpace(
                systemId))
        {
            return "неизвестная";
        }

        StarSystemConfig systemConfig =
            FindStarSystemConfig(
                systemId);

        if (systemConfig == null)
            return systemId;

        if (!string.IsNullOrWhiteSpace(
                systemConfig.DisplayName))
        {
            return systemConfig.DisplayName;
        }

        return systemConfig.Id;
    }

    private PlanetConfig FindPlanetConfig(
        string planetId)
    {
        if (_configService == null)
            return null;

        IReadOnlyList<StarSystemConfig>
            systems =
                _configService
                    .GetAllStarSystems();

        if (systems == null)
            return null;

        foreach (
            StarSystemConfig systemConfig
            in systems)
        {
            if (systemConfig == null)
                continue;

            if (systemConfig.PlanetRefs ==
                null)
            {
                continue;
            }

            foreach (
                PlanetConfig planetConfig
                in systemConfig.PlanetRefs)
            {
                if (planetConfig == null)
                    continue;

                if (planetConfig.Id ==
                    planetId)
                {
                    return planetConfig;
                }
            }
        }

        return null;
    }

    private StarSystemConfig
        FindStarSystemConfig(
            string systemId)
    {
        if (_configService == null)
            return null;

        IReadOnlyList<StarSystemConfig>
            systems =
                _configService
                    .GetAllStarSystems();

        if (systems == null)
            return null;

        foreach (
            StarSystemConfig systemConfig
            in systems)
        {
            if (systemConfig == null)
                continue;

            if (systemConfig.Id ==
                systemId)
            {
                return systemConfig;
            }
        }

        return null;
    }

    /// <summary>
    /// Временная публичная точка для существующей
    /// логики выбора врага.
    ///
    /// Не создаёт Target State и не изменяет
    /// gameplay-сервисы.
    /// </summary>
    public void SetEnemyTarget(
        string enemyName)
    {
        if (string.IsNullOrWhiteSpace(
                enemyName))
        {
            enemyName =
                "неизвестный";
        }

        _currentTargetLabel =
            "враг " +
            enemyName;

        SetTargetText(
            _currentTargetLabel);

        SetStatusText(
            FlyingStatusText);
    }

    private void SetNoTargetIdle()
    {
        _currentTargetLabel =
            NoTargetText;

        SetTargetText(
            NoTargetText);

        SetStatusText(
            IdleStatusText);
    }

    private void SetTargetText(
        string value)
    {
        SetTextSafe(
            targetText,
            "Цель: " +
            value);
    }

    /// <summary>
    /// Сохраняет существующее значение статуса.
    /// Финальная строка вместе со скоростью
    /// строится только в RefreshStatusTextWithSpeed.
    /// </summary>
    private void SetStatusText(
        string value)
    {
        string normalizedValue =
            string.IsNullOrWhiteSpace(
                value)
                ? UnknownStatusText
                : value;

        if (_currentStatusLabel ==
            normalizedValue)
        {
            return;
        }

        _currentStatusLabel =
            normalizedValue;

        RefreshStatusTextWithSpeed(
            force: true);
    }

    /// <summary>
    /// Формирует одну общую строку:
    ///
    /// Статус: полет • Скорость: 3.2
    ///
    /// Метод не изменяет gameplay State.
    /// </summary>
    private void RefreshStatusTextWithSpeed(
        bool force = false)
    {
        if (statusText == null)
            return;

        float currentSpeed =
            GetCurrentShipSpeed();

        displayedShipSpeed =
            currentSpeed;

        string statusLabel =
            string.IsNullOrWhiteSpace(
                _currentStatusLabel)
                ? UnknownStatusText
                : _currentStatusLabel;

        string nextText =
            "Статус: " +
            statusLabel +
            "  •  Скорость: " +
            currentSpeed.ToString(
                "0.0");

        if (!force &&
            nextText ==
            _lastRenderedStatusText)
        {
            return;
        }

        statusText.text =
            nextText;

        _lastRenderedStatusText =
            nextText;
    }

    /// <summary>
    /// Возвращает скорость, которая должна
    /// отображаться игроку.
    ///
    /// Приоритет:
    /// 1. активный ShipMovementRuntimeState;
    /// 2. фактическая legacy travel speed;
    /// 3. ноль.
    /// </summary>
    private float GetCurrentShipSpeed()
    {
        usesMovementRuntimeState =
            false;

        usesLegacyTravelSpeed =
            false;

        /*
         * При общей игровой паузе корабль
         * фактически не перемещается.
         */
        if (_gameTimeService != null &&
            _gameTimeService.IsPaused)
        {
            return 0f;
        }

        /*
         * Новый movement service считается
         * источником только тогда, когда он включён.
         *
         * Это исключает чтение stale velocity,
         * когда legacy travel остаётся владельцем
         * движения.
         */
        if (_shipMovementService != null &&
            _shipMovementService.IsEnabled &&
            _shipMovementService.State != null)
        {
            float movementSpeed =
                SanitizeSpeed(
                    _shipMovementService
                        .State
                        .CurrentSpeed);

            usesMovementRuntimeState =
                true;

            return movementSpeed;
        }

        /*
         * Fallback для текущей legacy-системы
         * перелёта.
         */
        float legacySpeed =
            GetLegacyTravelSpeed();

        if (legacySpeed >
            SpeedEpsilon)
        {
            usesLegacyTravelSpeed =
                true;
        }

        return legacySpeed;
    }

    /// <summary>
    /// Читает ту же скорость активного корабля,
    /// которую legacy SystemTravelService
    /// использует во время полёта.
    ///
    /// HUD не вычисляет движение и не меняет
    /// полученные данные.
    /// </summary>
    private float GetLegacyTravelSpeed()
    {
        if (_travelService == null ||
            _travelService.State == null)
        {
            return 0f;
        }

        if (_travelService.State.Status !=
            SystemTravelStatus.Flying)
        {
            return 0f;
        }

        if (_hangarService == null)
            return 0f;

        var activeShipStats =
            _hangarService
                .GetActiveShipStats();

        if (activeShipStats == null)
            return 0f;

        return SanitizeSpeed(
            activeShipStats.Speed);
    }

    private static float SanitizeSpeed(
        float value)
    {
        if (float.IsNaN(value) ||
            float.IsInfinity(value))
        {
            return 0f;
        }

        return Mathf.Max(
            0f,
            value);
    }

    private static void SetTextSafe(
        TMP_Text target,
        string value)
    {
        if (target != null)
        {
            target.text =
                value;
        }
    }
}