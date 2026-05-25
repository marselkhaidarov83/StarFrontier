using System.Collections.Generic;

using UnityEngine;


public class RewardService : IRewardService
{
    private readonly HashSet<string> _grantedMissionRewards = new();
    private string _lastRewardMessage = "No rewards granted yet.";

    public bool TryGrantMissionReward(MissionInstanceData mission, PlayerProfileData profile)
    {
        if (mission == null)
        {
            _lastRewardMessage = "Reward failed: mission is null.";
            Debug.LogWarning(_lastRewardMessage);
            return false;
        }

        if (profile == null)
        {
            _lastRewardMessage = "Reward failed: profile is null.";
            Debug.LogWarning(_lastRewardMessage);
            return false;
        }

        if (mission.Reward == null)
        {
            _lastRewardMessage = $"Reward failed: mission reward is null for {mission.Title}.";
            Debug.LogWarning(_lastRewardMessage);
            return false;
        }

        if (_grantedMissionRewards.Contains(mission.MissionRuntimeId))
        {
            _lastRewardMessage = $"Reward skipped: already granted for {mission.Title}.";
            Debug.Log(_lastRewardMessage);
            return false;
        }

        profile.Credits += mission.Reward.Credits;
        profile.Experience += mission.Reward.Xp;

        _grantedMissionRewards.Add(mission.MissionRuntimeId);

        _lastRewardMessage = $"Granted reward for {mission.Title}: +{mission.Reward.Credits} credits, +{mission.Reward.Xp} XP";
        Debug.Log(_lastRewardMessage);

        return true;
    }

    public List<string> ExportGrantedMissionIds()
    {
        return new List<string>(_grantedMissionRewards);
    }

    public void ImportGrantedMissionIds(List<string> ids)
    {
        _grantedMissionRewards.Clear();

        if (ids == null)
        {
            _lastRewardMessage = "Imported empty reward grant state.";
            return;
        }

        foreach (string id in ids)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                _grantedMissionRewards.Add(id);
            }
        }

        _lastRewardMessage = $"Imported reward grant state: {_grantedMissionRewards.Count} entries.";
    }

    public string GetLastRewardMessage()
    {
        return _lastRewardMessage;
    }

    public void Clear()
    {
        _grantedMissionRewards.Clear();
        _lastRewardMessage = "Reward service cleared.";
    }
}