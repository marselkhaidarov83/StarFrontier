using System;
using System.Linq;
using UnityEngine;

public sealed partial class DebugController2A
{
    [ContextMenu("STAR FRONTIER/Station/Мгновенно повысить уровень станции на 1")]
    private void DebugIncreaseCurrentStationLevel()
    {
        if (!DeveloperModeGate.CanRunExplicitCommand(Application.isPlaying, Application.isEditor, DeveloperModeGate.IsEnabled))
        { LogStationUpgradeRejection("PlayModeOrDeveloperModeRequired"); return; }
        var registry = ServiceRegistry;
        if (registry == null || !registry.TryGet<IGameSessionService>(out var session) ||
            !registry.TryGet<IConfigService>(out var config) || !registry.TryGet<IStationService>(out var stations) ||
            !registry.TryGet<SimpleEventBus>(out var events))
        { LogStationUpgradeRejection("ServicesUnavailable"); return; }
        string systemId = session.State?.Player?.CurrentSystemId;
        if (string.IsNullOrEmpty(systemId)) { LogStationUpgradeRejection("CurrentSystemMissing"); return; }
        var settings = config.GetStarSystemConfigById(systemId)?.Station?.LevelEffects;
        if (settings == null || !stations.TryGetStation(systemId, out _))
        { LogStationUpgradeRejection("StationUnavailable"); return; }
        var system = session.State.Galaxy.Systems.Find(x => x != null && x.SystemId == systemId);
        int galaxyLevel = Math.Max(1, session.State.Galaxy.Sectors?.Count(x => x != null && x.IsUnlocked) ?? 1);
        bool hasEnemies = registry.TryGet<ISystemNpcRuntimeService>(out var npcs) &&
            npcs.GetAliveNpcsInSystem(systemId).Any(x => x != null && x.IsAlive && (x.IsEnemy || x.IsHostileToPlayer));
        int before = system?.Station?.Level ?? 0;
        if (!StationUpgradeDebug2A.TryIncrease(system, settings, galaxyLevel, hasEnemies, out string reason))
        { LogStationUpgradeRejection(reason); return; }
        stations.ReconcileSystem(systemId);
        events.PublishSafely(new SaveNeedEvent("debug_station_level_increased"), e => LogStationUpgradeRejection(e.Message));
        AppLog.Info(DebugLogChannel.DebugTools, "[StationUpgradeDebug] UPGRADED | System=" + systemId +
            " | Before=" + before + " | After=" + system.Station.Level);
    }
    private static void LogStationUpgradeRejection(string reason) =>
        AppLog.Warning(DebugLogChannel.DebugTools, "[StationUpgradeDebug] Rejected | Reason=" + reason);
}

public static class StationUpgradeDebug2A
{
    public static bool TryIncrease(StarSystemRuntimeState system, StationLevelEffectsConfig config,
        int galaxyLevel, bool hasEnemies, out string reason)
    {
        reason = "StationUnavailable";
        var station = system?.Station;
        if (station == null || config == null || station.ConfigMissing || !station.IsEnabled) return false;
        if (system.SystemStatus == StarSystemStatus.Captured) { reason = "SystemCaptured"; return false; }
        if (station.IsDestroyed) { reason = "StationDestroyed"; return false; }
        if (station.Level <= 0) { reason = "NotBuilt"; return false; }
        if (station.Level >= StationLifecycleRules.MaxLevel) { reason = "MaximumLevel"; return false; }
        if (hasEnemies || system.SystemStatus == StarSystemStatus.Invasion) { reason = "StationUnderAttack"; return false; }
        if (StationLifecycleRules.HasDamage(station)) { reason = "StationDamaged"; return false; }
        if (StationLifecycleRules.HasRecoveryOperation(station)) { reason = "RecoveryAlreadyRunning"; return false; }
        int target = station.Level + 1;
        var settings = config.GetLevel(target);
        if (settings == null) { reason = "StationLevelSettingsMissing"; return false; }
        if (galaxyLevel < settings.RequiredGalaxyLevel) { reason = "GalaxyLevelRequired"; return false; }
        if (station.Development != null && (station.Development.Kind != StationOperationKind.Upgrade || station.Development.TargetLevel != target))
        { reason = "InvalidOperation"; return false; }
        if (station.Development == null)
        {
            var operation = new StationOperationRuntimeState { OperationId = Guid.NewGuid().ToString("N"),
                Kind = StationOperationKind.Upgrade, TargetLevel = target, PaymentOptionId = "debug_free_upgrade" };
            if (!StationLifecycleRules.BeginPaidOperation(station, operation)) { reason = "UpgradeUnavailable"; return false; }
        }
        // Finish an already paid improvement as well; never charge or refund its payment.
        station.Development.ElapsedSeconds = station.Development.DurationSeconds;
        StationLifecycleRules.Advance(system, 0, _ => settings.EffectiveMaxHealth, false);
        reason = station.Level == target ? string.Empty : "UpgradeNotCompleted";
        return station.Level == target;
    }
}
