/// <summary>
/// Начало межсистемного перелёта; сигнал службам навигации.
/// </summary>
public sealed class TravelStartedEvent
{
    /// <summary>Идентификатор исходной системы.</summary>
    public string FromSystemId { get; }
    /// <summary>Идентификатор системы назначения.</summary>
    public string ToSystemId { get; }

    public TravelStartedEvent(string fromSystemId, string toSystemId)
    {
        FromSystemId = fromSystemId;
        ToSystemId = toSystemId;
    }
}