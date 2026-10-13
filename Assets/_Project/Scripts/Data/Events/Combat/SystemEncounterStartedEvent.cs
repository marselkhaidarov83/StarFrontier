/// <summary>
/// Начало боевого столкновения в системе.
/// </summary>
public readonly struct SystemEncounterStartedEvent
    {
        /// <summary>Идентификатор боевого столкновения.</summary>
        public readonly string EncounterId;
        /// <summary>Идентификатор звёздной системы.</summary>
        public readonly string SystemId;
        /// <summary>Количество оставшихся живых врагов.</summary>
        public readonly int EnemiesAlive;
        /// <summary>Количество живых союзников.</summary>
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