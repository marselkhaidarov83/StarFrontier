/// <summary>
/// Вход игрока на планету/в планетарное состояние интерфейса.
/// </summary>
public sealed class PlanetEnteredEvent
{
    /// <summary>Идентификатор планеты.</summary>
    public string PlanetId { get; }

    public PlanetEnteredEvent(string planetId)
    {
        PlanetId = planetId;
    }
}