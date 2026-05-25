public sealed class PlanetSelectedEvent
{
    public PlanetConfig Planet { get; }

    public PlanetSelectedEvent(PlanetConfig planet)
    {
        Planet = planet;
    }
}