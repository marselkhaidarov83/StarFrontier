public sealed class PlanetEnteredEvent
{
    public string PlanetId { get; }

    public PlanetEnteredEvent(string planetId)
    {
        PlanetId = planetId;
    }
}