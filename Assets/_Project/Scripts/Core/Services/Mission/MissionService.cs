using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MissionService : CustomService, IMissionService
{
    private IGameSessionService _gameSessionService;
    private ISaveService _saveService;
    private IRewardService _rewardService;
    private ISystemNpcPopulationService _systemNpcPopulationService;

    public MissionService()
    {
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _saveService = Bootstrapper.Instance.ServiceRegistry.Get<ISaveService>();
        _rewardService = Bootstrapper.Instance.ServiceRegistry.Get<IRewardService>();
        _systemNpcPopulationService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcPopulationService>();
    }

    public IReadOnlyList<MissionInstanceData> GetAvailableMissions()
    {
        return _gameSessionService.CurrentSave.MissionBlock.AvailableMissions;
    }
    public IReadOnlyList<MissionInstanceData> GetActiveMissions()
    {
        return _gameSessionService.CurrentSave.MissionBlock.ActiveMissions;
    }
    public IReadOnlyList<MissionInstanceData> GetCompletedMissions()
    {
        return _gameSessionService.CurrentSave.MissionBlock.CompletedMissions;
    }
    public void SetAvailableMissions(List<MissionInstanceData> missions)
    {
        _gameSessionService.CurrentSave.MissionBlock.AvailableMissions.Clear();

        if (missions == null)
            return;

        _saveService.EnableSave(false);
        foreach (MissionInstanceData mission in missions)
        {
            if (mission == null)
                continue;

            mission.Status = MissionStatus.Available;
            mission.IsReadyToTurnIn = false;

            if (mission.Objective != null)
            {
                mission.Objective.CurrentAmount = 0;
                mission.Objective.IsMissionCargoGranted = false;
                mission.Objective.IsMissionCargoDelivered = false;
            }

            _gameSessionService.CurrentSave.MissionBlock.AvailableMissions.Add(mission);
        }
        _saveService.EnableSave(true);
    }

    public bool AcceptMission(string missionRuntimeId)
    {
        MissionInstanceData mission = _gameSessionService.CurrentSave.MissionBlock.AvailableMissions.FirstOrDefault(m => m.MissionRuntimeId == missionRuntimeId);

        if (mission == null)
        {
            if (IsDebug())
                Debug.LogWarning($"MissionService: AcceptMission failed. Mission not found: {missionRuntimeId}");
            return false;
        }

        _saveService.EnableSave(false);
        _gameSessionService.CurrentSave.MissionBlock.AvailableMissions.Remove(mission);
        mission.Status = MissionStatus.Accepted;

        if (mission.MissionType == MissionType.Delivery && mission.Objective != null)
        {
            mission.Objective.IsMissionCargoGranted = true;
            mission.Objective.IsMissionCargoDelivered = false;
            mission.IsReadyToTurnIn = false;
        }

        if (mission.MissionType.Equals(MissionType.Elimination))
            mission.PirateGroupNpcId = _systemNpcPopulationService.CreatePirateGroup(mission.PirateGroupSpawnRuleConfig);

        _gameSessionService.CurrentSave.MissionBlock.ActiveMissions.Add(mission);
        _saveService.EnableSave(true);

        LogCustom($"MissionService: Mission accepted: {mission.Title}");
        LogCustom($"MissionService: _availableMissions.Count: {_gameSessionService.CurrentSave.MissionBlock.AvailableMissions.Count}");
        LogCustom($"MissionService: _activeMissions.Count: {_gameSessionService.CurrentSave.MissionBlock.ActiveMissions.Count}");

        return true;
    }

    public bool CompleteMission(string missionRuntimeId)
    {
        MissionInstanceData mission = _gameSessionService.CurrentSave.MissionBlock.ActiveMissions.FirstOrDefault(m => m.MissionRuntimeId == missionRuntimeId);

        if (mission == null)
        {
            if (IsDebug())
                Debug.LogWarning($"MissionService: CompleteMission failed. Active mission not found: {missionRuntimeId}");
            return false;
        }

        if (mission.MissionType == MissionType.Delivery)
        {
            if (!mission.IsReadyToTurnIn)
            {
                if (IsDebug())
                    Debug.LogWarning($"MissionService: Delivery mission is not ready to turn in: {mission.Title}");
                return false;
            }

            if (IsDebug())
                Debug.Log("[MissionService] mission.Objective = " + mission.Objective);
            if (mission.Objective != null)
                mission.Objective.IsMissionCargoDelivered = true;
        }

        _saveService.EnableSave(false);
        _gameSessionService.CurrentSave.MissionBlock.ActiveMissions.Remove(mission);
        mission.Status = MissionStatus.Completed;
        _gameSessionService.CurrentSave.MissionBlock.CompletedMissions.Add(mission);
        _rewardService.TryGrantMissionReward(mission, _gameSessionService.CurrentSave.PlayerProfile);
        _saveService.EnableSave(true);

        if (IsDebug())
            Debug.Log($"MissionService: Mission completed: {mission.Title}");
        return true;
    }

    public bool FailMission(string missionRuntimeId)
    {
        MissionInstanceData mission = _gameSessionService.CurrentSave.MissionBlock.ActiveMissions.FirstOrDefault(m => m.MissionRuntimeId == missionRuntimeId);

        if (mission == null)
        {
            if (IsDebug())
                Debug.LogWarning($"MissionService: FailMission failed. Active mission not found: {missionRuntimeId}");
            return false;
        }

        _saveService.EnableSave(false);
        _gameSessionService.CurrentSave.MissionBlock.ActiveMissions.Remove(mission);
        mission.Status = MissionStatus.Failed;
        _saveService.EnableSave(true);

        if (IsDebug())
            Debug.Log($"MissionService: Mission failed: {mission.Title}");
        return true;
    }

    public MissionInstanceData GetMissionById(string missionRuntimeId)
    {
        MissionInstanceData mission = _gameSessionService.CurrentSave.MissionBlock.AvailableMissions.FirstOrDefault(m => m.MissionRuntimeId == missionRuntimeId);
        if (mission != null)
            return mission;

        mission = _gameSessionService.CurrentSave.MissionBlock.ActiveMissions.FirstOrDefault(m => m.MissionRuntimeId == missionRuntimeId);
        if (mission != null)
            return mission;

        mission = _gameSessionService.CurrentSave.MissionBlock.CompletedMissions.FirstOrDefault(m => m.MissionRuntimeId == missionRuntimeId);
        return mission;
    }

    public void ClearAll()
    {
        _saveService.EnableSave(false);
        _gameSessionService.CurrentSave.MissionBlock.AvailableMissions.Clear();
        _gameSessionService.CurrentSave.MissionBlock.ActiveMissions.Clear();
        _gameSessionService.CurrentSave.MissionBlock.CompletedMissions.Clear();
        _saveService.EnableSave(true);
    }

    // public void RestoreState(
    //     List<MissionInstanceData> availableMissions,
    //     List<MissionInstanceData> activeMissions,
    //     List<MissionInstanceData> completedMissions)
    // {
    //     _availableMissions.Clear();
    //     _activeMissions.Clear();
    //     _completedMissions.Clear();

    //     if (availableMissions != null)
    //     {
    //         _availableMissions.AddRange(availableMissions);
    //     }

    //     if (activeMissions != null)
    //     {
    //         _activeMissions.AddRange(activeMissions);
    //     }

    //     if (completedMissions != null)
    //     {
    //         _completedMissions.AddRange(completedMissions);
    //     }
    // }        
}