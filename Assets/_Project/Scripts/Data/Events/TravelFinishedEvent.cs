/// <summary>
/// Завершение межсистемного перелёта; сигнал службам навигации.
/// </summary>
public sealed class TravelFinishedEvent
{
    /// <summary>Идентификатор исходной системы.</summary>
    public string FromSystemId { get; }
    /// <summary>Идентификатор системы назначения.</summary>
    public string ToSystemId { get; }
    /// <summary>Признак успешного результата.</summary>
    public bool Success { get; }
    /// <summary>Количество израсходованного топлива.</summary>
    public int FuelSpent { get; }
    /// <summary>Причина неуспешного выполнения действия.</summary>
    public TravelFailReason FailReason { get; }

    public TravelFinishedEvent(
        string fromSystemId,
        string toSystemId,
        bool success,
        int fuelSpent,
        TravelFailReason failReason)
    {
        FromSystemId = fromSystemId;
        ToSystemId = toSystemId;
        Success = success;
        FuelSpent = fuelSpent;
        FailReason = failReason;
    }
}