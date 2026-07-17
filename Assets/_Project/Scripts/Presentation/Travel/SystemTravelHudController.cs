using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SystemTravelHudController : MonoBehaviour
{
    private const string NoTargetText = "не определена";
    private const string IdleStatusText = "в покое";
    private const string FlyingStatusText = "полет";

    [Header("Texts")]
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text modeText;
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private TMP_Text statusText;

    [Header("Buttons")]
    [SerializeField] private Button playPauseButton;
    [SerializeField] private Button stepDayButton;

    [Header("Button Labels")]
    [SerializeField] private TMP_Text playPauseButtonText;
    [SerializeField] private TMP_Text stepDayButtonText;

    private IGameTimeService _gameTimeService;
    private ISystemTravelService _travelService;
    private IConfigService _configService;
    private SimpleEventBus _eventBus;

    private string _currentTargetLabel = NoTargetText;

    private void Start()
    {
        _gameTimeService = Bootstrapper.Instance.ServiceRegistry.Get<IGameTimeService>();
        _travelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        if (_gameTimeService == null)
            Debug.LogError("[SystemTravelHudController] IGameTimeService not found.");

        if (_travelService == null)
            Debug.LogError("[SystemTravelHudController] ISystemTravelService not found.");

        if (_configService == null)
            Debug.LogError("[SystemTravelHudController] IConfigService not found.");

        if (_eventBus == null)
            Debug.LogError("[SystemTravelHudController] SimpleEventBus not found.");

        SubscribeButtons();
        SubscribeEvents();

        SetNoTargetIdle();
        RefreshAll();
    }

    private void Update()
    {
        RefreshTime();
        RefreshTravelState();
    }

    private void OnDestroy()
    {
        UnsubscribeButtons();
        UnsubscribeEvents();
    }

    private void SubscribeButtons()
    {
        if (playPauseButton != null)
            playPauseButton.onClick.AddListener(OnPlayPauseClicked);

        if (stepDayButton != null)
            stepDayButton.onClick.AddListener(OnStepDayClicked);
    }

    private void UnsubscribeButtons()
    {
        if (playPauseButton != null)
            playPauseButton.onClick.RemoveListener(OnPlayPauseClicked);

        if (stepDayButton != null)
            stepDayButton.onClick.RemoveListener(OnStepDayClicked);
    }

    private void SubscribeEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Subscribe<DestinationSelectedEvent>(OnDestinationSelected);
        _eventBus.Subscribe<SystemTravelStartedEvent>(OnTravelStarted);
        _eventBus.Subscribe<SystemTravelCompletedEvent>(OnTravelCompleted);
        _eventBus.Subscribe<SystemTravelCancelledEvent>(OnTravelCancelled);

        // Если уже есть событие выбора врага, подключим его на шаге 3.
        // _eventBus.Subscribe(OnEnemyTargetSelected);
    }

    private void UnsubscribeEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Unsubscribe<DestinationSelectedEvent>(OnDestinationSelected);
        _eventBus.Unsubscribe<SystemTravelStartedEvent>(OnTravelStarted);
        _eventBus.Unsubscribe<SystemTravelCompletedEvent>(OnTravelCompleted);
        _eventBus.Unsubscribe<SystemTravelCancelledEvent>(OnTravelCancelled);

        // Если уже есть событие выбора врага, подключим его на шаге 3.
        // _eventBus.Unsubscribe(OnEnemyTargetSelected);
    }

    private void OnPlayPauseClicked()
    {
        if (_gameTimeService == null)
            return;

        _gameTimeService.TogglePause();
        RefreshTime();
    }

    private void OnStepDayClicked()
    {
        if (_gameTimeService == null)
            return;

        _gameTimeService.TogglePause();
        StartCoroutine(Delay(_gameTimeService.DelayTime));
        RefreshTime();
    }

    private IEnumerator Delay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (_gameTimeService != null)
            _gameTimeService.TogglePause();
    }

    private void OnDestinationSelected(DestinationSelectedEvent evt)
    {
        _currentTargetLabel = BuildTargetLabel(evt);

        SetTargetText(_currentTargetLabel);
        SetStatusText(FlyingStatusText);
    }

    private void OnTravelStarted(SystemTravelStartedEvent evt)
    {
        if (_currentTargetLabel == NoTargetText)
            _currentTargetLabel = BuildTargetLabelFromState();

        SetTargetText(_currentTargetLabel);
        SetStatusText(FlyingStatusText);
    }

    private void OnTravelCompleted(SystemTravelCompletedEvent evt)
    {
        SetNoTargetIdle();
    }

    private void OnTravelCancelled(SystemTravelCancelledEvent evt)
    {
        SetNoTargetIdle();
    }

    private void RefreshAll()
    {
        RefreshTime();
        RefreshTravelState();
    }

    private void RefreshTime()
    {
        if (_gameTimeService == null)
            return;

        SetTextSafe(dayText, $"Квант {_gameTimeService.CurrentQuantTick}");

        if (_gameTimeService.IsPaused)
        {
            SetTextSafe(modeText, "Пауза");
            SetTextSafe(playPauseButtonText, "Старт");
        }
        else
        {
            SetTextSafe(modeText, "Игра");
            SetTextSafe(playPauseButtonText, "Пауза");
        }

        SetTextSafe(stepDayButtonText, "Шаг");
    }

    private void RefreshTravelState()
    {
        if (_travelService == null)
        {
            SetNoTargetIdle();
            return;
        }

        SystemTravelState state = _travelService.State;

        if (state == null)
        {
            SetNoTargetIdle();
            return;
        }

        if (!state.HasDestination &&
            state.Status != SystemTravelStatus.Flying &&
            state.Status != SystemTravelStatus.DestinationSelected)
        {
            SetNoTargetIdle();
            return;
        }

        if (state.HasDestination ||
            state.Status == SystemTravelStatus.Flying ||
            state.Status == SystemTravelStatus.DestinationSelected)
        {
            if (_currentTargetLabel == NoTargetText)
                _currentTargetLabel = BuildTargetLabelFromState();

            SetTargetText(_currentTargetLabel);
            SetStatusText(FlyingStatusText);
        }
    }

    private string BuildTargetLabel(DestinationSelectedEvent evt)
    {
        switch (evt.DestinationType)
        {
            case TravelDestinationType.Planet:
                return "планета " + GetPlanetDisplayName(evt.PlanetId);

            case TravelDestinationType.MapPoint:
                return "космос";

            case TravelDestinationType.SystemExit:
                return "система " + GetSystemDisplayName(evt.TargetSystemId);

            default:
                return NoTargetText;
        }
    }

    private string BuildTargetLabelFromState()
    {
        if (_travelService == null)
            return NoTargetText;

        SystemTravelState state = _travelService.State;

        if (state == null || state.Destination == null)
            return NoTargetText;

        switch (state.Destination.Type)
        {
            case TravelDestinationType.Planet:
                return "планета " + GetPlanetDisplayName(state.Destination.PlanetId);

            case TravelDestinationType.MapPoint:
                return "космос";

            case TravelDestinationType.SystemExit:
                return "система " + GetSystemDisplayName(state.Destination.TargetSystemId);

            default:
                return NoTargetText;
        }
    }

    private string GetPlanetDisplayName(string planetId)
    {
        if (string.IsNullOrWhiteSpace(planetId))
            return "неизвестная";

        PlanetConfig planetConfig = FindPlanetConfig(planetId);

        if (planetConfig == null)
            return planetId;

        if (!string.IsNullOrWhiteSpace(planetConfig.DisplayName))
            return planetConfig.DisplayName;

        return planetConfig.Id;
    }

    private string GetSystemDisplayName(string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId))
            return "неизвестная";

        StarSystemConfig systemConfig = FindStarSystemConfig(systemId);

        if (systemConfig == null)
            return systemId;

        if (!string.IsNullOrWhiteSpace(systemConfig.DisplayName))
            return systemConfig.DisplayName;

        return systemConfig.Id;
    }

    private PlanetConfig FindPlanetConfig(string planetId)
    {
        if (_configService == null)
            return null;

        IReadOnlyList<StarSystemConfig> systems = _configService.GetAllStarSystems();

        if (systems == null)
            return null;

        foreach (StarSystemConfig systemConfig in systems)
        {
            if (systemConfig == null)
                continue;

            if (systemConfig.PlanetRefs == null)
                continue;

            foreach (PlanetConfig planetConfig in systemConfig.PlanetRefs)
            {
                if (planetConfig == null)
                    continue;

                if (planetConfig.Id == planetId)
                    return planetConfig;
            }
        }

        return null;
    }

    private StarSystemConfig FindStarSystemConfig(string systemId)
    {
        if (_configService == null)
            return null;

        IReadOnlyList<StarSystemConfig> systems = _configService.GetAllStarSystems();

        if (systems == null)
            return null;

        foreach (StarSystemConfig systemConfig in systems)
        {
            if (systemConfig == null)
                continue;

            if (systemConfig.Id == systemId)
                return systemConfig;
        }

        return null;
    }

    public void SetEnemyTarget(string enemyName)
    {
        if (string.IsNullOrWhiteSpace(enemyName))
            enemyName = "неизвестный";

        _currentTargetLabel = "враг " + enemyName;

        SetTargetText(_currentTargetLabel);
        SetStatusText(FlyingStatusText);
    }

    private void SetNoTargetIdle()
    {
        _currentTargetLabel = NoTargetText;

        SetTargetText(NoTargetText);
        SetStatusText(IdleStatusText);
    }

    private void SetTargetText(string value)
    {
        SetTextSafe(targetText, $"Цель: {value}");
    }

    private void SetStatusText(string value)
    {
        SetTextSafe(statusText, $"Статус: {value}");
    }

    private void SetTextSafe(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value;
    }
}