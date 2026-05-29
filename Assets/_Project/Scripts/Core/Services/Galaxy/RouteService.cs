using System.Collections.Generic;

public class RouteService : IRouteService
{
    private readonly IGameSessionService _gameSessionService;
    private GalaxyState _galaxyState;
    private readonly IStarSystemService _starSystemService;
    private readonly SimpleEventBus _eventBus;

    public RouteService()
    {
        _gameSessionService = Bootstrapper2A.Instance.ServiceRegistry.Get<IGameSessionService>();
        _starSystemService = Bootstrapper2A.Instance.ServiceRegistry.Get<IStarSystemService>();
        _eventBus = Bootstrapper2A.Instance.ServiceRegistry.Get<SimpleEventBus>();
    }

    private void DetermineGalaxyState()
    {
        if (_galaxyState == null)
            _galaxyState = _gameSessionService.CurrentSave.GameState.Galaxy;
    }

    public RouteState GetRoute(string routeId)
    {
        DetermineGalaxyState();

        if (_galaxyState == null || string.IsNullOrEmpty(routeId))
            return null;

        for (int i = 0; i < _galaxyState.Routes.Count; i++)
        {
            var route = _galaxyState.Routes[i];

            if (route != null && route.Id == routeId)
                return route;
        }

        return null;
    }

    public List<RouteState> GetRoutesFromCurrentSystem()
    {
        DetermineGalaxyState();

        if (_galaxyState == null)
            return new List<RouteState>();

        return GetRoutesForSystem(_galaxyState.CurrentSystemId);
    }

    public List<RouteState> GetRoutesForSystem(string systemId)
    {
        DetermineGalaxyState();

        var result = new List<RouteState>();

        if (_galaxyState == null || string.IsNullOrEmpty(systemId))
            return result;

        for (int i = 0; i < _galaxyState.Routes.Count; i++)
        {
            var route = _galaxyState.Routes[i];

            if (route == null)
                continue;

            if (route.FromSystemId == systemId || route.ToSystemId == systemId)
                result.Add(route);
        }

        return result;
    }

    public RouteState GetRouteBetween(string firstSystemId, string secondSystemId)
    {
        DetermineGalaxyState();

        if (_galaxyState == null)
            return null;

        for (int i = 0; i < _galaxyState.Routes.Count; i++)
        {
            var route = _galaxyState.Routes[i];

            if (route == null)
                continue;

            var directMatch =
                route.FromSystemId == firstSystemId &&
                route.ToSystemId == secondSystemId;

            var reverseMatch =
                route.FromSystemId == secondSystemId &&
                route.ToSystemId == firstSystemId;

            if (directMatch || reverseMatch)
                return route;
        }

        return null;
    }

    public bool CanTravelTo(string targetSystemId)
    {
        DetermineGalaxyState();

        if (_galaxyState == null || string.IsNullOrEmpty(targetSystemId))
            return false;

        if (_galaxyState.CurrentSystemId == targetSystemId)
            return false;

        var targetSystem = _starSystemService.GetSystem(targetSystemId);

        if (targetSystem == null)
            return false;

        if (!targetSystem.IsDiscovered || !targetSystem.IsUnlocked)
            return false;

        var route = GetRouteBetween(_galaxyState.CurrentSystemId, targetSystemId);

        if (route == null)
            return false;

        return route.IsUnlocked;
    }

    public bool TravelTo(string targetSystemId)
    {
        DetermineGalaxyState();

        if (!CanTravelTo(targetSystemId))
            return false;

        var fromSystemId = _galaxyState.CurrentSystemId;

        _galaxyState.CurrentSystemId = targetSystemId;

        _eventBus.Publish(new PlayerTravelledToSystemEvent(fromSystemId, targetSystemId));

        return true;
    }

    public void UnlockRoute(string routeId)
    {
        var route = GetRoute(routeId);

        if (route == null)
            return;

        if (route.IsUnlocked)
            return;

        route.IsUnlocked = true;
        _eventBus.Publish(new RouteUnlockedEvent(routeId));
    }

    public string GetOtherSystemId(RouteState route, string systemId)
    {
        if (route == null || string.IsNullOrEmpty(systemId))
            return null;

        if (route.FromSystemId == systemId)
            return route.ToSystemId;

        if (route.ToSystemId == systemId)
            return route.FromSystemId;

        return null;
    }
}