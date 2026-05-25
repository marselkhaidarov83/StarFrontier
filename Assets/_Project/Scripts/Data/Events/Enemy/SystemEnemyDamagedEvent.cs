public readonly struct SystemEnemyDamagedEvent
    {
        public readonly string RuntimeEnemyId;
        public readonly int Damage;
        public readonly int CurrentHull;
        public readonly int CurrentShield;

        public SystemEnemyDamagedEvent(
            string runtimeEnemyId,
            int damage,
            int currentHull,
            int currentShield)
        {
            RuntimeEnemyId = runtimeEnemyId;
            Damage = damage;
            CurrentHull = currentHull;
            CurrentShield = currentShield;
        }
    }