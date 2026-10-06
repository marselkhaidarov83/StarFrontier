public enum WarNewsEventKind
{
    InvasionStarted = 0,
    SystemCaptured = 1,
    InfrastructureDamaged = 2,
    InfrastructureDestroyed = 3,
    SystemLiberated = 4,
    InvasionCancelled = 5,
    FrontChanged = 6
}

public readonly struct WarNewsItemCreatedEvent
{
    public readonly WarNewsEventKind Kind;
    public readonly string SystemId;
    public readonly string FactionId;
    public readonly string Title;
    public readonly string Body;
    public readonly int CreatedAtTick;
    public readonly int Priority;

    public WarNewsItemCreatedEvent(
        WarNewsEventKind kind,
        string systemId,
        string factionId,
        string title,
        string body,
        int createdAtTick,
        int priority)
    {
        Kind = kind;
        SystemId = systemId ?? string.Empty;
        FactionId = factionId ?? string.Empty;
        Title = title ?? string.Empty;
        Body = body ?? string.Empty;
        CreatedAtTick = createdAtTick;
        Priority = priority;
    }
}