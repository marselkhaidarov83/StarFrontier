using System.Linq;

public sealed class GovernmentRewardService : CustomService, IGovernmentRewardService
{
    private IMissionService Missions => Bootstrapper.Instance.ServiceRegistry.Get<IMissionService>();
    private IGameSessionService Session => Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();

    private MissionInstanceData FindReward(string systemId)
    {
        var player = Session.State?.Player;
        if (player == null || player.CurrentSystemId != systemId || string.IsNullOrWhiteSpace(player.CurrentPlanetId)) return null;
        Missions.ReevaluateActiveMissions("GovernmentReward");
        return Missions.GetActiveMissions().FirstOrDefault(m => m != null && MissionBattleRules.IsBattle(m.MissionType) &&
            m.TargetSystemId == systemId && m.Status == MissionStatus.ReadyToComplete && m.IsReadyToTurnIn);
    }

    public bool CanClaimReward(string currentSystemId, bool isCurrentPlanetInhabited) => FindReward(currentSystemId) != null;

    public GovernmentRewardResult ClaimReward(string currentSystemId, bool isCurrentPlanetInhabited)
    {
        using (var observation = PlayerAnalyticsSafe.Observe(PlayerAnalyticsAction.GovernmentReward, currentSystemId))
        {
            var mission = FindReward(currentSystemId);
            bool success = mission != null && Missions.CompleteMission(mission.MissionRuntimeId);
            observation.Complete(success, success ? "None" : "RewardUnavailable");
            return success ? GovernmentRewardResult.Granted(mission.Reward.Credits, mission.Reward.Xp) :
                GovernmentRewardResult.Failed("Reward is not available.");
        }
    }
}
