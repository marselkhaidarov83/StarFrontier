public readonly struct SystemEnemyDestroyedEvent
    {
        public readonly string RuntimeEnemyId;
        public readonly string EnemyConfigId;
        public readonly string SystemId;
        public readonly bool KilledByPlayer;

        public SystemEnemyDestroyedEvent(
            string runtimeEnemyId,
            string enemyConfigId,
            string systemId,
            bool killedByPlayer)
        {
            RuntimeEnemyId = runtimeEnemyId;
            EnemyConfigId = enemyConfigId;
            SystemId = systemId;
            KilledByPlayer = killedByPlayer;
        }
    }