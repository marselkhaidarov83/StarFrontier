/// <summary>
/// Изменение боевых характеристик игрока, в том числе данных для HUD.
/// </summary>
public readonly struct PlayerCombatStatsChangedEvent
    {
        /// <summary>Прочность корпуса после изменения.</summary>
        public readonly int CurrentHull;
        /// <summary>Максимальная прочность корпуса.</summary>
        public readonly int MaxHull;
        /// <summary>Значение щита после изменения.</summary>
        public readonly int CurrentShield;
        /// <summary>Максимальное значение щита.</summary>
        public readonly int MaxShield;
        /// <summary>Текущее значение энергии объекта.</summary>
        public readonly int CurrentEnergy;
        /// <summary>Максимальное значение энергии объекта.</summary>
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