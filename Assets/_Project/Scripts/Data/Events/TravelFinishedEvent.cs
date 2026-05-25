public sealed class TravelFinishedEvent
{
    public string FromSystemId { get; }
    public string ToSystemId { get; }
    public bool Success { get; }
    public int FuelSpent { get; }
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