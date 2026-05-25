public interface ISystemNpcPopulationService
{
    SystemPopulationRuntimeState RuntimeState { get; }

    void Tick(StarSystemConfig starSystem, float deltaTime);
    // void ForcePopulateSystem(StarSystemConfig starSystem);
    void ClearRuntimeState();
    string CreatePirateGroup(PirateGroupSpawnRuleConfig rule);
}