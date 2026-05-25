public readonly struct SystemEncounterVictoryPendingRewardEvent
    {
        public readonly string EncounterId;
        public readonly string SystemId;
        public readonly int PlayerKills;

        public SystemEncounterVictoryPendingRewardEvent(
            string encounterId,
            string systemId,
            int playerKills)
        {
            EncounterId = encounterId;
            SystemId = systemId;
            PlayerKills = playerKills;
        }
    }