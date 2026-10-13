/// <summary>
/// Разрешение/завершение столкновения; сигнал об итоговом состоянии encounter.
/// </summary>
public readonly struct SystemEncounterResolvedEvent
    {
        /// <summary>Идентификатор боевого столкновения.</summary>
        public readonly string EncounterId;
        /// <summary>Идентификатор звёздной системы.</summary>
        public readonly string SystemId;

        public SystemEncounterResolvedEvent(string encounterId, string systemId)
        {
            EncounterId = encounterId;
            SystemId = systemId;
        }
    }