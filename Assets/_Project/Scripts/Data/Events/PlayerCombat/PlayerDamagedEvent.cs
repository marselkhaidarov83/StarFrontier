/// <summary>
/// Получение урона кораблём игрока; обновление состояния и интерфейса боя.
/// </summary>
public readonly struct PlayerDamagedEvent
    {
        /// <summary>Нанесённый или полученный урон.</summary>
        public readonly int Damage;
        /// <summary>Прочность корпуса после изменения.</summary>
        public readonly int CurrentHull;
        /// <summary>Значение щита после изменения.</summary>
        public readonly int CurrentShield;

        public PlayerDamagedEvent(int damage, int currentHull, int currentShield)
        {
            Damage = damage;
            CurrentHull = currentHull;
            CurrentShield = currentShield;
        }
    }
