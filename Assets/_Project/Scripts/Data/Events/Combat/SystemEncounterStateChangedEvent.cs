public readonly struct SystemEncounterStateChangedEvent
    {
        public readonly string EncounterId;
        public readonly string SystemId;
        public readonly SystemEncounterState NewState;
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