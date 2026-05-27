public readonly struct GalaxyGeneratedEvent
{
    public readonly int SectorCount;
    public readonly int SystemCount;
    public readonly int RouteCount;

    public GalaxyGeneratedEvent(int sectorCount, int systemCount, int routeCount)
    {
        SectorCount = sectorCount;
        SystemCount = systemCount;
        RouteCount = routeCount;
    }
}