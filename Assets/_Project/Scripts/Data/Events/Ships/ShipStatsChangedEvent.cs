public readonly struct ShipStatsChangedEvent
{
    public readonly string ShipId;

    public ShipStatsChangedEvent(string shipId)
    {
        ShipId = shipId;
    }
}