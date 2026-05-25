public readonly struct ShipRepairedEvent
{
    public readonly string ShipId;

    public ShipRepairedEvent(string shipId)
    {
        ShipId = shipId;
    }
}