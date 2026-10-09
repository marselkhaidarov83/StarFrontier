/// <summary>
/// Нанесение урона вражескому кораблю; сигнал обновления его состояния.
/// </summary>
public readonly struct SystemEnemyDamagedEvent
    {
        /// <summary>Идентификатор врага в runtime-состоянии.</summary>
        public readonly string RuntimeEnemyId;
        /// <summary>Нанесённый или полученный урон.</summary>
        public readonly int Damage;
        /// <summary>Прочность корпуса после изменения.</summary>
        public readonly int CurrentHull;
        /// <summary>Значение щита после изменения.</summary>
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