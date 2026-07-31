using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GalaxySystemInfoPanel2A : CustomMonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text planetsText;
    [SerializeField] private TMP_Text baseText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text targetSystemText;
    [SerializeField] private TMP_Text routeSystemCountText;
    [SerializeField] private TMP_Text routePriceText;

    [Header("Button")]
    [SerializeField] private Button flyButton;

    [Header("Route Price Colors")]
    [SerializeField] private Color routePriceNormalColor = Color.white;
    [SerializeField] private Color routePriceNotEnoughFuelColor = Color.red;

    private IConfigService _configService;
    private ITravelService _travelService;
    private IGameSessionService _gameSessionService;
    private SimpleEventBus _eventBus;
    private IGameStateMachine _gameStateMachine;
    private ISystemTravelService _systemTravelService;

    private string _targetSystemId;
    private string _nextSystemId;
    private List<string> _currentPath = new();

    private bool _isInitialized;

    public void Initialize()
    {
        if (_isInitialized)
            return;

        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _travelService = Bootstrapper.Instance.ServiceRegistry.Get<ITravelService>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _gameStateMachine = Bootstrapper.Instance.ServiceRegistry.Get<IGameStateMachine>();
        _systemTravelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();

        SubscribeToEvents();
        SetupFlyButton();

        Hide();

        _isInitialized = true;
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Subscribe<GalaxyMapSystemSelectedEvent>(OnGalaxyMapSystemSelected);
        _eventBus.Subscribe<GalaxyMapSelectionClearedEvent>(OnGalaxyMapSelectionCleared);
    }

    private void UnsubscribeFromEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Unsubscribe<GalaxyMapSystemSelectedEvent>(OnGalaxyMapSystemSelected);
        _eventBus.Unsubscribe<GalaxyMapSelectionClearedEvent>(OnGalaxyMapSelectionCleared);
    }

    private void SetupFlyButton()
    {
        if (flyButton == null)
            return;

        flyButton.onClick.RemoveAllListeners();
        flyButton.onClick.AddListener(OnFlyButtonClicked2);
    }

    private void OnGalaxyMapSystemSelected(GalaxyMapSystemSelectedEvent eventData)
    {
        StarSystemConfig targetSystemConfig =
            FindSystemConfig(eventData.TargetSystemId);

        StarSystemRuntimeState targetSystemState =
            FindSystemRuntimeState(eventData.TargetSystemId);

        ShowSelectedSystem(
            targetSystemConfig,
            targetSystemState,
            eventData.CurrentSystemId,
            eventData.Path,
            eventData.NextSystemId,
            eventData.TravelFailReason
        );
    }

    private void OnGalaxyMapSelectionCleared(GalaxyMapSelectionClearedEvent eventData)
    {
        Hide();
    }

    private void ShowSelectedSystem(
        StarSystemConfig targetSystemConfig,
        StarSystemRuntimeState targetSystemState,
        string currentSystemId,
        List<string> path,
        string nextSystemId,
        TravelFailReason travelFailReason)
    {
        if (targetSystemConfig == null)
        {
            Hide();
            return;
        }

        _targetSystemId = targetSystemConfig.Id;
        _nextSystemId = nextSystemId;
        _currentPath = path != null
            ? new List<string>(path)
            : new List<string>();

        SetPlanetsText(targetSystemConfig);
        SetBaseText(targetSystemConfig);
        SetStatusText(targetSystemConfig, targetSystemState, currentSystemId);
        SetTargetSystemText(targetSystemConfig);
        SetRouteSystemCountText(_currentPath);

        SetRoutePriceText(
            currentSystemId,
            _currentPath,
            _nextSystemId,
            travelFailReason
        );

        RefreshFlyButton(
            currentSystemId,
            travelFailReason
        );
    }

    public void Hide()
    {
        _targetSystemId = string.Empty;
        _nextSystemId = string.Empty;
        _currentPath.Clear();

        SetPlanetsText(null);
        SetBaseText(null);
        SetStatusText(null, null, null);
        SetTargetSystemText(null);
        SetRouteSystemCountText(null);
        SetRoutePriceText(null, null, null, TravelFailReason.None);

        if (routePriceText != null)
            routePriceText.color = routePriceNormalColor;

        if (flyButton != null)
            flyButton.interactable = false;
    }

    private void ClearText(TMP_Text text)
    {
        if (text != null)
            text.text = string.Empty;
    }

    private void SetPlanetsText(StarSystemConfig targetSystemConfig)
    {
        string planetCount = "";

        if (targetSystemConfig != null && targetSystemConfig.PlanetRefs != null)
            planetCount = targetSystemConfig.PlanetRefs.Length.ToString();

        if (planetsText != null)
            planetsText.text = "Планеты: " + planetCount;
    }

    private void SetBaseText(StarSystemConfig targetSystemConfig)
    {
        if (targetSystemConfig == null)
        {
            baseText.text = "База:";
            return;
        }

        string text = "База: нет данных";

        if (targetSystemConfig != null &&
            targetSystemConfig.PlanetRefs != null &&
            targetSystemConfig.PlanetRefs.Length > 0)
        {
            bool hasInhabitedPlanet = targetSystemConfig.PlanetRefs.Any(
                planet => planet != null && planet.IsInhabited
            );

            text = hasInhabitedPlanet
                ? "База: можно построить"
                : "База: нет подходящей планеты";
        }

        if (baseText != null)
            baseText.text = text;
    }

    private void SetStatusText(
        StarSystemConfig targetSystemConfig,
        StarSystemRuntimeState targetSystemState,
        string currentSystemId)
    {
        string text;

        if (targetSystemConfig == null)
            text = "Статус:";
        else if (targetSystemState == null)
            text = "Статус: нет данных";
        else if (!targetSystemState.IsDiscovered)
            text = "Статус: скрыта";
        else if (targetSystemConfig.Id == currentSystemId)
            text = "Статус: текущая система";
        else if (targetSystemState.IsVisited)
            text = "Статус: посещена";
        else
            text = "Статус: открыта";

        if (statusText != null)
            statusText.text = text;
    }

    private void SetTargetSystemText(StarSystemConfig targetSystemConfig)
    {
        if (targetSystemConfig == null)
        {
            targetSystemText.text = "Цель: не определена";
            return;
        }

        if (targetSystemText != null)
            targetSystemText.text = "Цель: " + targetSystemConfig.DisplayName;
    }

    private void SetRouteSystemCountText(List<string> path)
    {
        string jumpCount = "";

        if (path != null && path.Count >= 2)
            jumpCount = (path.Count - 1).ToString();

        if (routeSystemCountText != null)
            routeSystemCountText.text = "Прыжков: " + jumpCount;
    }

    private void SetRoutePriceText(
        string currentSystemId,
        List<string> path,
        string nextSystemId,
        TravelFailReason travelFailReason)
    {
        if (path == null)
        {
            routePriceText.text = "Стоимость:";
            return;
        }

        int totalPrice = CalculateTotalRoutePrice(path);
        int firstJumpPrice = CalculateFirstJumpPrice(
            currentSystemId,
            nextSystemId
        );

        if (routePriceText == null)
            return;

        routePriceText.text =
            "Стоимость: всего " +
            totalPrice +
            " / первый прыжок " +
            firstJumpPrice;

        routePriceText.color = travelFailReason == TravelFailReason.NotEnoughFuel
            ? routePriceNotEnoughFuelColor
            : routePriceNormalColor;
    }

    private int CalculateFirstJumpPrice(
        string currentSystemId,
        string nextSystemId)
    {
        if (string.IsNullOrWhiteSpace(currentSystemId))
            return 0;

        if (string.IsNullOrWhiteSpace(nextSystemId))
            return 0;

        return GetRoutePrice(currentSystemId, nextSystemId);
    }

    private int CalculateTotalRoutePrice(List<string> path)
    {
        if (path == null || path.Count < 2)
            return 0;

        int total = 0;

        for (int i = 0; i < path.Count - 1; i++)
        {
            total += GetRoutePrice(
                path[i],
                path[i + 1]
            );
        }

        return total;
    }

    private int GetRoutePrice(
        string fromSystemId,
        string toSystemId)
    {
        RouteConfig routeConfig = FindRouteConfig(
            fromSystemId,
            toSystemId
        );

        if (routeConfig == null)
            return 0;

        if (routeConfig.ParsecDistance <= 0)
            return 1;

        return routeConfig.ParsecDistance;
    }

    private StarSystemConfig FindSystemConfig(string systemId)
    {
        if (_configService == null)
            return null;

        IReadOnlyList<StarSystemConfig> systems =
            _configService.GetAllStarSystems();

        if (systems == null)
            return null;

        return systems.FirstOrDefault(
            system => system != null && system.Id == systemId
        );
    }

    private StarSystemRuntimeState FindSystemRuntimeState(string systemId)
    {
        if (_gameSessionService == null)
            return null;

        if (_gameSessionService.State == null)
            return null;

        if (_gameSessionService.State.Galaxy == null)
            return null;

        if (_gameSessionService.State.Galaxy.Systems == null)
            return null;

        return _gameSessionService.State.Galaxy.Systems.FirstOrDefault(
            system => system.SystemId == systemId
        );
    }

    private RouteConfig FindRouteConfig(
        string fromSystemId,
        string toSystemId)
    {
        if (_configService == null)
            return null;

        IReadOnlyList<StarSystemConfig> systems =
            _configService.GetAllStarSystems();

        if (systems == null)
            return null;

        foreach (StarSystemConfig systemConfig in systems)
        {
            if (systemConfig == null || systemConfig.Routes == null)
                continue;

            foreach (RouteConfig routeConfig in systemConfig.Routes)
            {
                if (routeConfig == null)
                    continue;

                if (IsRouteBetweenSystems(
                        routeConfig,
                        fromSystemId,
                        toSystemId))
                {
                    return routeConfig;
                }
            }
        }

        return null;
    }

    private bool IsRouteBetweenSystems(
        RouteConfig routeConfig,
        string firstSystemId,
        string secondSystemId)
    {
        if (routeConfig == null)
            return false;

        if (routeConfig.FromSystem == null)
            return false;

        if (routeConfig.ToSystem == null)
            return false;

        string routeFromSystemId = routeConfig.FromSystem.Id;
        string routeToSystemId = routeConfig.ToSystem.Id;

        bool direct =
            routeFromSystemId == firstSystemId &&
            routeToSystemId == secondSystemId;

        bool reverse =
            routeFromSystemId == secondSystemId &&
            routeToSystemId == firstSystemId;

        return direct || reverse;
    }

    private void RefreshFlyButton(
        string currentSystemId,
        TravelFailReason travelFailReason)
    {
        if (flyButton == null)
            return;

        bool hasNextSystem =
            !string.IsNullOrWhiteSpace(_nextSystemId);

        bool isCurrentSystem =
            _targetSystemId == currentSystemId;

        bool canTravel =
            travelFailReason == TravelFailReason.None;

        flyButton.interactable =
            hasNextSystem &&
            !isCurrentSystem &&
            canTravel;
    }

    private void OnFlyButtonClicked()
    {
        if (string.IsNullOrWhiteSpace(_nextSystemId))
        {
            LogCustom("Нет следующей системы для перелёта.");
            return;
        }

        string currentSystemId = GetCurrentSystemId();
        TravelFailReason result = _travelService.GetTravelFailReason(
            currentSystemId,
            _nextSystemId
        );

        LogCustom("FlyButton travel result = " + result);

        if (result != TravelFailReason.None)
        {
            LogTravelFailReason(result);
            return;
        }

        // TravelResult travelResult = _travelService.TryTravel(_nextSystemId);

        // if (travelResult == null)
        // {
        //     LogCustom("[GalaxySystemInfoPanel2A] TravelResult is null.");
        //     return;
        // }

        // if (!travelResult.Success)
        // {
        //     LogCustom(
        //         "[GalaxySystemInfoPanel2A] Travel failed. Reason = " +
        //         travelResult.FailReason
        //     );

        //     return;
        // }

        // LogCustom("Travel started/completed to system: " + _nextSystemId);

        _gameStateMachine.Enter(new MetaState());
        // _simpleEventBus.Publish(new RouteExitMapChangedEvent(
        //     _routeConfig,
        //     currentSystemId,
        //     _targetSystemId,
        //     _endpointConfig.ExitPoint,
        //     _routeConfig.GetEntryPoint(_targetSystemId)
        // ));
    }

    private void OnFlyButtonClicked2()
    {
        if (string.IsNullOrWhiteSpace(_nextSystemId))
        {
            LogCustom("[GalaxySystemInfoPanel2A] Нет следующей системы для перелёта.");
            return;
        }

        string currentSystemId = GetCurrentSystemId();

        if (string.IsNullOrWhiteSpace(currentSystemId))
        {
            LogCustom("[GalaxySystemInfoPanel2A] Текущая система не найдена.");
            return;
        }

        TravelFailReason result = _travelService.GetTravelFailReason(
            currentSystemId,
            _nextSystemId
        );

        LogCustom("[GalaxySystemInfoPanel2A] FlyButton travel result = " + result);

        if (result != TravelFailReason.None)
        {
            LogTravelFailReason(result);
            return;
        }

        RouteConfig routeConfig = FindRouteConfig(
            currentSystemId,
            _nextSystemId
        );

        if (routeConfig == null)
        {
            LogCustom(
                "[GalaxySystemInfoPanel2A] RouteConfig не найден. currentSystemId = " +
                currentSystemId +
                " | nextSystemId = " +
                _nextSystemId
            );

            return;
        }

        Vector3 exitPoint = routeConfig.GetExitPoint(currentSystemId);
        Vector3 entryPoint = routeConfig.GetEntryPoint(_nextSystemId);

        LogCustom(
            "[GalaxySystemInfoPanel2A] Route exit selected. Route = " +
            routeConfig.Id +
            " | from = " +
            currentSystemId +
            " | to = " +
            _nextSystemId +
            " | exitPoint = " +
            exitPoint +
            " | entryPoint = " +
            entryPoint
        );

        _systemTravelService.SetSystemExitDestination(new RouteExitMapChangedEvent(
            routeConfig,
            currentSystemId,
            _nextSystemId,
            exitPoint,
            entryPoint
        ));
        _gameStateMachine.Enter(new MetaState());
        // _eventBus.Publish(new StarSystemEnteredEvent(currentSystemId));
        // _eventBus.Publish(new RouteExitMapChangedEvent(
        //     routeConfig,
        //     currentSystemId,
        //     _nextSystemId,
        //     exitPoint,
        //     entryPoint
        // ));
    }

    private void LogTravelFailReason(TravelFailReason reason)
    {
        switch (reason)
        {
            case TravelFailReason.NotEnoughFuel:
                LogCustom("[GalaxySystemInfoPanel2A] Недостаточно топлива для первого прыжка.");
                break;

            case TravelFailReason.SystemsAreNotNeighbors:
                LogCustom("[GalaxySystemInfoPanel2A] Первая система маршрута недоступна.");
                break;

            case TravelFailReason.TargetSystemMissing:
                LogCustom("[GalaxySystemInfoPanel2A] Целевая система не найдена.");
                break;

            case TravelFailReason.CurrentSystemMissing:
                LogCustom("[GalaxySystemInfoPanel2A] Текущая система не найдена.");
                break;

            case TravelFailReason.TargetSystemIsCurrent:
                LogCustom("[GalaxySystemInfoPanel2A] Уже находимся в выбранной системе.");
                break;

            default:
                LogCustom("[GalaxySystemInfoPanel2A] Перелёт невозможен. Причина: " + reason);
                break;
        }
    }

    private string GetCurrentSystemId()
    {
        if (_gameSessionService == null)
            return string.Empty;

        if (_gameSessionService.State == null)
            return string.Empty;

        if (_gameSessionService.State.Player == null)
            return string.Empty;

        return _gameSessionService.State.Player.CurrentSystemId;
    }
}