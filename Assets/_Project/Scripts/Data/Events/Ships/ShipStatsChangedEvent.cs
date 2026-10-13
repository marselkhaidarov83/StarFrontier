/// <summary>
/// Изменение характеристик корабля; по контракту содержит ID корабля.
/// </summary>
public readonly struct ShipStatsChangedEvent
{
    /// <summary>Идентификатор корабля.</summary>
    public readonly string ShipId;

    public ShipStatsChangedEvent(string shipId)
    {
        ShipId = shipId;
    }
}