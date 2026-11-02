/// <summary>Detached before/after snapshots; observers cannot mutate live state through the event.</summary>
public readonly struct StationStateChangedEvent
{
    public readonly string SystemId;
    public readonly string Reason;
    private readonly StationRuntimeState _previous;
    private readonly StationRuntimeState _current;
    public StationRuntimeState Previous => _previous?.Copy();
    public StationRuntimeState Current => _current?.Copy();

    public StationStateChangedEvent(string systemId, string reason,
        StationRuntimeState previous, StationRuntimeState current)
    {
        SystemId = systemId;
        Reason = reason;
        _previous = previous?.Copy();
        _current = current?.Copy();
    }
}
