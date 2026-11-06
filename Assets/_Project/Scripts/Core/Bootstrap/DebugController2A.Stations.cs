using UnityEngine;

public sealed partial class DebugController2A
{
    [ContextMenu("STAR FRONTIER/Station/Уничтожить станцию в текущей системе")]
    private void DebugDestroyCurrentStation() => DebugDamageCurrentStation(true);

    [ContextMenu("STAR FRONTIER/Station/Урон 100")]
    private void DebugDamage100CurrentStation() => DebugDamageCurrentStation(false);

    private void DebugDamageCurrentStation(bool destroy)
    {
        if (!DeveloperModeGate.CanRunExplicitCommand(Application.isPlaying, Application.isEditor,
                DeveloperModeGate.IsEnabled))
        {
            AppLog.Warning(DebugLogChannel.DebugTools, typeof(DebugController2A),
                "[StationDebug] Command rejected | Reason=PlayModeOrDeveloperModeRequired");
            return;
        }
        var registry = ServiceRegistry;
        if (registry == null || !registry.TryGet<IGameSessionService>(out var session) ||
            !registry.TryGet<IStationService>(out var stations) ||
            !registry.TryGet<SimpleEventBus>(out var events))
        {
            AppLog.Warning(DebugLogChannel.DebugTools, typeof(DebugController2A),
                "[StationDebug] Command rejected | Reason=ServicesUnavailable");
            return;
        }
        string reason;
        bool success = destroy ? StationDebugCommands2A.TryDestroyCurrent(session, stations, events, out reason)
            : StationDebugCommands2A.TryDamage100Current(session, stations, events, out reason);
        if (!success)
            AppLog.Warning(DebugLogChannel.DebugTools, typeof(DebugController2A),
                "[StationDebug] Command rejected | Reason=" + reason);
    }
}

/// <summary>Uses the normal station damage/reconciliation pipeline, without altering config assets.</summary>
public static class StationDebugCommands2A
{
    public static bool TryDestroyCurrent(IGameSessionService session, IStationService stations,
        SimpleEventBus events, out string reason) => TryApplyCurrent(session, stations, events, true, out reason);

    public static bool TryDamage100Current(IGameSessionService session, IStationService stations,
        SimpleEventBus events, out string reason) => TryApplyCurrent(session, stations, events, false, out reason);

    private static bool TryApplyCurrent(IGameSessionService session, IStationService stations,
        SimpleEventBus events, bool destroy, out string reason)
    {
        reason = "ServicesUnavailable";
        if (session == null || stations == null || events == null) return false;
        var state = session.State;
        string systemId = state?.Player?.CurrentSystemId;
        if (string.IsNullOrWhiteSpace(systemId)) { reason = "CurrentSystemMissing"; return false; }
        var system = state.Galaxy?.Systems?.Find(x => x != null && x.SystemId == systemId);
        // Reject unavailable targets before reconciliation so a rejected debug command changes nothing.
        if (system?.Station != null && system.Station.Level <= 0)
        { reason = "StationNotBuilt"; return false; }
        // TryGetStation reconciles runtime data. Protected recovery must be rejected before that mutating read.
        if (StationLifecycleRules.HasRecoveryOperation(system?.Station))
        { reason = "StationRecoveryProtected"; return false; }
        if (system?.Station == null || !stations.TryGetStation(systemId, out var station) ||
            station == null || !station.IsEnabled || station.ConfigMissing)
        { reason = "StationUnavailable"; return false; }
        if (station.Level <= 0) { reason = "StationNotBuilt"; return false; }
        if (station.Recovery != null) { reason = "StationRecoveryProtected"; return false; }
        if (station.IsDestroyed) { reason = "StationAlreadyDestroyed"; return false; }

        system.ApplyInfrastructureDamageFromWar(100, destroy);
        stations.ReconcileSystem(systemId);
        events.PublishSafely(new SaveNeedEvent(destroy ? "debug_station_destroyed" : "debug_station_damage_100"), exception =>
            AppLog.Warning(DebugLogChannel.DebugTools, "[StationDebug] Save observer failed | " + exception.Message));
        reason = string.Empty;
        AppLog.Info(DebugLogChannel.DebugTools,
            "[StationDebug] " + (destroy ? "Destroyed" : "Damage100") + " | System=" + systemId +
            " | Station=" + station.ConfigId + " | Level=" + station.Level +
            " | Health=" + system.Station.Health + "/" + system.Station.MaxHealth);
        return true;
    }
}
