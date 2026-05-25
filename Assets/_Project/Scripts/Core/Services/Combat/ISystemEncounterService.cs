public interface ISystemEncounterService
    {
        ActiveSystemEncounter Current { get; }

        bool HasActiveEncounter { get; }
        bool HasPendingGovernmentReward { get; }

        void StartEncounter(string encounterId, string systemId, int enemyCount, int allyCount);
        void RestoreEncounter(ActiveSystemEncounter encounter);

        void RegisterEnemyDestroyed(bool killedByPlayer);
        void RegisterAllyDestroyed();
        void RegisterPlayerDestroyed();

        void RegisterPlayerEnteredPlanet();
        void RegisterPlayerLaunchedFromPlanet();
        void RegisterPlanetDayPassed();

        void RegisterPlayerLeftSystem(string fromSystemId);

        bool TryResolvePendingReward();

        void ClearEncounter();

        void DebugPrintCurrentState();
    }