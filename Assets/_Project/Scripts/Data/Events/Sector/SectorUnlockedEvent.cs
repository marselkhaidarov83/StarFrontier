/// <summary>
/// Открытие сектора галактики для игрока.
/// </summary>
public readonly struct SectorUnlockedEvent
{
    /// <summary>Идентификатор сектора.</summary>
    public readonly string SectorId;

    public SectorUnlockedEvent(string sectorId)
    {
        SectorId = sectorId;
    }
}