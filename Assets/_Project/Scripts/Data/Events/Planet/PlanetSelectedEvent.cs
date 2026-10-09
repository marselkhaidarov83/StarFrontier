/// <summary>
/// Выбор планеты как объекта интереса или назначения.
/// </summary>
public sealed class PlanetSelectedEvent
{
    /// <summary>Ссылка на данные планеты, относящейся к событию.</summary>
    public PlanetConfig Planet { get; }

    public PlanetSelectedEvent(PlanetConfig planet)
    {
        Planet = planet;
    }
}