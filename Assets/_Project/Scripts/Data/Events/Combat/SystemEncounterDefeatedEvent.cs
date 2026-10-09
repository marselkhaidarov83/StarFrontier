 /// <summary>
 /// Столкновение завершилось поражением; используется логикой и диагностикой боя.
 /// </summary>
 public readonly struct SystemEncounterDefeatedEvent
    {
        /// <summary>Идентификатор боевого столкновения.</summary>
        public readonly string EncounterId;
        /// <summary>Идентификатор звёздной системы.</summary>
        public readonly string SystemId;
        /// <summary>Причина завершения или поражения.</summary>
        public readonly SystemEncounterDefeatReason Reason;

        public SystemEncounterDefeatedEvent(
            string encounterId,
            string systemId,
            SystemEncounterDefeatReason reason)
        {
            EncounterId = encounterId;
            SystemId = systemId;
            Reason = reason;
        }
    }