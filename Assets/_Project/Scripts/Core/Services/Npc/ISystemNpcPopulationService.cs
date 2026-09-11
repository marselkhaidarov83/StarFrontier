using System.Collections.Generic;

public interface ISystemNpcPopulationService
{
    SystemPopulationRuntimeState RuntimeState { get; }

    void Tick(StarSystemConfig starSystem, float deltaTime);

    void EnsureMinimumAlliesInSystem(StarSystemConfig starSystem);

    int ScheduleRespawn(SystemNpcRuntimeState npc, int destroyedAtTick);

    void ProcessOfflinePopulation(
        IReadOnlyList<StarSystemConfig> starSystems,
        double offlineHours);

    void ClearRuntimeState();

    string CreatePirateGroup(PirateGroupSpawnRuleConfig rule);

    bool DebugSpawnEnemyAttackGroupInCurrentSystem();

    bool DebugSpawnAllyInCurrentSystem(AllyRole2A role);
    bool DebugSpawnEnemyInCurrentSystem();
}