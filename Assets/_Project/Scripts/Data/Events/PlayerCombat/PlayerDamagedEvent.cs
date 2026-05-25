public readonly struct PlayerDamagedEvent
    {
        public readonly int Damage;
        public readonly int CurrentHull;
        public readonly int CurrentShield;

        public PlayerDamagedEvent(int damage, int currentHull, int currentShield)
        {
            Damage = damage;
            CurrentHull = currentHull;
            CurrentShield = currentShield;
        }
    }
