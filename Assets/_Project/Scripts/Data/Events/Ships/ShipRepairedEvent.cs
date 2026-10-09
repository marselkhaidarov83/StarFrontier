/// <summary>
/// Восстановление/ремонт корабля.
/// </summary>
public readonly struct ShipRepairedEvent
{
    /// <summary>Идентификатор корабля.</summary>
    public readonly string ShipId;

    public ShipRepairedEvent(string shipId)
    {
        ShipId = shipId;
    }
}