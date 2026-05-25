public readonly struct PlayerCombatStatsChangedEvent
    {
        public readonly int CurrentHull;
        public readonly int MaxHull;
        public readonly int CurrentShield;
        public readonly int MaxShield;
        public readonly int CurrentEnergy;
        public readonly int MaxEnergy;

        public PlayerCombatStatsChangedEvent(
            int currentHull,
            int maxHull,
            int currentShield,
            int maxShield,
            int currentEnergy,
            int maxEnergy)
        {
            CurrentHull = currentHull;
            MaxHull = maxHull;
            CurrentShield = currentShield;
            MaxShield = maxShield;
            CurrentEnergy = currentEnergy;
            MaxEnergy = maxEnergy;
        }
    }