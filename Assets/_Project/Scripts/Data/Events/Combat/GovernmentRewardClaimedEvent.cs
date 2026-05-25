public readonly struct GovernmentRewardClaimedEvent
    {
        public readonly string EncounterId;
        public readonly string SystemId;
        public readonly int Credits;
        public readonly int Xp;

        public GovernmentRewardClaimedEvent(
            string encounterId,
            string systemId,
            int credits,
            int xp)
        {
            EncounterId = encounterId;
            SystemId = systemId;
            Credits = credits;
            Xp = xp;
        }
    }