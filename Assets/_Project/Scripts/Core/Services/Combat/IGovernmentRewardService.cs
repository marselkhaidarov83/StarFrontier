public interface IGovernmentRewardService
    {
        bool CanClaimReward(string currentSystemId, bool isCurrentPlanetInhabited);

        GovernmentRewardResult ClaimReward(
            string currentSystemId,
            bool isCurrentPlanetInhabited);
    }