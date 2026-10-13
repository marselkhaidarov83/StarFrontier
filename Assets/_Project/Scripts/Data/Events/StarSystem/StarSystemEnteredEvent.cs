/// <summary>
/// Вход в звёздную систему; обновление миссий, NPC, камеры, HUD и времени.
/// </summary>
public sealed class StarSystemEnteredEvent
{
    /// <summary>Идентификатор звёздной системы.</summary>
    public string SystemId { get; }

    public StarSystemEnteredEvent(string systemId)
    {
        SystemId = systemId;
    }
}