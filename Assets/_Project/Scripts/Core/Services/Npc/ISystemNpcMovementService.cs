public interface ISystemNpcMovementService
    {
        void Tick(StarSystemConfig starSystem, float deltaTime, int currentTick);
        // void TickAllOpenSystems(string[] openSystemIds, float deltaTime, int currentTick);
    }