using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GalaxyMapRuntimeView : MonoBehaviour
{
    [Header("Корневые объекты")]
    [SerializeField] private RectTransform systemsRoot;
    [SerializeField] private RectTransform routesRoot;

    [Header("Шаблоны")]
    [SerializeField] private StarSystemNodeView2 systemNodePrefab;
    [SerializeField] private GalaxyRouteLineView routeLinePrefab;

    [Header("Панель информации")]
    [SerializeField] private GalaxySystemInfoPanel infoPanel;

    private GalaxyRuntimeState _galaxyRuntimeState;
    private GalaxyConfig _galaxyConfig;
    private ITravelService _travelService;
    private SimpleEventBus _eventBus;

    private readonly Dictionary<string, StarSystemNodeView2> _systemViews = new();
    private readonly Dictionary<string, GalaxyRouteLineView> _routeViews = new();

    private string _selectedSystemId;

    private void Start()
    {
        _galaxyRuntimeState =
            Bootstrapper.Instance.ServiceRegistry.Get<GalaxyRuntimeState>();

        _travelService =
            Bootstrapper.Instance.ServiceRegistry.Get<ITravelService>();

        _eventBus =
            Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        IConfigService configService =
            Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();

        _galaxyConfig = configService.GalaxyConfig;

        // if (infoPanel != null)
        //     infoPanel.Initialize(this);

        SubscribeEvents();
        Rebuild();
    }

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Subscribe<StarSystemUnlockedEvent>(OnStarSystemDiscovered);
        _eventBus.Subscribe<RouteUnlockedEvent>(OnRouteUnlocked);
        _eventBus.Subscribe<CurrentSystemEnteredEvent>(OnCurrentSystemChanged);
        // _eventBus.Subscribe<StarSystemSimulatedEvent>(OnStarSystemSimulated);
    }

    private void UnsubscribeEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Unsubscribe<StarSystemUnlockedEvent>(OnStarSystemDiscovered);
        _eventBus.Unsubscribe<RouteUnlockedEvent>(OnRouteUnlocked);
        _eventBus.Unsubscribe<CurrentSystemEnteredEvent>(OnCurrentSystemChanged);
        // _eventBus.Unsubscribe<StarSystemSimulatedEvent>(OnStarSystemSimulated);
    }

    public void Rebuild()
    {
        ClearOldViews();
        BuildRoutes();
        BuildSystems();
    }

    private void ClearOldViews()
    {
        foreach (Transform child in systemsRoot)
            Destroy(child.gameObject);

        foreach (Transform child in routesRoot)
            Destroy(child.gameObject);

        _systemViews.Clear();
        _routeViews.Clear();
    }

    private void BuildSystems()
    {
        if (_galaxyConfig == null || _galaxyConfig.Sectors == null)
            return;

        foreach (SectorConfig sectorConfig in _galaxyConfig.Sectors)
        {
            if (sectorConfig == null || sectorConfig.Systems == null)
                continue;

            foreach (StarSystemConfig systemConfig in sectorConfig.Systems)
            {
                CreateSystemView(systemConfig);
            }
        }
    }

    private void CreateSystemView(StarSystemConfig systemConfig)
    {
        if (systemConfig == null)
            return;

        StarSystemRuntimeState systemState = FindSystemState(systemConfig.Id);

        if (systemState == null)
            return;

        StarSystemNodeView2 view = Instantiate(systemNodePrefab, systemsRoot);
        RectTransform rectTransform = view.transform as RectTransform;
        rectTransform.anchoredPosition = systemConfig.MapPosition;

        bool isCurrent =
            _galaxyRuntimeState.CurrentSystemId == systemConfig.Id;

        // view.Initialize(
        //     systemConfig.Id,
        //     systemConfig.DisplayName,
        //     systemConfig.Icon,
        //     systemState.IsDiscovered,
        //     systemState.IsVisited,
        //     isCurrent,
        //     this
        // );

        _systemViews[systemConfig.Id] = view;
    }

    private void BuildRoutes()
    {
        if (_galaxyConfig == null || _galaxyConfig.Sectors == null)
            return;

        foreach (SectorConfig sectorConfig in _galaxyConfig.Sectors)
        {
            if (sectorConfig == null || sectorConfig.Systems == null)
                continue;

            foreach (StarSystemConfig systemConfig in sectorConfig.Systems)
            {
                if (systemConfig == null || systemConfig.Routes == null)
                    continue;

                foreach (RouteConfig routeConfig in systemConfig.Routes)
                {
                    CreateRouteView(routeConfig);
                }
            }
        }
    }

    private void CreateRouteView(RouteConfig routeConfig)
    {
        if (routeConfig == null)
            return;

        StarSystemConfig fromConfig = FindSystemConfig(routeConfig.FromSystem.Id);
        StarSystemConfig toConfig = FindSystemConfig(routeConfig.ToSystem.Id);

        if (fromConfig == null || toConfig == null)
            return;

        RouteRuntimeState routeState = FindRouteState(routeConfig.Id);

        if (routeState == null)
            return;

        GalaxyRouteLineView view = Instantiate(routeLinePrefab, routesRoot);

        // view.Initialize(
        //     routeConfig.Id,
        //     fromConfig.MapPosition,
        //     toConfig.MapPosition,
        //     routeState.IsUnlocked
        // );

        _routeViews[routeConfig.Id] = view;
    }

    public void SelectSystem(string systemId)
    {
        _selectedSystemId = systemId;

        StarSystemConfig config = FindSystemConfig(systemId);
        StarSystemRuntimeState state = FindSystemState(systemId);

        if (config == null || state == null || infoPanel == null)
            return;

        bool isCurrent =
            _galaxyRuntimeState.CurrentSystemId == systemId;

        // infoPanel.Show(
        //     systemId,
        //     config.DisplayName,
        //     state.IsDiscovered,
        //     state.IsVisited,
        //     isCurrent,
        //     state.DangerLevel,
        //     state.DevelopmentLevel,
        //     state.Stability
        // );
    }

    public void TryTravelToSelectedSystem(string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId))
            return;

        _travelService.TryTravel(systemId);
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
        foreach (SectorConfig sectorConfig in _galaxyConfig.Sectors)
        {
            foreach (StarSystemConfig systemConfig in sectorConfig.Systems)
            {
                if (systemConfig.Id == systemId)
                    return systemConfig;
            }
        }

        return null;
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
    }

    // private void OnStarSystemSimulated(StarSystemSimulatedEvent eventData)
    // {
    //     if (!string.IsNullOrWhiteSpace(_selectedSystemId))
    //         SelectSystem(_selectedSystemId);
    // }
}