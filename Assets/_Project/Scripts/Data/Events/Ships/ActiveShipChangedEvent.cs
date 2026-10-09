/// <summary>
/// Смена активного корабля игрока.
/// </summary>
public readonly struct ActiveShipChangedEvent
{
    /// <summary>Идентификатор корабля.</summary>
    public readonly string ShipId;

    public ActiveShipChangedEvent(string shipId)
    {
        ShipId = shipId;
    }
}