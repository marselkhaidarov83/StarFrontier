public readonly struct SystemEncounterResolvedEvent
    {
        public readonly string EncounterId;
        public readonly string SystemId;

        public SystemEncounterResolvedEvent(string encounterId, string systemId)
        {
            EncounterId = encounterId;
            SystemId = systemId;
        }
    }