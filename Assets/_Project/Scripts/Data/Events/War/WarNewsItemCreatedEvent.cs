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

/// <summary>
/// Создана новость о военной ситуации; вид новости, система, фракция, заголовок, текст, тик и приоритет.
/// </summary>
public readonly struct WarNewsItemCreatedEvent
{
    /// <summary>Вид военной новости (WarNewsEventKind).</summary>
    public readonly WarNewsEventKind Kind;
    /// <summary>Идентификатор звёздной системы.</summary>
    public readonly string SystemId;
    /// <summary>Идентификатор фракции.</summary>
    public readonly string FactionId;
    /// <summary>Заголовок новости.</summary>
    public readonly string Title;
    /// <summary>Текст новости.</summary>
    public readonly string Body;
    /// <summary>Номер игрового тика, когда создана новость.</summary>
    public readonly int CreatedAtTick;
    /// <summary>Приоритет новости.</summary>
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