using System.Text;
using UnityEngine;

public sealed partial class DebugController2A
{
    [Header("Mission Debug")]
    [SerializeField] private string debugMissionRuntimeId;
    [SerializeField, Range(1, 20)] private int debugBattlePersonalKills = 1;

    [ContextMenu("STAR FRONTIER/Mission/Удалить все миссии")]
    private void DebugDeleteAllMissions()
    {
        if (!DeveloperModeGate.CanRunExplicitCommand(Application.isPlaying, Application.isEditor,
                DeveloperModeGate.IsEnabled))
        {
            AppLog.Warning(DebugLogChannel.Mission, typeof(DebugController2A),
                "[MissionDebug] Delete all rejected | Reason=" +
                (!Application.isPlaying ? "NotInPlayMode" : "DeveloperModeDisabledOutsideEditor"));
            return;
        }
        if (ServiceRegistry == null || !ServiceRegistry.TryGet<IMissionService>(out var missions) ||
            !ServiceRegistry.TryGet<SimpleEventBus>(out var events))
        {
            AppLog.Warning(DebugLogChannel.Mission, typeof(DebugController2A),
                "[MissionDebug] Delete all rejected | Reason=ServicesUnavailable");
            return;
        }
        int available = missions.GetAvailableMissions().Count;
        int active = missions.GetActiveMissions().Count;
        int completed = missions.GetCompletedMissions().Count;
        int archived = missions.GetArchivedMissions().Count;
        try
        {
            missions.ClearAll();
            debugMissionRuntimeId = string.Empty;
            events.Publish(new MissionStateChangedEvent());
            AppLog.Info(DebugLogChannel.Mission, typeof(DebugController2A),
                "[MissionDebug] Deleted all missions | Available=" + available +
                " | Active=" + active + " | Completed=" + completed +
                " | Archived=" + archived + " | RewardLedgerPreserved=true");
        }
        catch (System.Exception exception)
        {
            AppLog.Error(DebugLogChannel.Mission, typeof(DebugController2A),
                "[MissionDebug] Delete all failed | " + exception.Message);
        }
    }

    [ContextMenu("STAR FRONTIER/Mission/Create Defense Reward On Current Planet")]
    private void DebugCreateDefenseReward() => DebugCreateReadyBattleReward(MissionType.Defense);

    [ContextMenu("STAR FRONTIER/Mission/Create Liberation Reward On Current Planet")]
    private void DebugCreateLiberationReward() => DebugCreateReadyBattleReward(MissionType.Liberation);

    private void DebugCreateReadyBattleReward(MissionType type)
    {
        bool developerMode = DeveloperModeGate.IsEnabled;
        if (!DeveloperModeGate.CanRunExplicitCommand(Application.isPlaying, Application.isEditor, developerMode))
        {
            AppLog.Warning(DebugLogChannel.Mission, typeof(DebugController2A),
                "[MissionDebug] Battle reward creation rejected | Type=" + type +
                " | Reason=" + (!Application.isPlaying ? "NotInPlayMode" : "DeveloperModeDisabledOutsideEditor"));
            return;
        }
        if (ServiceRegistry == null || !ServiceRegistry.TryGet<IMissionService>(out var missions) ||
            missions is not MissionService missionService ||
            !ServiceRegistry.TryGet<SimpleEventBus>(out var events))
        {
            AppLog.Warning(DebugLogChannel.Mission, typeof(DebugController2A),
                "[MissionDebug] Battle reward creation rejected | Reason=ServicesUnavailable");
            return;
        }
        int kills = Mathf.Clamp(debugBattlePersonalKills, 1, 20);
        if (!missionService.TryCreateDebugReadyBattleMission(type, kills,
            out MissionInstanceData mission, out string reason))
        {
            AppLog.Warning(DebugLogChannel.Mission, typeof(DebugController2A),
                "[MissionDebug] Battle reward creation rejected | Type=" + type + " | Reason=" + reason);
            return;
        }
        debugMissionRuntimeId = mission.MissionRuntimeId;
        AppLog.Info(DebugLogChannel.Mission, typeof(DebugController2A),
            "[MissionDebug] Battle reward ready | Type=" + type + " | Id=" + mission.MissionRuntimeId +
            " | Status=" + mission.Status + " | Credits=" + mission.Reward.Credits +
            " | PersonalKills=" + kills + " | WorldCombatUnchanged=true");
        events.Publish(new GovernmentEnteredEvent());
    }

