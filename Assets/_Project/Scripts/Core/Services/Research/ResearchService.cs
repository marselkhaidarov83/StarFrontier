using System;
using System.Linq;
using UnityEngine;

public sealed class ResearchService : IResearchService
{
    private readonly IGameSessionService _session;
    private readonly IConfigService _config;
    private readonly IStationInfrastructureService _infrastructure;
    private readonly IStationPaymentGateway _payments;
    private readonly SimpleEventBus _events;
    private bool _starting;
    private bool _sciencePaused;
    private GameRuntimeState _loggedSession;
    private readonly Func<string, bool> _stationAccess;
    public ResearchTreeConfig Tree { get; }
    public ResearchService(ResearchTreeConfig tree, IGameSessionService session, IConfigService config,
        IStationInfrastructureService infrastructure, IStationPaymentGateway payments, SimpleEventBus events, Func<string, bool> stationAccess = null)
    { Tree = tree; _session = session; _config = config; _infrastructure = infrastructure; _payments = payments; _events = events; _stationAccess = stationAccess; }
    public void InitializeSession()
    {
        if (_session.State?.Galaxy == null) return;
        _session.State.Galaxy.Research ??= new ResearchRuntimeState();
        _session.State.Galaxy.Research.Normalize();
        if (!ReferenceEquals(_loggedSession, _session.State))
        {
            _loggedSession = _session.State;
            var restored = _session.State.Galaxy.Research;
            if (restored.Active != null)
                AppLog.Info(DebugLogChannel.Research, "[Research] RESTORED | Upgrade=" + restored.ActiveUpgrade +
                    " | TargetLevel=" + restored.Active.TargetLevel + " | Operation=" + restored.Active.OperationId +
                    " | Elapsed=" + restored.Active.ElapsedSeconds + " | Duration=" + restored.Active.DurationSeconds);
        }
    }
    public bool TryEnterStation(string stationSystemId, out string reason)
    {
        reason = "ScienceStationUnavailable";
        if (_stationAccess != null) return _stationAccess(stationSystemId);
        var player = _session.State?.Player;
        var system = _session.State?.Galaxy?.Systems?.Find(x => x != null && x.SystemId == stationSystemId);
        var config = _config.GetStarSystemConfigById(stationSystemId)?.Station;
        if (player == null || config == null || !StationLocationRules.IsInside(_session.State, _config.GetStarSystemConfigById(stationSystemId))) return false;
        reason = string.Empty; return true;
    }
    public void LeaveStation()
    {
        if (Bootstrapper.Instance != null && Bootstrapper.Instance.ServiceRegistry.TryGet<IStationVisitService>(out var visit)) visit.Leave();
        else if (_session.State?.Player != null) _session.State.Player.CurrentStationId = string.Empty;
    }

    public ResearchRuntimeState GetSnapshot()
    {
        InitializeSession();
        if (_session.State?.Galaxy?.Research == null) return null;
        var snapshot = JsonUtility.FromJson<ResearchRuntimeState>(JsonUtility.ToJson(_session.State.Galaxy.Research));
        snapshot.Normalize();
        return snapshot;
    }
    public bool TryStart(string stationSystemId, ResearchUpgrade upgrade, string optionId, long expectedRevision, out string reason)
    {
        reason = "ResearchUnavailable";
        if (_starting || _session.State?.Galaxy == null) return false;
        InitializeSession();
        var state = _session.State.Galaxy.Research;
        var player = _session.State.Player;
        var system = _session.State.Galaxy.Systems.Find(x => x != null && x.SystemId == stationSystemId);
        if (system?.Station == null || !system.Station.CanDock || system.Station.ConfigMissing ||
            system.SystemStatus == StarSystemStatus.Captured ||
            player == null || player.CurrentSystemId != stationSystemId || player.CurrentStationId != system.Station.ConfigId)
        { reason = "NotInsideScienceStation"; return false; }
        if (!TryEnterStation(stationSystemId, out reason)) return false;
        if (!ResearchRules.CanAccessUpgrade(Tree, system.Station.StationType, upgrade))
        { reason = "ResearchBranchUnavailable"; return false; }
        if (state.Revision != expectedRevision) { reason = "ResearchStateChanged"; return false; }
        if (_infrastructure.GetScienceRate() <= 0) { reason = "ScienceUnavailable"; return false; }
        int galaxyLevel = Math.Max(1, _session.State.Galaxy.Sectors?.Count(x => x != null && x.IsUnlocked) ?? 1);
        if (!ResearchRules.TryQuote(Tree, state, upgrade, optionId, out var quote, out reason, galaxyLevel)) return false;
        _starting = true;
        try
        {
            if (!_payments.TryPay(stationSystemId, quote.Copy(), out reason)) return false;
            state.ActiveUpgrade = upgrade; state.Active = quote.Copy(); state.Revision++;
            Log("STARTED", upgrade, quote.TargetLevel);
            AdvanceTime(0); // Instant variants still publish exactly one completion.
            Save("research_started");
            _events?.PublishSafely(new CreditsChangedEvent(player.Credits), _ => { });
            _events?.PublishSafely(new DiamondsChangedEvent(player.Diamonds), _ => { });
            if (Bootstrapper.Instance != null && Bootstrapper.Instance.ServiceRegistry.TryGet<IInventoryService>(out var inventory))
                _events?.PublishSafely(new CargoChangedEvent(inventory.GetUsedCargo()), _ => { });
            return true;
        }
        finally { _starting = false; }
    }
    public void AdvanceTime(double seconds)
    {
        var state = _session.State?.Galaxy?.Research;
        if (state == null) return;
        double rate = _infrastructure.GetScienceRate();
        if (state.Active != null && (rate <= 0) != _sciencePaused)
        {
            _sciencePaused = rate <= 0;
            Log(_sciencePaused ? "PAUSED_NO_SCIENCE" : "RESUMED", state.ActiveUpgrade, state.Active.TargetLevel);
        }
        if (!ResearchRules.Advance(state, seconds, rate, out var upgrade, out int level)) return;
        Log("COMPLETED", upgrade, level);
        _events?.PublishSafely(new ResearchCompletedEvent(upgrade, level),
            e => AppLog.Warning(DebugLogChannel.Research, "[Research] Observer failed | " + e.Message));
        Save("research_completed");
    }
    private void Save(string reason) => _events?.PublishSafely(new SaveNeedEvent(reason), _ => { });
    private static void Log(string action, ResearchUpgrade upgrade, int level) => AppLog.Info(DebugLogChannel.Research,
        "[Research] " + action + " | Upgrade=" + upgrade + " | Level=" + level);
}
