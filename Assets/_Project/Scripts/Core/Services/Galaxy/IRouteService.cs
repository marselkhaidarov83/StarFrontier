    public interface IRouteService
    {
        RouteState GetRoute(string routeId);
        bool CanTravelTo(string targetSystemId);
        bool TravelTo(string targetSystemId);
    }