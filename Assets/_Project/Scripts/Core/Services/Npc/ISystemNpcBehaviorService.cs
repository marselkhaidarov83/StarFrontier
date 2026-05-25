public interface ISystemNpcBehaviorService
    {
        void Tick(StarSystemConfig starSystem, int currentTick);
        // void TickAllOpenSystems(string[] openSystemIds, int currentTick);

        void AssignBehavior(SystemNpcRuntimeState npc, int currentTick);
        void CompleteBehavior(SystemNpcRuntimeState npc, int currentTick);

        void ClearBehavior(SystemNpcRuntimeState npc);
    }