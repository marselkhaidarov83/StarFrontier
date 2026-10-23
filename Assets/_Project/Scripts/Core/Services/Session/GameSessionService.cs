using UnityEngine;

public class GameSessionService : IGameSessionService
{
    public GameRuntimeState State { get; private set; }

    public bool HasActiveSession => State != null;

    private ISystemTravelService _systemTravelService;

    public GameSessionService()
    {
    }

    public void StartNewSession(GameRuntimeState state)
    {
        State = state;
        InitializeMissionSession();
        ForcePauseOnSessionOpen();
        InitializeSystemTravelService();
        PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.NewGame, true);
    }

    public void LoadSession(GameRuntimeState state)
    {
        State = state;
        InitializeMissionSession();
        ForcePauseOnSessionOpen();
        InitializeSystemTravelService();
        PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.ContinueGame, true);
    }

    private void InitializeMissionSession()
    {
        if (State == null) return;
        State.MissionBlock ??= new RuntimeMissionSaveBlock();
        int completedBefore = (State.MissionBlock.CompletedMissions?.Count ?? 0) +
            (State.MissionBlock.ActiveMissions?.FindAll(item => item != null && item.Status == MissionStatus.Completed).Count ?? 0) +
            (State.MissionBlock.AvailableMissions?.FindAll(item => item != null && item.Status == MissionStatus.Completed).Count ?? 0);
        MissionLifecycleRules.Normalize(State.MissionBlock, State.Meta?.CurrentGameDay ?? 1);
        if (completedBefore > 0)
            AppLog.Info(DebugLogChannel.Mission,
                "[MissionLifecycle] Completed records removed on session initialization | Count=" + completedBefore +
                " | RewardIds=" + State.MissionBlock.RewardGrantedMissionIds.Count);
        if (Bootstrapper.Instance.ServiceRegistry.TryGet<IRewardService>(out var rewards))
            rewards.ImportGrantedMissionIds(State.MissionBlock.RewardGrantedMissionIds);
        AppLog.Info(DebugLogChannel.Mission,
            "[MissionLifecycle] Session restored | Active=" + State.MissionBlock.ActiveMissions.Count +
            " | Archived=" + State.MissionBlock.ArchivedMissions.Count +
            " | RewardIds=" + State.MissionBlock.RewardGrantedMissionIds.Count);
    }

    private void InitializeSystemTravelService()
    {
        _systemTravelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();
        _systemTravelService.State.SetCurrentPosition(State.Player.SystemMapShipPosition);
    }

    public void ClearSession()
    {
        State = null;
    }

    private void ForcePauseOnSessionOpen()
    {
        if (State?.Meta != null)
            State.Meta.IsGameTimePaused = true;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        if (Bootstrapper.Instance.ServiceRegistry.TryGet<IGameTimeService>(
                out IGameTimeService gameTimeService))
        {
            gameTimeService.SetPaused(true);
        }
    }
}
