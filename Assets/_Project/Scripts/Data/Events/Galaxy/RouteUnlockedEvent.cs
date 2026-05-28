public readonly struct RouteUnlockedEvent
{
    public readonly string RouteId;

    public RouteUnlockedEvent(string routeId)
    {
        RouteId = routeId;
    }
}