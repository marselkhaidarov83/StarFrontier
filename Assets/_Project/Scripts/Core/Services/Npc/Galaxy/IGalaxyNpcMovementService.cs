public interface IGalaxyNpcMovementService
{
    double LastCurrentSystemMs { get; }
    double LastOffscreenMs { get; }
    double LastTotalMs { get; }

    void Tick(float deltaTime, int quantTick);
}