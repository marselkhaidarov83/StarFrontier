public sealed class StationCardRequestedEvent
{
    public readonly StationConfig Station;
    public StationCardRequestedEvent(StationConfig station) { Station = station; }
}
