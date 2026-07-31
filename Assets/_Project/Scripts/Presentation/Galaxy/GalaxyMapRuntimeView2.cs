using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GalaxyMapRuntimeView2 : CustomMonoBehaviour
{
    [Header("Корневые объекты")]
    [SerializeField] private Transform systemsRoot;
    [SerializeField] private Transform routesRoot;

    [Header("Шаблоны")]
    [SerializeField] private StarSystemNodeView2A systemNodePrefab;
    [SerializeField] private GalaxyRouteLineView routeLinePrefab;

    [Header("Панель информации")]
    [SerializeField] private GalaxySystemInfoPanel infoPanel;

    [Header("Камера карты")]
    [SerializeField] private Camera mapCamera;

    [Header("Настройки камеры")]
    [SerializeField] private bool focusCurrentSystemOnStart = true;

    private GalaxyRuntimeState _galaxyRuntimeState;
    private GalaxyConfig _galaxyConfig;
    private ITravelService _travelService;
    private IConfigService _configService;
    private IGameSessionService _gameSessionService;
    private SimpleEventBus _eventBus;

    private readonly Dictionary<string, StarSystemNodeView2A> _systemViews = new();
    private readonly Dictionary<string, GalaxyRouteLineView> _routeViews = new();

    private readonly Dictionary<string, StarSystemConfig> _systemConfigsById = new();
    private readonly Dictionary<string, RouteConfig> _routeConfigsByKey = new();

    private readonly List<string> _selectedPath = new();

    private string _selectedSystemId;
    private bool _isInitialized;

    private void Start()
    {
        Initialize();
        SubscribeEvents();
        Rebuild();

        if (focusCurrentSystemOnStart)
            FocusCurrentSystem();
    }

    private void OnEnable()
    {
        if (!_isInitialized)
            return;

        Rebuild();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    private void Initialize()
    {
        if (_isInitialized)
            return;

        _gameSessionService =
            Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();

        _galaxyRuntimeState =
            _gameSessionService.State.Galaxy;

        _travelService =
            Bootstrapper.Instance.ServiceRegistry.Get<ITravelService>();

        _eventBus =
            Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        _configService =
            Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();

        _galaxyConfig =
            _configService.GalaxyConfig;

        if (mapCamera == null)
            mapCamera = Camera.main;

        if (infoPanel != null)
            infoPanel.Initialize(StartTravelToNextSystem);

        _isInitialized = true;
    }

    private void SubscribeEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Subscribe<StarSystemUnlockedEvent>(OnStarSystemDiscovered);
        _eventBus.Subscribe<RouteUnlockedEvent>(OnRouteUnlocked);
        _eventBus.Subscribe<CurrentSystemEnteredEvent>(OnCurrentSystemChanged);
    }

    private void UnsubscribeEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Unsubscribe<StarSystemUnlockedEvent>(OnStarSystemDiscovered);
        _eventBus.Unsubscribe<RouteUnlockedEvent>(OnRouteUnlocked);
        _eventBus.Unsubscribe<CurrentSystemEnteredEvent>(OnCurrentSystemChanged);
    }

    public void Rebuild()
    {
        Initialize();

        ClearOldViews();
        CacheConfigs();
        BuildRoutes();
        BuildSystems();

        if (!string.IsNullOrWhiteSpace(_selectedSystemId))
            SelectSystem(_selectedSystemId);
    }

    private void ClearOldViews()
    {
        if (systemsRoot != null)
        {
            for (int i = systemsRoot.childCount - 1; i >= 0; i--)
                Destroy(systemsRoot.GetChild(i).gameObject);
        }

        if (routesRoot != null)
        {
            for (int i = routesRoot.childCount - 1; i >= 0; i--)
                Destroy(routesRoot.GetChild(i).gameObject);
        }

        _systemViews.Clear();
        _routeViews.Clear();
        _systemConfigsById.Clear();
        _routeConfigsByKey.Clear();
        _selectedPath.Clear();
    }

    private void CacheConfigs()
    {
        if (_galaxyConfig == null || _galaxyConfig.Sectors == null)
            return;

        foreach (SectorConfig sectorConfig in _galaxyConfig.Sectors)
        {
            if (sectorConfig == null || sectorConfig.Systems == null)
                continue;

            foreach (StarSystemConfig systemConfig in sectorConfig.Systems)
            {
                if (systemConfig == null)
                    continue;

                if (!_systemConfigsById.ContainsKey(systemConfig.Id))
                    _systemConfigsById.Add(systemConfig.Id, systemConfig);

                if (systemConfig.Routes == null)
                    continue;

                foreach (RouteConfig routeConfig in systemConfig.Routes)
                {
                    if (routeConfig == null)
                        continue;

                    string fromSystemId = GetRouteFromSystemId(routeConfig);
                    string toSystemId = GetRouteToSystemId(routeConfig);

                    if (string.IsNullOrWhiteSpace(fromSystemId))
                        continue;

                    if (string.IsNullOrWhiteSpace(toSystemId))
                        continue;

                    string routeKey = MakeRouteKey(fromSystemId, toSystemId);

                    if (!_routeConfigsByKey.ContainsKey(routeKey))
                        _routeConfigsByKey.Add(routeKey, routeConfig);
                }
            }
        }
    }

    private void BuildSystems()
    {
        if (systemsRoot == null)
        {
            Debug.LogError("[GalaxyMapRuntimeView2] SystemsRoot не назначен.");
            return;
        }

        if (systemNodePrefab == null)
        {
            Debug.LogError("[GalaxyMapRuntimeView2] SystemNodePrefab не назначен.");
            return;
        }

        foreach (StarSystemConfig systemConfig in _systemConfigsById.Values)
        {
            CreateSystemView(systemConfig);
        }
    }

    private void CreateSystemView(StarSystemConfig systemConfig)
    {
        if (systemConfig == null)
            return;

        StarSystemRuntimeState systemState = FindSystemState(systemConfig.Id);

        if (systemState == null)
            return;

        StarSystemNodeView2A view = Instantiate(systemNodePrefab, systemsRoot);

        view.Initialize(
            systemConfig,
            SelectSystem
        );

        _systemViews[systemConfig.Id] = view;
    }

    private void BuildRoutes()
    {
        if (routesRoot == null)
        {
            Debug.LogError("[GalaxyMapRuntimeView2] RoutesRoot не назначен.");
            return;
        }

        if (routeLinePrefab == null)
        {
            Debug.LogError("[GalaxyMapRuntimeView2] RouteLinePrefab не назначен.");
            return;
        }

        foreach (KeyValuePair<string, RouteConfig> pair in _routeConfigsByKey)
        {
            RouteConfig routeConfig = pair.Value;
            CreateRouteView(routeConfig);
        }
    }

    private void CreateRouteView(RouteConfig routeConfig)
    {
        if (routeConfig == null)
            return;

        string fromSystemId = GetRouteFromSystemId(routeConfig);
        string toSystemId = GetRouteToSystemId(routeConfig);

        if (string.IsNullOrWhiteSpace(fromSystemId))
            return;

        if (string.IsNullOrWhiteSpace(toSystemId))
            return;

        StarSystemConfig fromConfig = FindSystemConfig(fromSystemId);
        StarSystemConfig toConfig = FindSystemConfig(toSystemId);

        if (fromConfig == null || toConfig == null)
            return;

        RouteRuntimeState routeState = FindRouteState(routeConfig.Id);

        bool isUnlocked = routeState == null || routeState.IsUnlocked;

        GalaxyMapRouteVisualState visualState = isUnlocked
            ? GalaxyMapRouteVisualState.Normal
            : GalaxyMapRouteVisualState.Disabled;

        GalaxyRouteLineView view = Instantiate(routeLinePrefab, routesRoot);

        view.Initialize(
            routeConfig,
            fromSystemId,
            toSystemId,
            fromConfig.MapPosition,
            toConfig.MapPosition,
            visualState
        );

        string routeKey = MakeRouteKey(fromSystemId, toSystemId);
        _routeViews[routeKey] = view;
    }

    public void SelectSystem(string systemId)
    {
        LogCustom("start");

        if (string.IsNullOrWhiteSpace(systemId))
            return;

        _selectedSystemId = systemId;

        foreach (StarSystemNodeView2A systemView in _systemViews.Values)
        {
            if (systemView == null)
                continue;

            systemView.SetSelected(systemView.SystemId == systemId);
        }

        string currentSystemId = GetCurrentSystemId();

        if (systemId == currentSystemId)
            FocusCurrentSystem();

        BuildSelectedPath(currentSystemId, systemId);
        RefreshRouteSelection();
        ShowSystemInfo(systemId);

        LogCustom("end");
    }

    private void BuildSelectedPath(string currentSystemId, string targetSystemId)
    {
        _selectedPath.Clear();

        if (string.IsNullOrWhiteSpace(currentSystemId))
            return;

        if (string.IsNullOrWhiteSpace(targetSystemId))
            return;

        if (currentSystemId == targetSystemId)
        {
            _selectedPath.Add(currentSystemId);
            return;
        }

        List<string> path = FindShortestPath(currentSystemId, targetSystemId);

        foreach (string systemId in path)
            _selectedPath.Add(systemId);
    }

    private List<string> FindShortestPath(string startSystemId, string targetSystemId)
    {
        Dictionary<string, List<string>> graph = BuildGraph();

        if (!graph.ContainsKey(startSystemId))
            return new List<string>();

        if (!graph.ContainsKey(targetSystemId))
            return new List<string>();

        Queue<string> queue = new();
        HashSet<string> visited = new();
        Dictionary<string, string> previous = new();

        queue.Enqueue(startSystemId);
        visited.Add(startSystemId);

        while (queue.Count > 0)
        {
            string currentSystemId = queue.Dequeue();

            if (currentSystemId == targetSystemId)
                break;

            foreach (string nextSystemId in graph[currentSystemId])
            {
                if (visited.Contains(nextSystemId))
                    continue;

                visited.Add(nextSystemId);
                previous[nextSystemId] = currentSystemId;
                queue.Enqueue(nextSystemId);
            }
        }

        if (!visited.Contains(targetSystemId))
            return new List<string>();

        List<string> path = new();
        string pathSystemId = targetSystemId;

        path.Add(pathSystemId);

        while (pathSystemId != startSystemId)
        {
            if (!previous.ContainsKey(pathSystemId))
                return new List<string>();

            pathSystemId = previous[pathSystemId];
            path.Add(pathSystemId);
        }

        path.Reverse();
        return path;
    }

    private Dictionary<string, List<string>> BuildGraph()
    {
        Dictionary<string, List<string>> graph = new();

        foreach (StarSystemConfig systemConfig in _systemConfigsById.Values)
        {
            if (systemConfig == null)
                continue;

            if (!CanUseSystemInPath(systemConfig.Id))
                continue;

            if (!graph.ContainsKey(systemConfig.Id))
                graph.Add(systemConfig.Id, new List<string>());
        }

        foreach (RouteConfig routeConfig in _routeConfigsByKey.Values)
        {
            if (routeConfig == null)
                continue;

            string fromSystemId = GetRouteFromSystemId(routeConfig);
            string toSystemId = GetRouteToSystemId(routeConfig);

            if (string.IsNullOrWhiteSpace(fromSystemId))
                continue;

            if (string.IsNullOrWhiteSpace(toSystemId))
                continue;

            if (!CanUseSystemInPath(fromSystemId))
                continue;

            if (!CanUseSystemInPath(toSystemId))
                continue;

            if (!CanUseRouteInPath(routeConfig.Id))
                continue;

            if (!graph.ContainsKey(fromSystemId))
                graph.Add(fromSystemId, new List<string>());

            if (!graph.ContainsKey(toSystemId))
                graph.Add(toSystemId, new List<string>());

            if (!graph[fromSystemId].Contains(toSystemId))
                graph[fromSystemId].Add(toSystemId);

            if (!graph[toSystemId].Contains(fromSystemId))
                graph[toSystemId].Add(fromSystemId);
        }

        return graph;
    }

    private bool CanUseSystemInPath(string systemId)
    {
        StarSystemRuntimeState systemState = FindSystemState(systemId);

        if (systemState == null)
            return false;

        return systemState.IsDiscovered;
    }

    private bool CanUseRouteInPath(string routeId)
    {
        RouteRuntimeState routeState = FindRouteState(routeId);

        if (routeState == null)
            return true;

        return routeState.IsUnlocked;
    }

    private void RefreshRouteSelection()
    {
        foreach (GalaxyRouteLineView routeView in _routeViews.Values)
        {
            if (routeView == null)
                continue;

            RouteConfig routeConfig = FindRouteConfigBySystems(
                routeView.FromSystemId,
                routeView.ToSystemId
            );

            bool isUnlocked = true;

            if (routeConfig != null)
            {
                RouteRuntimeState routeState = FindRouteState(routeConfig.Id);

                if (routeState != null)
                    isUnlocked = routeState.IsUnlocked;
            }

            routeView.SetVisualState(
                isUnlocked
                    ? GalaxyMapRouteVisualState.Normal
                    : GalaxyMapRouteVisualState.Disabled
            );
        }

        if (_selectedPath.Count < 2)
            return;

        for (int i = 0; i < _selectedPath.Count - 1; i++)
        {
            string routeKey = MakeRouteKey(
                _selectedPath[i],
                _selectedPath[i + 1]
            );

            if (_routeViews.TryGetValue(routeKey, out GalaxyRouteLineView routeView))
            {
                routeView.SetVisualState(
                    GalaxyMapRouteVisualState.SelectedPath
                );
            }
        }
    }

    private void ShowSystemInfo(string systemId)
    {
        StarSystemConfig config = FindSystemConfig(systemId);
        StarSystemRuntimeState state = FindSystemState(systemId);

        if (config == null || state == null || infoPanel == null)
            return;

        string currentSystemId = GetCurrentSystemId();
        string nextSystemId = GetNextSystemIdInSelectedPath(currentSystemId);

        int totalPathCost = CalculateSelectedPathCost();

        bool canStartTravel = CanStartTravelToNextSystem(nextSystemId);
        string failReasonText = BuildFailReasonText(nextSystemId, canStartTravel);

        infoPanel.Show(
            config,
            state,
            currentSystemId,
            new List<string>(_selectedPath),
            totalPathCost,
            nextSystemId,
            canStartTravel,
            failReasonText
        );
    }

    private string GetNextSystemIdInSelectedPath(string currentSystemId)
    {
        if (_selectedPath.Count < 2)
            return string.Empty;

        if (_selectedPath[0] != currentSystemId)
            return string.Empty;

        return _selectedPath[1];
    }

    private int CalculateSelectedPathCost()
    {
        if (_selectedPath.Count < 2)
            return 0;

        int totalCost = 0;

        for (int i = 0; i < _selectedPath.Count - 1; i++)
        {
            int cost = _travelService.GetTravelCost(
                _selectedPath[i],
                _selectedPath[i + 1]
            );

            if (cost <= 0)
                cost = 1;

            totalCost += cost;
        }

        return totalCost;
    }

    private bool CanStartTravelToNextSystem(string nextSystemId)
    {
        if (string.IsNullOrWhiteSpace(nextSystemId))
            return false;

        TravelFailReason failReason = _travelService.GetTravelFailReason(
            GetCurrentSystemId(),
            nextSystemId
        );

        return failReason == TravelFailReason.None;
    }

    private string BuildFailReasonText(string nextSystemId, bool canStartTravel)
    {
        if (string.IsNullOrWhiteSpace(nextSystemId))
            return string.Empty;

        if (canStartTravel)
            return string.Empty;

        TravelFailReason failReason = _travelService.GetTravelFailReason(
            GetCurrentSystemId(),
            nextSystemId
        );

        switch (failReason)
        {
            case TravelFailReason.NotEnoughFuel:
                return "Недостаточно топлива для первого прыжка";

            case TravelFailReason.SystemsAreNotNeighbors:
                return "Первая система маршрута недоступна";

            case TravelFailReason.TargetSystemIsCurrent:
                return "Вы уже находитесь в этой системе";

            case TravelFailReason.TargetSystemMissing:
                return "Целевая система не найдена";

            case TravelFailReason.CurrentSystemMissing:
                return "Текущая система не найдена";

            default:
                return "Перелёт сейчас невозможен";
        }
    }

    private void StartTravelToNextSystem(string nextSystemId)
    {
        if (string.IsNullOrWhiteSpace(nextSystemId))
            return;

        if (!CanStartTravelToNextSystem(nextSystemId))
            return;

        StarSystemLink systemLink =
            _configService.GetCurrentStarSystemLink(nextSystemId);

        if (systemLink == null)
        {
            Debug.LogError(
                "[GalaxyMapRuntimeView2] Не найден StarSystemLink для перелёта в систему: "
                + nextSystemId
            );

            return;
        }

        _eventBus.Publish(new ExitMapChangedEvent(systemLink));
    }

    public void TryTravelToSelectedSystem(string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId))
            return;

        SelectSystem(systemId);

        string nextSystemId = GetNextSystemIdInSelectedPath(
            GetCurrentSystemId()
        );

        StartTravelToNextSystem(nextSystemId);
    }

    public void FocusCurrentSystem()
    {
        FocusSystem(GetCurrentSystemId());
    }

    public void FocusSystem(string systemId)
    {
        if (mapCamera == null)
            return;

        StarSystemConfig systemConfig = FindSystemConfig(systemId);

        if (systemConfig == null)
            return;

        Vector3 cameraPosition = mapCamera.transform.position;

        mapCamera.transform.position = new Vector3(
            systemConfig.MapPosition.x,
            systemConfig.MapPosition.y,
            cameraPosition.z
        );
    }

    private StarSystemRuntimeState FindSystemState(string systemId)
    {
        if (_galaxyRuntimeState == null || _galaxyRuntimeState.Systems == null)
            return null;

        return _galaxyRuntimeState.Systems.FirstOrDefault(
            system => system.SystemId == systemId
        );
    }

    private RouteRuntimeState FindRouteState(string routeId)
    {
        if (_galaxyRuntimeState == null || _galaxyRuntimeState.Routes == null)
            return null;

        return _galaxyRuntimeState.Routes.FirstOrDefault(
            route => route.RouteId == routeId
        );
    }

    private StarSystemConfig FindSystemConfig(string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId))
            return null;

        if (_systemConfigsById.TryGetValue(systemId, out StarSystemConfig cachedConfig))
            return cachedConfig;

        if (_galaxyConfig == null || _galaxyConfig.Sectors == null)
            return null;

        foreach (SectorConfig sectorConfig in _galaxyConfig.Sectors)
        {
            if (sectorConfig == null || sectorConfig.Systems == null)
                continue;

            foreach (StarSystemConfig systemConfig in sectorConfig.Systems)
            {
                if (systemConfig != null && systemConfig.Id == systemId)
                    return systemConfig;
            }
        }

        return null;
    }

    private RouteConfig FindRouteConfigBySystems(string firstSystemId, string secondSystemId)
    {
        string routeKey = MakeRouteKey(firstSystemId, secondSystemId);

        if (_routeConfigsByKey.TryGetValue(routeKey, out RouteConfig routeConfig))
            return routeConfig;

        return null;
    }

    private string GetRouteFromSystemId(RouteConfig routeConfig)
    {
        if (routeConfig == null || routeConfig.FromSystem == null)
            return string.Empty;

        return routeConfig.FromSystem.Id;
    }

    private string GetRouteToSystemId(RouteConfig routeConfig)
    {
        if (routeConfig == null || routeConfig.ToSystem == null)
            return string.Empty;

        return routeConfig.ToSystem.Id;
    }

    private string GetCurrentSystemId()
    {
        if (_galaxyRuntimeState != null &&
            !string.IsNullOrWhiteSpace(_galaxyRuntimeState.CurrentSystemId))
        {
            return _galaxyRuntimeState.CurrentSystemId;
        }

        if (_gameSessionService != null &&
            _gameSessionService.State != null &&
            _gameSessionService.State.Player != null)
        {
            return _gameSessionService.State.Player.CurrentSystemId;
        }

        return string.Empty;
    }

    private string MakeRouteKey(string firstSystemId, string secondSystemId)
    {
        if (string.CompareOrdinal(firstSystemId, secondSystemId) <= 0)
            return firstSystemId + "__" + secondSystemId;

        return secondSystemId + "__" + firstSystemId;
    }

    private void OnStarSystemDiscovered(StarSystemUnlockedEvent eventData)
    {
        Rebuild();
    }

    private void OnRouteUnlocked(RouteUnlockedEvent eventData)
    {
        Rebuild();
    }

    private void OnCurrentSystemChanged(CurrentSystemEnteredEvent eventData)
    {
        Rebuild();

        if (focusCurrentSystemOnStart)
            FocusCurrentSystem();
    }
}