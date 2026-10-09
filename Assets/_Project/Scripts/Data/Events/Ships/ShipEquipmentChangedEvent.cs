/// <summary>
/// Изменение установленного на корабле оборудования/модулей.
/// </summary>
public readonly struct ShipEquipmentChangedEvent
{
    /// <summary>Идентификатор корабля.</summary>
    public readonly string ShipId;

    public ShipEquipmentChangedEvent(string shipId)
    {
        ShipId = shipId;
    }
}