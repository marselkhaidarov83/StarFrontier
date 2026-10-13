/// <summary>
/// Выбор станции как цели/объекта взаимодействия.
/// </summary>
public sealed class StationSelectedEvent
{
    /// <summary>Ссылка на данные станции.</summary>
    public StationConfig Station { get; }

    public StationSelectedEvent(StationConfig station)
    {
        Station = station;
    }
}
