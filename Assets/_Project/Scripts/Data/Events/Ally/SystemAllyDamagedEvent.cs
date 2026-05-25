public readonly struct SystemAllyDamagedEvent
    {
        public readonly string RuntimeAllyId;
        public readonly int Damage;
        public readonly int CurrentHull;
        public readonly int CurrentShield;

        public SystemAllyDamagedEvent(
            string runtimeAllyId,
            int damage,
            int currentHull,
            int currentShield)
        {
            RuntimeAllyId = runtimeAllyId;
            Damage = damage;
            CurrentHull = currentHull;
            CurrentShield = currentShield;
        }
    }
