public interface ISystemNpcPopulationService
{
    SystemPopulationRuntimeState RuntimeState { get; }

    void Tick(StarSystemConfig starSystem, float deltaTime);
    int ScheduleRespawn(SystemNpcRuntimeState npc, int destroyedAtTick);
    // void ForcePopulateSystem(StarSystemConfig starSystem);
    void ClearRuntimeState();
    string CreatePirateGroup(PirateGroupSpawnRuleConfig rule);
}