public sealed class TravelStartedEvent
{
    public string FromSystemId { get; }
    public string ToSystemId { get; }

    public TravelStartedEvent(string fromSystemId, string toSystemId)
    {
        FromSystemId = fromSystemId;
        ToSystemId = toSystemId;
    }
}