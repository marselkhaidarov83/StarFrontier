using System;
using System.Collections.Generic;

public class RewardService : IRewardService
{
    private readonly HashSet<string> _grantedMissionRewards = new(StringComparer.Ordinal);
    private RuntimeMissionSaveBlock _boundBlock;
    private string _lastRewardMessage = "No rewards granted yet.";

    public bool TryGrantMissionReward(MissionInstanceData mission, PlayerState profile)
    {
        using (var observation = PlayerAnalyticsSafe.Observe(PlayerAnalyticsAction.MissionReward, mission?.MissionRuntimeId))
        {
            bool result = TryGrantMissionRewardCore(mission, profile, out string reason);
            observation.Complete(result, reason);
            return result;
        }
    }

    private bool TryGrantMissionRewardCore(MissionInstanceData mission, PlayerState profile, out string reason)
    {
        reason = "None";
        BindCurrentSession();
        if (mission == null || string.IsNullOrWhiteSpace(mission.MissionRuntimeId)) reason = "MissingMission";
        else if (profile == null) reason = "MissingPlayer";
        else if (_boundBlock == null) reason = "MissingSession";
        else if (mission.Reward == null || mission.Reward.Credits < 0 || mission.Reward.Xp < 0) reason = "InvalidReward";
        else if (!MissionLifecycleRules.IsPlayableType(mission.MissionType) ||
                 mission.Status != MissionStatus.ReadyToComplete || !mission.IsReadyToTurnIn ||
                 !_boundBlock.ActiveMissions.Contains(mission)) reason = "InvalidMissionState";
        else if (_grantedMissionRewards.Contains(mission.MissionRuntimeId) ||
                 _boundBlock.RewardGrantedMissionIds.Contains(mission.MissionRuntimeId)) reason = "AlreadyGranted";

        if (reason != "None") return Reject(mission, reason);
        if (!Bootstrapper.Instance.ServiceRegistry.TryGet<IGameSessionService>(out var session) ||
            !ReferenceEquals(session.State?.Player, profile))
        {
            reason = "PlayerSessionMismatch";
            return Reject(mission, reason);
        }

        // Validate both additions before changing either balance.
        long credits = (long)profile.Credits + mission.Reward.Credits;
        long xp = (long)profile.Experience + mission.Reward.Xp;
        if (credits > int.MaxValue || xp > int.MaxValue)
        {
            reason = "RewardOverflow";
            return Reject(mission, reason);
        }

        // The save ledger and balances change in the same save-suspended mission operation.
        _boundBlock.RewardGrantedMissionIds.Add(mission.MissionRuntimeId);
        _grantedMissionRewards.Add(mission.MissionRuntimeId);
        int previousCredits = profile.Credits;
        profile.Credits = (int)credits;
        profile.Experience = (int)xp;
        _lastRewardMessage = "[MissionReward] Granted | Id=" + mission.MissionRuntimeId +
            " | Credits=" + mission.Reward.Credits + " | XP=" + mission.Reward.Xp +
            " | WalletBefore=" + previousCredits + " | WalletAfter=" + profile.Credits;
        AppLog.Info(DebugLogChannel.Mission, _lastRewardMessage);
        return true;
    }

    public List<string> ExportGrantedMissionIds()
    {
        BindCurrentSession();
        return _boundBlock == null ? new List<string>(_grantedMissionRewards) :
            new List<string>(_boundBlock.RewardGrantedMissionIds);
    }

    public void ImportGrantedMissionIds(List<string> ids)
    {
        // Copy first: ids may be the same list that belongs to the current save block.
        var copy = ids == null ? new List<string>() : new List<string>(ids);
        _boundBlock = GetCurrentBlock();
        _grantedMissionRewards.Clear();
        foreach (var id in copy)
            if (!string.IsNullOrWhiteSpace(id)) _grantedMissionRewards.Add(id);
        if (_boundBlock != null)
            _boundBlock.RewardGrantedMissionIds = new List<string>(_grantedMissionRewards);
        _lastRewardMessage = "[MissionReward] Imported | Count=" + _grantedMissionRewards.Count;
        AppLog.Info(DebugLogChannel.Mission, _lastRewardMessage);
    }

    private void BindCurrentSession()
    {
        var current = GetCurrentBlock();
        if (ReferenceEquals(current, _boundBlock)) return;
        _boundBlock = current;
        _grantedMissionRewards.Clear();
        if (current != null)
            foreach (var id in current.RewardGrantedMissionIds)
                if (!string.IsNullOrWhiteSpace(id)) _grantedMissionRewards.Add(id);
        AppLog.Info(DebugLogChannel.Mission, "[MissionReward] Session changed | LedgerCount=" + _grantedMissionRewards.Count);
    }

    private static RuntimeMissionSaveBlock GetCurrentBlock()
    {
        if (Bootstrapper.Instance == null || Bootstrapper.Instance.ServiceRegistry == null ||
            !Bootstrapper.Instance.ServiceRegistry.TryGet<IGameSessionService>(out var session) || session.State == null)
            return null;
        session.State.MissionBlock ??= new RuntimeMissionSaveBlock();
        MissionLifecycleRules.EnsureCollections(session.State.MissionBlock);
        return session.State.MissionBlock;
    }

    private bool Reject(MissionInstanceData mission, string reason)
    {
        _lastRewardMessage = "[MissionReward] Rejected | Id=" + mission?.MissionRuntimeId + " | Reason=" + reason;
        AppLog.Warning(DebugLogChannel.Mission, _lastRewardMessage);
        return false;
    }

    public string GetLastRewardMessage() => _lastRewardMessage;

    public void Clear()
    {
        _boundBlock = null;
        _grantedMissionRewards.Clear();
        _lastRewardMessage = "[MissionReward] Runtime cache cleared; saved ledger preserved.";
        AppLog.Info(DebugLogChannel.Mission, _lastRewardMessage);
    }
}
