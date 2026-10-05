public interface IGalaxyNpcBehaviorService
{
    double LastCurrentSystemMs { get; }
    double LastOffscreenMs { get; }
    double LastTotalMs { get; }

    void Tick(int quantTick);
}