/// <summary>
/// Изменение состояния столкновения; сообщает новое состояние и причину поражения, если она есть.
/// </summary>
public readonly struct SystemEncounterStateChangedEvent
    {
        /// <summary>Идентификатор боевого столкновения.</summary>
        public readonly string EncounterId;
        /// <summary>Идентификатор звёздной системы.</summary>
        public readonly string SystemId;
        /// <summary>Новое состояние после изменения.</summary>
        public readonly SystemEncounterState NewState;
        /// <summary>Причина поражения в столкновении.</summary>
        public readonly SystemEncounterDefeatReason DefeatReason;

        public SystemEncounterStateChangedEvent(
            string encounterId,
            string systemId,
            SystemEncounterState newState,
            SystemEncounterDefeatReason defeatReason)
        {
            EncounterId = encounterId;
            SystemId = systemId;
            NewState = newState;
            DefeatReason = defeatReason;
        }
    }