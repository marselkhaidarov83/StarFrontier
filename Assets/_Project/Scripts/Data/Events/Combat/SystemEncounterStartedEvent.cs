public readonly struct SystemEncounterStartedEvent
    {
        public readonly string EncounterId;
        public readonly string SystemId;
        public readonly int EnemiesAlive;
        public readonly int AlliesAlive;

        public SystemEncounterStartedEvent(
            string encounterId,
            string systemId,
            int enemiesAlive,
            int alliesAlive)
        {
            EncounterId = encounterId;
            SystemId = systemId;
            EnemiesAlive = enemiesAlive;
            AlliesAlive = alliesAlive;
        }
    }