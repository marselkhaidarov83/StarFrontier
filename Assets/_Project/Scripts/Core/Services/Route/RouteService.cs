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
        if (_galaxyRuntimeState == null && Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>().State != null)
            _galaxyRuntimeState = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>().State.Galaxy;        
    }

    public bool IsRouteUnlocked(string routeId)
    {
        RouteRuntimeState routeState = FindRouteState(routeId);
        return routeState != null && routeState.IsUnlocked;
    }

    public bool HasUnlockedRoute(string fromSystemId, string toSystemId)
    {
        string routeId = FindRouteId(fromSystemId, toSystemId);

        if (string.IsNullOrEmpty(routeId))
            return false;

        return IsRouteUnlocked(routeId);
    }

    public string FindRouteId(string fromSystemId, string toSystemId)
    {
        if (_configService.GalaxyConfig == null || _configService.GetAllSectors() == null)
            return string.Empty;

        foreach (SectorConfig sectorConfig in _configService.GetAllSectors())
        {
            if (sectorConfig == null || sectorConfig.Systems == null)
                continue;

            foreach (StarSystemConfig systemConfig in sectorConfig.Systems)
            {
                if (systemConfig == null || systemConfig.Routes == null)
                    continue;

                foreach (RouteConfig routeConfig in systemConfig.Routes)
                {
                    if (routeConfig == null)
                        continue;

                    bool directRoute =
                        routeConfig.FromSystem.Id == fromSystemId &&
                        routeConfig.ToSystem.Id == toSystemId;

                    bool reverseRoute =
                        routeConfig.FromSystem.Id == toSystemId &&
                        routeConfig.ToSystem.Id == fromSystemId;

                    if (directRoute || reverseRoute)
                        return routeConfig.Id;
                }
            }
        }

        return string.Empty;
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
            route => route.RouteId == routeId
        );
    }
}