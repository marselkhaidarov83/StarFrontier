/// <summary>
/// Получение урона союзным кораблём; сигнал обновления боевого состояния.
/// </summary>
public readonly struct SystemAllyDamagedEvent
    {
        /// <summary>Идентификатор союзника в runtime-состоянии.</summary>
        public readonly string RuntimeAllyId;
        /// <summary>Нанесённый или полученный урон.</summary>
        public readonly int Damage;
        /// <summary>Прочность корпуса после изменения.</summary>
        public readonly int CurrentHull;
        /// <summary>Значение щита после изменения.</summary>
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