    [ContextMenu("STAR FRONTIER/Mission/Print Active Missions")]
    private void DebugPrintActiveMissions()
    {
        if (ServiceRegistry == null) return;
        IMissionService missions = ServiceRegistry.Get<IMissionService>();
        IConfigService config = ServiceRegistry.Get<IConfigService>();
        var text = new StringBuilder("[MissionDebug] Active missions");
        foreach (MissionInstanceData mission in missions.GetActiveMissions())
        {
            if (mission == null) continue;
            text.Append("\n").Append(mission.MissionRuntimeId)
                .Append(" | ").Append(mission.MissionType)
                .Append(" | ").Append(mission.Status)
                .Append(" | ").Append(MissionUiText.NextStep(mission, config));
        }
        AppLog.Info(DebugLogChannel.Mission, typeof(DebugController2A), text.ToString());
    }

    [ContextMenu("STAR FRONTIER/Mission/Show Current Planet Offer")]
    private void DebugShowCurrentPlanetOffer()
    {
        if (ServiceRegistry == null) return;
        IConfigService config = ServiceRegistry.Get<IConfigService>();
        PlanetConfig planet = config.GetCurrentPlanetConfig();
        StarSystemConfig system = config.GetCurrentSystemConfig();
        if (planet == null || system == null) return;
        var offer = ServiceRegistry.Get<IPlanetGovernmentMissionService>()
            .GetOrCreateGovernmentOffer(system, planet);
        AppLog.Info(DebugLogChannel.Mission, typeof(DebugController2A),
            "[MissionDebug] Planet=" + planet.Id + " | Offer=" +
            (offer?.OfferedMission?.MissionRuntimeId ?? "none"));
    }

    [ContextMenu("STAR FRONTIER/Mission/Accept Current Planet Offer")]
    private void DebugAcceptCurrentPlanetOffer()
    {
        if (ServiceRegistry == null) return;
        IConfigService config = ServiceRegistry.Get<IConfigService>();
        PlanetConfig planet = config.GetCurrentPlanetConfig();
        if (planet == null) return;
        bool accepted = ServiceRegistry.Get<IPlanetGovernmentMissionService>()
            .AcceptGovernmentOffer(planet.Id);
        AppLog.Info(DebugLogChannel.Mission, typeof(DebugController2A),
            "[MissionDebug] Accept planet offer | Planet=" + planet.Id + " | Success=" + accepted);
    }

    [ContextMenu("STAR FRONTIER/Mission/Reevaluate Active Missions")]
    private void DebugReevaluateMissions()
    {
        if (ServiceRegistry == null) return;
        ServiceRegistry.Get<IMissionService>().ReevaluateActiveMissions("DebugController2A");
        DebugPrintActiveMissions();
    }

    [ContextMenu("STAR FRONTIER/Mission/Preview Ready To Claim")]
    private void DebugPreviewMissionReady()
    {
        MissionInstanceData mission = ResolveDebugMission();
        if (mission == null) return;
        mission.Status = MissionStatus.ReadyToComplete;
        mission.IsReadyToTurnIn = true;
        ServiceRegistry.Get<SimpleEventBus>().Publish(new MissionStateChangedEvent
        {
            MissionRuntimeId = mission.MissionRuntimeId
        });
        AppLog.Info(DebugLogChannel.Mission, typeof(DebugController2A),
            "[MissionDebug] UI preview ready; gameplay objectives were not advanced | Id=" +
            mission.MissionRuntimeId);
    }

    [ContextMenu("STAR FRONTIER/Mission/Fail Selected Mission")]
    private void DebugFailSelectedMission()
    {
        MissionInstanceData mission = ResolveDebugMission();
        if (mission == null) return;
        bool failed = ServiceRegistry.Get<IMissionService>().FailMission(mission.MissionRuntimeId);
        AppLog.Info(DebugLogChannel.Mission, typeof(DebugController2A),
            "[MissionDebug] Fail | Id=" + mission.MissionRuntimeId + " | Success=" + failed);
    }

    [ContextMenu("STAR FRONTIER/Mission/Claim Selected Mission Reward")]
    private void DebugClaimSelectedMissionReward()
    {
        MissionInstanceData mission = ResolveDebugMission();
        if (mission == null) return;
        bool completed = ServiceRegistry.Get<IMissionService>().CompleteMission(mission.MissionRuntimeId);
        AppLog.Info(DebugLogChannel.Mission, typeof(DebugController2A),
            "[MissionDebug] Claim | Id=" + mission.MissionRuntimeId + " | Success=" + completed);
    }

    private MissionInstanceData ResolveDebugMission()
    {
        if (ServiceRegistry == null) return null;
        IMissionService missions = ServiceRegistry.Get<IMissionService>();
        if (!string.IsNullOrWhiteSpace(debugMissionRuntimeId))
            return missions.GetMissionById(debugMissionRuntimeId);
        foreach (MissionInstanceData mission in missions.GetActiveMissions())
            if (mission != null) return mission;
        AppLog.Warning(DebugLogChannel.Mission, typeof(DebugController2A),
            "[MissionDebug] No active mission. Set debugMissionRuntimeId or accept an offer.");
        return null;
    }
}
