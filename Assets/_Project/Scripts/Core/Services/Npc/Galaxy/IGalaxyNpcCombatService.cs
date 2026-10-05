public interface IGalaxyNpcCombatService
{
    double LastCurrentSystemMs { get; }
    double LastOffscreenMs { get; }
    double LastTotalMs { get; }

    void TickQuant(int quantTick);
    void Tick(float deltaTime, int quantTick);
}