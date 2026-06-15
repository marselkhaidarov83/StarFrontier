public interface IRouteService
{
    bool IsRouteUnlocked(string routeId);
    bool HasUnlockedRoute(string fromSystemId, string toSystemId);
    string FindRouteId(string fromSystemId, string toSystemId);
    void UnlockRoute(string routeId);
}