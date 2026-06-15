public readonly struct GalaxyTravelRequestedEvent
{
    public readonly string FromSystemId;
    public readonly string ToSystemId;
    public readonly string RouteId;

    public GalaxyTravelRequestedEvent(
        string fromSystemId,
        string toSystemId,
        string routeId)
    {
        FromSystemId = fromSystemId;
        ToSystemId = toSystemId;
        RouteId = routeId;
    }
}