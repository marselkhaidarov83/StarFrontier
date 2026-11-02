using System;
using System.Collections.Generic;

/// <summary>S07-01 station state coordination. No scene, MonoBehaviour, Update or view dependency.</summary>
public sealed class StationService : IStationService
{
    private readonly IGameSessionService _session;
    private readonly Func<string, StarSystemConfig> _resolveSystem;
    private readonly SimpleEventBus _events;
    private readonly IStationPaymentGateway _payments;
    private readonly Dictionary<string, StationRuntimeState> _published =
        new Dictionary<string, StationRuntimeState>(StringComparer.Ordinal);
    private readonly HashSet<string> _commands = new HashSet<string>(StringComparer.Ordinal);

    public StationService() : this(
        Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>().GetStarSystemConfigById,
        Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>())
    {
        Bootstrapper.Instance.ServiceRegistry.TryGet(out _payments);
    }

    public StationService(IGameSessionService session, Func<string, StarSystemConfig> resolveSystem,
        SimpleEventBus events, IStationPaymentGateway payments = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _resolveSystem = resolveSystem ?? throw new ArgumentNullException(nameof(resolveSystem));
        _events = events;
        _payments = payments;
    }

    public void InitializeSession(bool newGame)
    {
        _published.Clear();
        var galaxy = _session.State?.Galaxy;
        if (galaxy?.Systems != null)
            foreach (var system in galaxy.Systems)
                StationLifecycleRules.NormalizeSerializedPlaceholders(system);
        ValidateWorld(galaxy);
        if (galaxy?.Systems == null) return;
        foreach (var system in galaxy.Systems)
        {
            if (system == null) continue;
            if (system.StationStateVersion == 0 && system.Station == null)
            {
                var systemConfig = _resolveSystem(system.SystemId);
                StationConfig config = systemConfig?.Station;
                system.Station = StationLifecycleRules.Create(config, legacySave: !newGame);
                // A missing legacy config must remain retryable after content is restored.
                if (newGame || config != null)
                    system.StationStateVersion = StationLifecycleRules.SchemaVersion;
                Log("BOUND", system.SystemId, config == null ? "NoStationConfig" :
                    newGame ? "NewGame" : "LegacyLevel1Fallback");
            }
            Reconcile(system);
            Notify(system, "SessionInitialized");
        }
        ValidateWorld(galaxy);
    }

    public bool TryGetStation(string systemId, out StationRuntimeState snapshot)
    {
        var system = FindSystem(systemId);
        snapshot = null;
        if (system?.Station == null) return false;
        Reconcile(system);
        Notify(system, "StateReconciled");
        snapshot = system.Station.Copy();
        return true;
    }

    public bool TryStartOperation(string systemId, StationOperationKind kind,
        string paymentOptionId, int expectedLevel, long expectedRevision, out string reason)
    {
        reason = string.Empty;
        var system = FindSystem(systemId);
        if (system == null) { reason = "SystemNotFound"; return false; }
        if (!_commands.Add(systemId)) { reason = "CommandAlreadyRunning"; return false; }
        try
        {
            Reconcile(system);
            if (!StationLifecycleRules.CanBegin(system.Station, kind, out reason)) return Reject(systemId, reason);
            if (system.Station.Level != expectedLevel || system.Station.Revision != expectedRevision)
            { reason = "StationStateChanged"; return Reject(systemId, reason); }
            if (kind == StationOperationKind.Recovery && system.SystemStatus == StarSystemStatus.Captured)
            { reason = "SystemCaptured"; return Reject(systemId, reason); }
            var config = _resolveSystem(systemId)?.Station;
            if (config == null || !config.TryCreateOperation(kind, system.Station.Level, paymentOptionId, out var quote))
            { reason = "InvalidPaymentOption"; return Reject(systemId, reason); }
            if (_payments == null)
            { reason = "PaymentUnavailable"; return Reject(systemId, reason); }
            // The gateway is the S07-03 transaction boundary; never grant a free operation when it is absent.
            if (!_payments.TryPay(systemId, quote.Copy(), out reason)) return Reject(systemId, reason);
            if (!StationLifecycleRules.BeginPaidOperation(system.Station, quote))
                throw new InvalidOperationException("Station payment gateway changed the operation preconditions.");
            StationLifecycleRules.ApplySystemStatus(system.Station, system.SystemStatus);
            StationLifecycleRules.Advance(system, 0d); // Complete zero-time variants exactly once.
            Notify(system, "OperationStarted");
            PublishSaveRequest("station_operation_started");
            return true;
        }
        finally { _commands.Remove(systemId); }
    }

