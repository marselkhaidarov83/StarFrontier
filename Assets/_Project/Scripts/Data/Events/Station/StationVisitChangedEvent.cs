public readonly struct StationVisitChangedEvent
{
    public readonly string SystemId;
    public readonly string StationId;
    public readonly bool Inside;
    public readonly bool ForcedExit;
    public StationVisitChangedEvent(string systemId, string stationId, bool inside, bool forcedExit)
    { SystemId = systemId; StationId = stationId; Inside = inside; ForcedExit = forcedExit; }
}
