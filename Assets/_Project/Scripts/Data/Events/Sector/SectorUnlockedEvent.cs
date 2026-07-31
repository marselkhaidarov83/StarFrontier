public readonly struct SectorUnlockedEvent
{
    public readonly string SectorId;

    public SectorUnlockedEvent(string sectorId)
    {
        SectorId = sectorId;
    }
}