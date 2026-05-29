public readonly struct PlayerTravelledToSystemEvent
{
    public readonly string FromSystemId;
    public readonly string ToSystemId;

    public PlayerTravelledToSystemEvent(string fromSystemId, string toSystemId)
    {
        FromSystemId = fromSystemId;
        ToSystemId = toSystemId;
    }
}