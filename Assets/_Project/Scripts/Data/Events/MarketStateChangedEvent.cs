public readonly struct MarketStateChangedEvent
{
    public readonly string SystemId;
    public readonly string PlanetId;
    public MarketStateChangedEvent(string systemId, string planetId) { SystemId = systemId; PlanetId = planetId; }
}
