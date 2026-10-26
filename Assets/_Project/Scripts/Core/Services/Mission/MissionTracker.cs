using System;
using System.Linq;

public class MissionTracker : CustomService, IMissionTracker, ITickable
{
    private readonly IMissionService _missionService;
    private readonly IGameSessionService _session;
    private readonly SimpleEventBus _eventBus;
    public string LastTrackerMessage { get; private set; }

    public MissionTracker()
    {
        var registry = Bootstrapper.Instance.ServiceRegistry;
        _missionService = registry.Get<IMissionService>();
        _session = registry.Get<IGameSessionService>();
        _eventBus = registry.Get<SimpleEventBus>();
        _eventBus.Subscribe<PlanetEnteredEvent>(OnPlanetEntered);
        _eventBus.Subscribe<StarSystemEnteredEvent>(OnSystemEntered);
        _eventBus.Subscribe<GameTimeQuantumAdvancedEvent>(OnDayAdvanced);
        _eventBus.Subscribe<TravelFinishedEvent>(OnTravelFinished);
        _eventBus.Subscribe<SystemEncounterVictoryPendingRewardEvent>(OnBattleVictory);
        _eventBus.Subscribe<SystemEncounterDefeatedEvent>(OnBattleDefeat);
        _eventBus.Subscribe<SystemInvasionStartedMissionEvent>(OnInvasionStarted);
        SetTrackerMessage("Initialized; five mission types enabled.");
    }

    public void Dispose()
    {
        _eventBus.Unsubscribe<PlanetEnteredEvent>(OnPlanetEntered);
        _eventBus.Unsubscribe<StarSystemEnteredEvent>(OnSystemEntered);
        _eventBus.Unsubscribe<GameTimeQuantumAdvancedEvent>(OnDayAdvanced);
        _eventBus.Unsubscribe<TravelFinishedEvent>(OnTravelFinished);
        _eventBus.Unsubscribe<SystemEncounterVictoryPendingRewardEvent>(OnBattleVictory);
        _eventBus.Unsubscribe<SystemEncounterDefeatedEvent>(OnBattleDefeat);
        _eventBus.Unsubscribe<SystemInvasionStartedMissionEvent>(OnInvasionStarted);
    }

    private void OnPlanetEntered(PlanetEnteredEvent evt) =>
        _missionService.ReevaluateActiveMissions("PlanetEntered:" + evt.PlanetId);

    private void OnSystemEntered(StarSystemEnteredEvent evt)
    {
        _missionService.ReevaluateActiveMissions("SystemEntered:" + evt.SystemId);
    }

    private void OnDayAdvanced(GameTimeQuantumAdvancedEvent evt) =>
        _missionService.ReevaluateActiveMissions("DayAdvanced:" + evt.CurrentDay);

    private void OnTravelFinished(TravelFinishedEvent evt) => _missionService.ObserveMissionTravel(evt);
    private void OnBattleVictory(SystemEncounterVictoryPendingRewardEvent evt) => _missionService.ResolveBattle(evt.EncounterId, evt.SystemId, true);
    private void OnBattleDefeat(SystemEncounterDefeatedEvent evt) => _missionService.ResolveBattle(evt.EncounterId, evt.SystemId, false);
    private void OnInvasionStarted(SystemInvasionStartedMissionEvent evt) => _missionService.ReevaluateActiveMissions("InvasionStarted:" + evt.SystemId);

    public void Tick(float deltaTime) => _missionService.ObserveReconTargets();

    public void ReevaluateAfterLoad()
    {
        var state = _session.State;
        if (state == null) return;
        MissionLifecycleRules.Normalize(state.MissionBlock, state.Meta?.CurrentGameDay ?? 1);
        _missionService.ReevaluateActiveMissions("AfterLoad");
        SetTrackerMessage("Reevaluated mission conditions after world restore.");
    }

    private void SetTrackerMessage(string message)
    {
        LastTrackerMessage = message;
        AppLog.Info(DebugLogChannel.Mission, "[MissionTracker] " + message);
    }
}
