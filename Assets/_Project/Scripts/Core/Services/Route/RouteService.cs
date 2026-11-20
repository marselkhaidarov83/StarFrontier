using System.Linq;

public class RouteService : CustomService, IRouteService
{
    private GalaxyRuntimeState _galaxyRuntimeState;
    private readonly SimpleEventBus _eventBus;
    private readonly IConfigService _configService;

    public RouteService()
    {
        GalaxyRuntimeStateUpdate();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
    }

    private void GalaxyRuntimeStateUpdate()
    {
        _galaxyRuntimeState = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>().State?.Galaxy;
    }

    public bool IsRouteUnlocked(string routeId)
    {
        if (string.IsNullOrWhiteSpace(routeId))
            return false;

        RouteConfig routeConfig = FindRouteConfigById(routeId);

        if (routeConfig == null)
            return false;

        return IsRouteAvailable(routeConfig);
    }

    public bool HasUnlockedRoute(string fromSystemId, string toSystemId)
    {
        if (string.IsNullOrWhiteSpace(fromSystemId) || string.IsNullOrWhiteSpace(toSystemId)) return false;
        StarSystemConfig source = _configService.GetStarSystemConfigById(fromSystemId);
        return source?.Routes != null && source.Routes.Any(route => route != null &&
            route.ConnectsSystems(fromSystemId, toSystemId) && IsRouteAvailable(route));
    }

    private bool IsRouteAvailable(RouteConfig routeConfig)
    {
        if (routeConfig == null)
            return false;

        if (!IsRouteStateUnlocked(routeConfig))
            return false;

        if (routeConfig.FromSystem == null || routeConfig.ToSystem == null)
            return false;

        if (!IsSystemSectorUnlocked(routeConfig.FromSystem.Id))
            return false;

        if (!IsSystemSectorUnlocked(routeConfig.ToSystem.Id))
            return false;

        return true;
    }

    private bool IsRouteStateUnlocked(RouteConfig routeConfig)
    {
        GalaxyRuntimeStateUpdate();

        return routeConfig.IsUnlocked(_galaxyRuntimeState);
    }

    private bool IsSystemSectorUnlocked(string systemId)
    {
        GalaxyRuntimeStateUpdate();

        if (string.IsNullOrWhiteSpace(systemId))
            return false;

        foreach (SectorConfig sectorConfig in _configService.GetAllSectors())
        {
            if (sectorConfig == null || sectorConfig.Systems == null)
                continue;

            bool containsSystem = sectorConfig.Systems.Any(
                systemConfig => systemConfig != null && systemConfig.Id == systemId
            );

            if (!containsSystem)
                continue;

            SectorRuntimeState sectorState = _galaxyRuntimeState?.Sectors?
                .FirstOrDefault(state => state != null && state.SectorId == sectorConfig.Id);

            if (sectorState != null)
                return sectorState.IsUnlocked;

            return sectorConfig.IsUnlocked;
        }

        return false;
    }

    private RouteConfig FindRouteConfig(string fromSystemId, string toSystemId)
    {
        StarSystemConfig source = _configService.GetStarSystemConfigById(fromSystemId);
        return source?.Routes?.FirstOrDefault(route => route != null && route.ConnectsSystems(fromSystemId, toSystemId));
    }

    private RouteConfig FindRouteConfigById(string routeId)
    {
        foreach (StarSystemConfig system in _configService.GetAllStarSystems())
        {
            if (system?.Routes == null) continue;
            foreach (RouteConfig route in system.Routes)
                if (route != null && route.Id == routeId && route.ContainsSystem(system.Id)) return route;
        }

        return null;
    }

    public string FindRouteId(string fromSystemId, string toSystemId)
    {
        if (string.IsNullOrWhiteSpace(fromSystemId) || string.IsNullOrWhiteSpace(toSystemId)) return string.Empty;
        return FindRouteConfig(fromSystemId, toSystemId)?.Id ?? string.Empty;
    }

    public void UnlockRoute(string routeId)
    {
        RouteRuntimeState routeState = FindRouteState(routeId);

        if (routeState == null)
        {
            LogCustom("[RouteService] Route not found: " + routeId);
            return;
        }

        if (routeState.IsUnlocked)
            return;

        routeState.IsUnlocked = true;

        _eventBus.Publish(new RouteUnlockedEvent(routeId));

        LogCustom("[RouteService] Route unlocked: " + routeId);
    }

    private RouteRuntimeState FindRouteState(string routeId)
    {
        GalaxyRuntimeStateUpdate();

        if (_galaxyRuntimeState == null || _galaxyRuntimeState.Routes == null)
            return null;

        return _galaxyRuntimeState.Routes.FirstOrDefault(
            route => route != null && route.RouteId == routeId
        );
    }

}