    public void ReconcileSystem(string systemId)
    {
        var system = FindSystem(systemId);
        if (system == null) return;
        Reconcile(system);
        Notify(system, "InfrastructureChanged");
    }

    public void AdvanceTime(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d) return;
        var galaxy = _session.State?.Galaxy;
        ValidateWorld(galaxy);
        if (galaxy?.Systems == null) return;
        foreach (var system in galaxy.Systems)
        {
            if (system?.Station == null) continue;
            Reconcile(system);
            Notify(system, "StateReconciled");
            var station = system.Station;
            bool hadRecovery = station.Recovery != null;
            bool hadDevelopment = station.Development != null;
            StationLifecycleRules.Advance(system, seconds);
            bool completed = (hadRecovery && station.Recovery == null) ||
                (hadDevelopment && station.Development == null);
            Notify(system, completed ? "OperationCompleted" : "Progress");
            if (completed) PublishSaveRequest("station_operation_completed");
        }
    }

    public void ProcessOffline(GameRuntimeState state, long nowUtcTicks)
    {
        if (state?.Meta == null || !ReferenceEquals(state, _session.State) ||
            nowUtcTicks <= 0 || nowUtcTicks > DateTime.MaxValue.Ticks) return;
        long from = Math.Max(state.Meta.LastSaveUtc, state.Meta.StationOfflineProcessedUtc);
        double elapsed = OfflineProgressWindow.ElapsedSeconds(from, nowUtcTicks);
        if (nowUtcTicks <= state.Meta.StationOfflineProcessedUtc) return;
        // Set before dispatch: repeated calls and event re-entry cannot apply the same interval twice.
        state.Meta.StationOfflineProcessedUtc = nowUtcTicks;
        if (elapsed <= 0d) return;
        AdvanceTime(elapsed);
        Log("OFFLINE", string.Empty, "Seconds=" + elapsed);
        PublishSaveRequest("station_offline_progress");
    }

    private StarSystemRuntimeState FindSystem(string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId)) return null;
        var galaxy = _session.State?.Galaxy;
        ValidateWorld(galaxy);
        if (galaxy?.Systems == null) return null;
        foreach (var system in galaxy.Systems)
            if (system != null && system.SystemId == systemId) return system;
        return null;
    }

    private void Reconcile(StarSystemRuntimeState system)
    {
        var station = system.Station;
        if (station == null) return;
        var config = _resolveSystem(system.SystemId)?.Station;
        bool missing = config == null || config.StationTypeConfig == null || config.Id != station.ConfigId;
        if (station.ConfigMissing != missing)
        {
            station.ConfigMissing = missing;
            station.Revision++;
            Log("CONFIG", system.SystemId, missing ? "MissingOrReplaced_PaidSnapshotPreserved" : "Available");
        }
        // Saved type/level and accepted duration/costs are authoritative even after an asset edit/removal.
        StationLifecycleRules.Normalize(system);
    }

    private static void ValidateWorld(GalaxyRuntimeState galaxy)
    {
        foreach (string error in StationLifecycleRules.GetValidationErrors(galaxy))
            throw new InvalidOperationException(error);
    }

    private void Notify(StarSystemRuntimeState system, string reason)
    {
        var current = system.Station;
        if (current == null) return;
        _published.TryGetValue(system.SystemId, out var previous);
        if (previous != null && previous.Revision == current.Revision) return;
        _published[system.SystemId] = current.Copy();
        if (reason != "Progress") Log("CHANGED", system.SystemId,
            reason + " | Level=" + current.Level + " | Destroyed=" + current.IsDestroyed);
        _events?.PublishSafely(new StationStateChangedEvent(system.SystemId, reason, previous, current),
            exception => AppLog.Warning(DebugLogChannel.StationLifecycle,
                "[StationLifecycle] Observer failed | " + exception.Message));
    }

    private void PublishSaveRequest(string reason)
    {
        _events?.PublishSafely(new SaveNeedEvent(reason),
            exception => AppLog.Warning(DebugLogChannel.StationLifecycle,
                "[StationLifecycle] Save observer failed | " + exception.Message));
    }

    private static bool Reject(string systemId, string reason)
    { Log("REJECTED", systemId, reason); return false; }

    private static void Log(string action, string systemId, string detail)
    {
        AppLog.Info(DebugLogChannel.StationLifecycle,
            "[StationLifecycle] " + action + " | System=" + systemId + " | " + detail);
    }
}
