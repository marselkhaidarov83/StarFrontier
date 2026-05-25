 public readonly struct SystemEncounterDefeatedEvent
    {
        public readonly string EncounterId;
        public readonly string SystemId;
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