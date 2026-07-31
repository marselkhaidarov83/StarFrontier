using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GalaxyMapRoutesBuilder2A : CustomMonoBehaviour
{
    [Header("Сцена")]
    [SerializeField] private Transform routesRoot;

    [Header("Шаблон")]
    [SerializeField] private GalaxyRouteLineView routeLinePrefab;

    private GalaxyRuntimeState _galaxyRuntimeState;
    private GalaxyConfig _galaxyConfig;

    private IConfigService _configService;
    private IGameSessionService _gameSessionService;
    private SimpleEventBus _eventBus;

    private readonly Dictionary<string, GalaxyRouteLineView> _routeViewsByKey = new();
    private readonly Dictionary<string, RouteConfig> _routeConfigsByKey = new();
    private readonly Dictionary<string, StarSystemConfig> _systemConfigsById = new();

    private readonly List<string> _selectedPath = new();

    private bool _isInitialized;

    public IReadOnlyList<string> SelectedPath => _selectedPath;

    public void Initialize()
    {
        if (_isInitialized)
            return;

        _configService =
            Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();

        _gameSessionService =
            Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();

        _eventBus =
            Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        _galaxyConfig =
            _configService.GalaxyConfig;

        _galaxyRuntimeState =
            _gameSessionService.State.Galaxy;

        SubscribeToEvents();

        _isInitialized = true;

        Rebuild();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Subscribe<RouteUnlockedEvent>(OnRouteUnlocked);
        _eventBus.Subscribe<CurrentSystemEnteredEvent>(OnCurrentSystemEntered);
        _eventBus.Subscribe<StarSystemUnlockedEvent>(OnStarSystemUnlocked);
    }

    private void UnsubscribeFromEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Unsubscribe<RouteUnlockedEvent>(OnRouteUnlocked);
        _eventBus.Unsubscribe<CurrentSystemEnteredEvent>(OnCurrentSystemEntered);
        _eventBus.Unsubscribe<StarSystemUnlockedEvent>(OnStarSystemUnlocked);
    }

    public void Rebuild()
    {
        EnsureInitialized();

        Clear();
        CacheConfigs();
        BuildRoutes();

        // После перестройки карты выбранного маршрута нет,
        // поэтому показываем только маршруты от текущей системы.
        ClearSelectedPath();
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
            Initialize();
    }

    private void Clear()
    {
        if (routesRoot != null)
        {
            for (int i = routesRoot.childCount - 1; i >= 0; i--)
                Destroy(routesRoot.GetChild(i).gameObject);
        }

        _routeViewsByKey.Clear();
        _routeConfigsByKey.Clear();
        _systemConfigsById.Clear();
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

                CacheSystemRoutes(systemConfig);
            }
        }
    }

    private void CacheSystemRoutes(StarSystemConfig systemConfig)
    {
        if (systemConfig == null || systemConfig.Routes == null)
            return;

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

    private void BuildRoutes()
    {
        if (routesRoot == null)
        {
            Debug.LogError("[GalaxyMapRoutesBuilder2A] RoutesRoot не назначен.");
            return;
        }

        if (routeLinePrefab == null)
        {
            Debug.LogError("[GalaxyMapRoutesBuilder2A] RouteLinePrefab не назначен.");
            return;
        }

        foreach (RouteConfig routeConfig in _routeConfigsByKey.Values)
        {
            CreateRouteView(routeConfig);
        }
    }

    private void CreateRouteView(RouteConfig routeConfig)
    {
        if (routeConfig == null)
            return;

        string fromSystemId = GetRouteFromSystemId(routeConfig);
        string toSystemId = GetRouteToSystemId(routeConfig);

        StarSystemConfig fromConfig = FindSystemConfig(fromSystemId);
        StarSystemConfig toConfig = FindSystemConfig(toSystemId);

        if (fromConfig == null || toConfig == null)
            return;

        GalaxyRouteLineView view =
            Instantiate(routeLinePrefab, routesRoot);

        view.Initialize(
            routeConfig,
            fromSystemId,
            toSystemId,
            fromConfig.MapPosition,
            toConfig.MapPosition,
            GetGalaxyMapRoutePointSpacing(),
            GalaxyMapRouteVisualState.Hidden
        );

        string routeKey = MakeRouteKey(fromSystemId, toSystemId);

        _routeViewsByKey[routeKey] = view;
    }

    private float GetGalaxyMapRoutePointSpacing()
    {
        const float fallbackSpacing = 0.25f;

        if (_configService == null)
            return fallbackSpacing;

        GameConfig gameConfig =
            _configService.GameConfig;

        if (gameConfig == null)
            return fallbackSpacing;

        if (gameConfig.galaxyMapRoutePointSpacing <= 0f)
            return fallbackSpacing;

        return gameConfig.galaxyMapRoutePointSpacing;
    }

    public void ClearSelectedPath()
    {
        EnsureInitialized();

        _selectedPath.Clear();

        ShowOnlyCurrentSystemRoutes();
    }

    public List<string> ShowShortestPathFromCurrentSystem(string targetSystemId)
    {
        return ShowShortestPath(
            GetCurrentSystemId(),
            targetSystemId
        );
    }

    public List<string> ShowShortestPath(string fromSystemId, string targetSystemId)
    {
        EnsureInitialized();

        _selectedPath.Clear();

        List<string> path = FindShortestPath(fromSystemId, targetSystemId);

        foreach (string systemId in path)
            _selectedPath.Add(systemId);

        if (_selectedPath.Count >= 2)
            ShowSelectedPath();
        else
            ShowOnlyCurrentSystemRoutes();

        return new List<string>(_selectedPath);
    }

    private void ShowOnlyCurrentSystemRoutes()
    {
        string currentSystemId = GetCurrentSystemId();

        foreach (KeyValuePair<string, GalaxyRouteLineView> pair in _routeViewsByKey)
        {
            GalaxyRouteLineView routeView = pair.Value;

            if (routeView == null)
                continue;

            RouteConfig routeConfig = FindRouteConfigBySystems(
                routeView.FromSystemId,
                routeView.ToSystemId
            );

            if (routeConfig == null)
            {
                routeView.SetVisualState(GalaxyMapRouteVisualState.Hidden);
                continue;
            }

            bool connectedToCurrent =
                IsRouteConnectedToSystem(routeView, currentSystemId);

            if (!connectedToCurrent)
            {
                routeView.SetVisualState(GalaxyMapRouteVisualState.Hidden);
                continue;
            }

            GalaxyMapRouteVisualState baseState =
                GetBaseVisualState(routeConfig);

            routeView.SetVisualState(baseState);
        }
    }

    private void ShowSelectedPath()
    {
        string currentSystemId = GetCurrentSystemId();

        foreach (GalaxyRouteLineView routeView in _routeViewsByKey.Values)
        {
            if (routeView == null)
                continue;

            RouteConfig routeConfig = FindRouteConfigBySystems(
                routeView.FromSystemId,
                routeView.ToSystemId
            );

            if (routeConfig == null)
            {
                routeView.SetVisualState(GalaxyMapRouteVisualState.Hidden);
                continue;
            }

            bool routeInSelectedPath = IsRouteInSelectedPath(routeView);
            bool routeConnectedToCurrentSystem = IsRouteConnectedToSystem(
                routeView,
                currentSystemId
            );

            if (routeInSelectedPath)
            {
                routeView.SetVisualState(GalaxyMapRouteVisualState.SelectedPath);
                continue;
            }

            if (routeConnectedToCurrentSystem)
            {
                GalaxyMapRouteVisualState baseState =
                    GetBaseVisualState(routeConfig);

                routeView.SetVisualState(baseState);
                continue;
            }

            routeView.SetVisualState(GalaxyMapRouteVisualState.Hidden);
        }
    }

    private bool IsRouteInSelectedPath(GalaxyRouteLineView routeView)
    {
        if (routeView == null)
            return false;

        if (_selectedPath == null || _selectedPath.Count < 2)
            return false;

        for (int i = 0; i < _selectedPath.Count - 1; i++)
        {
            string routeKey = MakeRouteKey(
                _selectedPath[i],
                _selectedPath[i + 1]
            );

            if (routeKey == routeView.RouteKey)
                return true;
        }

        return false;
    }

    public string GetNextSystemIdInSelectedPath()
    {
        return GetNextSystemIdInSelectedPath(GetCurrentSystemId());
    }

    public string GetNextSystemIdInSelectedPath(string currentSystemId)
    {
        if (_selectedPath.Count < 2)
            return string.Empty;

        if (_selectedPath[0] != currentSystemId)
            return string.Empty;

        return _selectedPath[1];
    }

    public bool HasSelectedPath()
    {
        return _selectedPath.Count > 1;
    }

    public int GetSelectedPathJumpCount()
    {
        if (_selectedPath.Count < 2)
            return 0;

        return _selectedPath.Count - 1;
    }

    public string GetSelectedPathText()
    {
        if (_selectedPath.Count == 0)
            return "Маршрут не найден";

        if (_selectedPath.Count == 1)
            return "Текущая система";

        return string.Join(" → ", _selectedPath);
    }

    private List<string> FindShortestPath(string startSystemId, string targetSystemId)
    {
        if (string.IsNullOrWhiteSpace(startSystemId))
            return new List<string>();

        if (string.IsNullOrWhiteSpace(targetSystemId))
            return new List<string>();

        if (startSystemId == targetSystemId)
            return new List<string> { startSystemId };

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

        return RestorePath(previous, startSystemId, targetSystemId);
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

    private List<string> RestorePath(
        Dictionary<string, string> previous,
        string startSystemId,
        string targetSystemId)
    {
        List<string> path = new();

        string currentSystemId = targetSystemId;
        path.Add(currentSystemId);

        while (currentSystemId != startSystemId)
        {
            if (!previous.ContainsKey(currentSystemId))
                return new List<string>();

            currentSystemId = previous[currentSystemId];
            path.Add(currentSystemId);
        }

        path.Reverse();

        return path;
    }

    private GalaxyMapRouteVisualState GetBaseVisualState(RouteConfig routeConfig)
    {
        if (routeConfig == null)
            return GalaxyMapRouteVisualState.Hidden;

        string fromSystemId = GetRouteFromSystemId(routeConfig);
        string toSystemId = GetRouteToSystemId(routeConfig);

        StarSystemRuntimeState fromState = FindSystemState(fromSystemId);
        StarSystemRuntimeState toState = FindSystemState(toSystemId);
        RouteRuntimeState routeState = FindRouteState(routeConfig.Id);

        bool fromDiscovered = fromState != null && fromState.IsDiscovered;
        bool toDiscovered = toState != null && toState.IsDiscovered;

        bool routeUnlocked;

        if (routeState != null)
            routeUnlocked = routeState.IsUnlocked;
        else
            routeUnlocked = routeConfig.IsLockedAtStart == false;

        if (!fromDiscovered || !toDiscovered || !routeUnlocked)
            return GalaxyMapRouteVisualState.Hidden;

        return GalaxyMapRouteVisualState.Normal;
    }

    private bool IsRouteConnectedToSystem(
        GalaxyRouteLineView routeView,
        string systemId)
    {
        if (routeView == null)
            return false;

        if (string.IsNullOrWhiteSpace(systemId))
            return false;

        return routeView.FromSystemId == systemId ||
               routeView.ToSystemId == systemId;
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

        if (_systemConfigsById.TryGetValue(systemId, out StarSystemConfig systemConfig))
            return systemConfig;

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

    private void OnRouteUnlocked(RouteUnlockedEvent eventData)
    {
        Rebuild();
    }

    private void OnCurrentSystemEntered(CurrentSystemEnteredEvent eventData)
    {
        Rebuild();
    }

    private void OnStarSystemUnlocked(StarSystemUnlockedEvent eventData)
    {
        Rebuild();
    }
}