using System.Collections.Generic;
using UnityEngine;

public class MissionTracker : CustomService, IMissionTracker
{
    private readonly IMissionService _missionService;
    private readonly SimpleEventBus _eventBus;
    private readonly ISystemNpcRuntimeService _systemNpcRuntimeService;

    public string LastTrackerMessage { get; private set; }

    public MissionTracker()
    {
        _missionService = Bootstrapper.Instance.ServiceRegistry.Get<IMissionService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _systemNpcRuntimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();

        _eventBus.Subscribe<PlanetEnteredEvent>(OnPlanetEntered);
        _eventBus.Subscribe<SystemEnteredEvent>(OnSystemEntered);
        _eventBus.Subscribe<SystemNpcDestroyedEvent>(OnSystemNpcDestroyedEvent);
        _eventBus.Subscribe<MissionAcceptedEvent>(OnMissionAccepted);

        LogCustom("initialized");
        SetTrackerMessage("MissionTracker initialized.");
    }

    public void Dispose()
    {
        _eventBus.Unsubscribe<PlanetEnteredEvent>(OnPlanetEntered);
        _eventBus.Unsubscribe<SystemEnteredEvent>(OnSystemEntered);
        _eventBus.Unsubscribe<SystemNpcDestroyedEvent>(OnSystemNpcDestroyedEvent);
        _eventBus.Unsubscribe<MissionAcceptedEvent>(OnMissionAccepted);

        SetTrackerMessage("MissionTracker disposed.");
    }

    private void OnMissionAccepted(MissionAcceptedEvent eventData)
    {
        SetTrackerMessage($"Tracker noticed accepted mission: {eventData.MissionRuntimeId}");
    }

    private void OnPlanetEntered(PlanetEnteredEvent eventData)
    {
        var activeMissions = _missionService.GetActiveMissions();

        Debug.Log("[MissionTracker] activeMissions.Count = " + activeMissions.Count);
        foreach (MissionInstanceData mission in activeMissions)
        {
            if (mission == null || mission.Objective == null)
                continue;

            if (mission.MissionType != MissionType.Delivery)
                continue;

            Debug.Log("[MissionTracker] TargetPlanetId = " + mission.TargetPlanetId);
            Debug.Log("[MissionTracker] PlanetId = " + eventData.PlanetId);
            if (mission.TargetPlanetId == eventData.PlanetId)
            {
                mission.Status = MissionStatus.ReadyToComplete;
                mission.IsReadyToTurnIn = true;
                SetTrackerMessage($"Delivery ready on planet: {mission.Title}");
            }
        }
    }

    private void OnSystemEntered(SystemEnteredEvent eventData)
    {
        var activeMissions = _missionService.GetActiveMissions();

        Debug.Log("[MissionTracker] activeMissions.count = " + activeMissions.Count);
        foreach (MissionInstanceData mission in activeMissions)
        {
            if (mission == null || mission.Objective == null)
                continue;

            Debug.Log("[MissionTracker] mission.MissionType = " + mission.MissionType);
            // if (mission.MissionType == MissionType.Delivery)
            // {
            //     if (mission.TargetSystemId == eventData.EnteredSystemId)
            //     {
            //         mission.Status = MissionStatus.ReadyToComplete;
            //         mission.IsReadyToTurnIn = true;
            //         SetTrackerMessage($"Delivery ready to complete: {mission.Title}");
            //     }
            // }
            // else
            if (mission.MissionType == MissionType.Recon)
            {
                if (mission.TargetSystemId == eventData.SystemId)
                {
                    if (mission.Objective != null)
                        mission.Objective.CurrentAmount = mission.Objective.RequiredAmount;

                    _missionService.CompleteMission(mission.MissionRuntimeId);
                    SetTrackerMessage($"Recon completed: {mission.Title}");
                    return;
                }
            }
        }
    }

    private void OnSystemNpcDestroyedEvent(SystemNpcDestroyedEvent eventData)
    {
        LogCustom("");
        var activeMissions = _missionService.GetActiveMissions();

        foreach (MissionInstanceData mission in activeMissions)
        {
            if (mission == null)
                continue;

            if (mission.MissionType != MissionType.Elimination)
                continue;

            bool enemyMatches = eventData.RuntimeNpcGroupId == mission.PirateGroupNpcId;
            LogCustom("enemyMatches = " + enemyMatches);

            if (!enemyMatches)
            {
                LogCustom($"Enemy ignored for mission {mission.Title}: expected {mission.Objective.TargetEnemyId}, got {eventData.RuntimeNpcId}");
                continue;
            }

            IReadOnlyList<SystemNpcRuntimeState> npcs = 
                _systemNpcRuntimeService.GetAliveNpcsByGroupId(mission.PirateGroupNpcId);

            if (npcs.Count == 0)
            {
                // _missionService.CompleteMission(mission.MissionRuntimeId);
                mission.Status = MissionStatus.ReadyToComplete;
                mission.IsReadyToTurnIn = true;
                SetTrackerMessage($"Elimination completed: {mission.Title}");
                return;
            }
            else
            {
                mission.Status = MissionStatus.InProgress;
            }

            SetTrackerMessage(
                $"Elimination progress: {mission.Title} [{mission.Objective.CurrentAmount}/{mission.Objective.RequiredAmount}]");
        }
    }

    private void SetTrackerMessage(string message)
    {
        LastTrackerMessage = message;
        Debug.Log($"MissionTracker: {message}");
    }
}