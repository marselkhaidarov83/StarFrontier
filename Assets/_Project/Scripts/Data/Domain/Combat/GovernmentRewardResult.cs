public readonly struct GovernmentRewardResult
    {
        public readonly bool Success;
        public readonly int Credits;
        public readonly int Xp;
        public readonly string Reason;

        public GovernmentRewardResult(bool success, int credits, int xp, string reason)
        {
            Success = success;
            Credits = credits;
            Xp = xp;
            Reason = reason;
        }

        public static GovernmentRewardResult Failed(string reason)
        {
            return new GovernmentRewardResult(false, 0, 0, reason);
        }

        public static GovernmentRewardResult Granted(int credits, int xp)
        {
            return new GovernmentRewardResult(true, credits, xp, "Reward granted.");
        }
    }