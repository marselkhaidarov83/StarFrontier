/// <summary>
/// Вход игрока в текущую звёздную систему; оповещение связанных контуров навигации.
/// </summary>
public sealed class CurrentSystemEnteredEvent
{
    /// <summary>Идентификатор звёздной системы.</summary>
    public string SystemId { get; }

    public CurrentSystemEnteredEvent(string systemId)
    {
        SystemId = systemId;
    }
}
