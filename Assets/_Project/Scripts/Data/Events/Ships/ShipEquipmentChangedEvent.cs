public readonly struct ShipEquipmentChangedEvent
{
    public readonly string ShipId;

    public ShipEquipmentChangedEvent(string shipId)
    {
        ShipId = shipId;
    }
}