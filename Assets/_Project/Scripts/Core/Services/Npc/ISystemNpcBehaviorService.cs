public interface ISystemNpcBehaviorService
{
    void Tick(StarSystemConfig starSystem, int currentTick);
    void Tick(StarSystemConfig starSystem, int currentTick, bool isDetailedSystem);
    void Tick(
        StarSystemConfig starSystem,
        int currentTick,
        bool isDetailedSystem,
        bool forceInitialWarmup);

    void AssignBehavior(SystemNpcRuntimeState npc, int currentTick);
    void CompleteBehavior(SystemNpcRuntimeState npc, int currentTick);

    void ClearBehavior(SystemNpcRuntimeState npc);
}