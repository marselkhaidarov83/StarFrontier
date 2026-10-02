public interface ISystemNpcOffscreenSimulationService
{
    void Tick(
        StarSystemConfig starSystem,
        int currentTick,
        bool forceUnlimitedCompletion);
}