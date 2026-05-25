public readonly struct ActiveShipChangedEvent
{
    public readonly string ShipId;

    public ActiveShipChangedEvent(string shipId)
    {
        ShipId = shipId;
    }
}