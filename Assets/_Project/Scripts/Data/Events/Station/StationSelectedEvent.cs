public sealed class StationSelectedEvent
{
    public StationConfig Station { get; }

    public StationSelectedEvent(StationConfig station)
    {
        Station = station;
    }
}
