using System;
using System.Collections.Generic;

/// <summary>Scene-independent transitions; no registry, UI, wallet or wall clock dependencies.</summary>
public static class StationLifecycleRules
{
    public const int SchemaVersion = 1;
    public const int MaxLevel = 10;

    public static StationRuntimeState Create(StationConfig config, bool legacySave = false)
    {
        if (config == null) return null;
        return new StationRuntimeState
        {
            ConfigId = config.Id, StationType = config.StationType,
            // Old saves had an already existing station, but no level. Do not turn it into a building site.
            Level = legacySave ? 1 : config.InitialLevel,
            IsEnabled = config.IsActive, IsDestroyed = config.IsDestroyed
        };
    }

    public static void Destroy(StationRuntimeState station)
    {
        if (station == null || station.Recovery != null) return;
        bool changed = !station.IsDestroyed;
        station.IsDestroyed = true;
        station.Health = 0;
        if (station.Development != null && station.Development.PauseReason != StationOperationPauseReason.Destroyed)
        {
            station.Development.PauseReason = StationOperationPauseReason.Destroyed;
            changed = true;
        }
        if (changed) station.Revision++;
    }

    public static void ApplySystemStatus(StationRuntimeState station, StarSystemStatus status)
    {
        if (station?.Recovery == null) return;
        if (status == StarSystemStatus.Captured)
        {
            station.Recovery = null; // No refund; keep the independent development snapshot.
            station.Revision++;
            return;
        }
        var pause = status == StarSystemStatus.Invasion
            ? StationOperationPauseReason.UnderAttack : StationOperationPauseReason.None;
        if (station.Recovery.PauseReason == pause) return;
        station.Recovery.PauseReason = pause;
        station.Revision++;
    }

    public static bool HasDamage(StationRuntimeState station) => station != null &&
        station.HealthVersion > 0 && station.Health < station.MaxHealth;

    // Unknown enemy context during save normalization preserves an already recorded attack pause.
    // The application layer always supplies live NPC presence before advancing time.
    public static void RefreshDevelopmentPause(StarSystemRuntimeState system, bool? hasEnemies = null)
    {
        var station = system?.Station;
        var operation = station?.Development;
        if (operation == null) return;
        var expected = station.IsDestroyed ? StationOperationPauseReason.Destroyed : StationOperationPauseReason.None;
        if (!station.IsDestroyed && (operation.Kind == StationOperationKind.Upgrade || operation.Kind == StationOperationKind.Construction))
            expected = hasEnemies == true || (hasEnemies == null && operation.PauseReason == StationOperationPauseReason.UnderAttack)
                ? StationOperationPauseReason.UnderAttack : operation.Kind == StationOperationKind.Upgrade && HasDamage(station)
                    ? StationOperationPauseReason.Damaged : StationOperationPauseReason.None;
        if (operation.PauseReason == expected) return;
        operation.PauseReason = expected;
        station.Revision++;
    }

    public static bool CanBegin(StationRuntimeState station, StationOperationKind kind, out string reason)
    {
        reason = string.Empty;
        if (station == null) reason = "StationNotFound";
        else if (station.ConfigMissing) reason = "StationConfigMissing";
        else if (!station.IsEnabled) reason = "StationDisabled";
        else if (kind == StationOperationKind.Recovery)
        {
            if (!station.IsDestroyed) reason = "StationNotDestroyed";
            else if (station.Recovery != null) reason = "RecoveryAlreadyRunning";
        }
        else if (kind != StationOperationKind.Construction && kind != StationOperationKind.Upgrade)
            reason = "InvalidOperation";
        else if (station.IsDestroyed) reason = "StationDestroyed";
        else if (station.Development != null) reason = "DevelopmentAlreadyRunning";
        else if (kind == StationOperationKind.Construction && station.Level != 0) reason = "AlreadyBuilt";
        else if (kind == StationOperationKind.Upgrade && station.Level == 0) reason = "NotBuilt";
        else if (station.Level >= MaxLevel) reason = "MaximumLevel";
        else if (kind == StationOperationKind.Upgrade && HasDamage(station)) reason = "StationDamaged";
        return reason.Length == 0;
    }

    // Only called after the application layer has accepted payment. Never reads prices from an asset afterwards.
    public static bool BeginPaidOperation(StationRuntimeState station, StationOperationRuntimeState operation)
    {
        if (operation == null || !CanBegin(station, operation.Kind, out _) ||
            !IsFinite(operation.DurationSeconds) || operation.DurationSeconds < 0 ||
            string.IsNullOrWhiteSpace(operation.OperationId)) return false;
        int target = operation.Kind == StationOperationKind.Recovery ? station.Level : station.Level + 1;
        if (operation.TargetLevel != target) return false;
        var accepted = operation.Copy();
        accepted.ElapsedSeconds = 0;
        accepted.PauseReason = StationOperationPauseReason.None;
        if (accepted.Kind == StationOperationKind.Recovery) station.Recovery = accepted;
        else station.Development = accepted;
        station.Revision++;
        return true;
    }

    public static void Advance(StarSystemRuntimeState system, double seconds, Func<int, int> maxHealth = null, bool hasEnemies = false)
    {
        var station = system?.Station;
        if (station == null || !station.IsEnabled || !IsFinite(seconds) || seconds < 0) return;
        StationInfrastructureRules.EnsureHealth(system);
        ApplySystemStatus(station, system.SystemStatus);
        double remaining = seconds;
        if (station.Recovery != null)
        {
            var recovery = station.Recovery;
            if (recovery.PauseReason != StationOperationPauseReason.None && recovery.DurationSeconds > 0) return;
            double previousElapsed = recovery.ElapsedSeconds;
            remaining = AdvanceOperation(recovery, remaining, out bool completed);
            if (completed || previousElapsed != recovery.ElapsedSeconds) station.Revision++;
            if (!completed) return;
            station.Recovery = null;
            station.IsDestroyed = false;
            station.RepairDamageCredit = 0;
            StationInfrastructureRules.RepairFully(system);
            if (station.Development != null) station.Development.PauseReason = StationOperationPauseReason.None;
        }
        RefreshDevelopmentPause(system, hasEnemies);
        if (station.IsDestroyed) return;
        var development = station.Development;
        if (development == null || development.PauseReason != StationOperationPauseReason.None) return;
        double previousDevelopmentElapsed = development.ElapsedSeconds;
        AdvanceOperation(development, remaining, out bool done);
        if (done || previousDevelopmentElapsed != development.ElapsedSeconds) station.Revision++;
        if (!done) return;
        station.Level = development.TargetLevel;
        station.Development = null;
        StationInfrastructureRules.EnsureHealth(system, maxHealth?.Invoke(station.Level) ?? 0);
        if (development.Kind == StationOperationKind.Construction) StationInfrastructureRules.RepairFully(system);
    }

    private static double AdvanceOperation(StationOperationRuntimeState operation, double seconds, out bool completed)
    {
        double required = Math.Max(0, operation.DurationSeconds - operation.ElapsedSeconds);
        double used = Math.Min(required, seconds);
        operation.ElapsedSeconds += used;
        completed = operation.ElapsedSeconds >= operation.DurationSeconds;
        return seconds - used;
    }

    /// <summary>
    /// Unity's inline serializer may materialize null class fields as all-default objects.
    /// Remove only these empty representations; never discard an identified/started operation.
    /// Must also run before validation when a session is initialized directly from JSON.
    /// </summary>
    public static bool NormalizeSerializedPlaceholders(StarSystemRuntimeState system)
    {
        var station = system?.Station;
        if (station == null) return false;
        bool changed = false;
        if (IsSerializedEmptyOperation(station.Development))
        { station.Development = null; changed = true; }
        if (IsSerializedEmptyOperation(station.Recovery))
        { station.Recovery = null; changed = true; }
        // An intentionally empty system slot can be materialized in exactly the same way.
        if (string.IsNullOrEmpty(station.ConfigId) && station.StationType == default &&
            station.Level == 0 && !station.IsDestroyed && !station.ConfigMissing && station.Revision == 0 &&
            station.Development == null && station.Recovery == null)
        { system.Station = null; changed = true; }
        return changed;
    }

    public static bool HasRecoveryOperation(StationRuntimeState station) =>
        station?.Recovery != null && !IsSerializedEmptyOperation(station.Recovery);

    private static bool IsSerializedEmptyOperation(StationOperationRuntimeState operation) =>
        operation != null && operation.Kind == StationOperationKind.None &&
        string.IsNullOrEmpty(operation.OperationId) && string.IsNullOrEmpty(operation.PaymentOptionId) &&
        operation.TargetLevel == 0 && operation.DurationSeconds == 0d && operation.ElapsedSeconds == 0d &&
        operation.PauseReason == StationOperationPauseReason.None &&
        (operation.PaidCosts == null || operation.PaidCosts.Length == 0);

    public static bool Normalize(StarSystemRuntimeState system)
    {
        bool removedPlaceholders = NormalizeSerializedPlaceholders(system);
        var station = system?.Station;
        if (station == null) return removedPlaceholders; // Legacy binding needs session config access.
        long previousRevision = station.Revision;
        int previousDamage = system.InfrastructureDamage;
        var previousDamageState = system.InfrastructureDamageState;
        // Clearing serializer-created null placeholders is not a gameplay state change.
        bool changed = false;
        int level = Math.Max(0, Math.Min(MaxLevel, station.Level));
        if (level != station.Level) { station.Level = level; changed = true; }
        if (system.StationStateVersion < SchemaVersion)
        { system.StationStateVersion = SchemaVersion; changed = true; }
        changed |= NormalizeOperation(station.Development);
        changed |= NormalizeOperation(station.Recovery);
        if (changed) station.Revision++;
        ApplySystemStatus(station, system.SystemStatus);
        if (station.IsDestroyed)
        {
            system.InfrastructureDamage = 100;
            system.InfrastructureDamageState = SystemInfrastructureDamageState.Destroyed;
            if (station.Development != null) station.Development.PauseReason = StationOperationPauseReason.Destroyed;
        }
        else if (station.HealthVersion == 0 && system.HasDestroyedInfrastructure()) Destroy(station);
        changed |= StationInfrastructureRules.EnsureHealth(system);
        RefreshDevelopmentPause(system);
        return removedPlaceholders || changed || previousRevision != station.Revision || previousDamage != system.InfrastructureDamage ||
            previousDamageState != system.InfrastructureDamageState;
    }

    private static bool NormalizeOperation(StationOperationRuntimeState operation)
    {
        if (operation == null) return false;
        bool changed = false;
        // Corrupt non-finite clocks are rejected by save validation, never changed into free completion.
        if (IsFinite(operation.ElapsedSeconds) && operation.ElapsedSeconds < 0)
        { operation.ElapsedSeconds = 0; changed = true; }
        if (IsFinite(operation.DurationSeconds) && operation.DurationSeconds >= 0 &&
            IsFinite(operation.ElapsedSeconds) && operation.ElapsedSeconds > operation.DurationSeconds)
        { operation.ElapsedSeconds = operation.DurationSeconds; changed = true; }
        return changed;
    }

    public static IEnumerable<string> GetValidationErrors(GalaxyRuntimeState galaxy)
    {
        if (galaxy == null) yield break;
        if (galaxy.StationLastRepairTick < 0 || !IsFinite(galaxy.StationOfflineRepairSeconds) || galaxy.StationOfflineRepairSeconds < 0)
            yield return "Invalid station repair tick clock.";
        if (galaxy.Systems == null) yield break;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var system in galaxy.Systems)
        {
            if (system == null) continue;
            if (string.IsNullOrWhiteSpace(system.SystemId))
            { yield return "Station system has an empty SystemId."; continue; }
            if (!ids.Add(system.SystemId)) yield return "Duplicate system/station owner: " + system.SystemId;
            if (system.StationStateVersion > SchemaVersion) yield return "Unsupported station schema: " + system.SystemId;
            var station = system.Station;
            if (station == null) continue;
            if (station.HealthVersion > 1 || station.HealthVersion < 0 ||
                (station.HealthVersion == 1 && (station.MaxHealth <= 0 || station.Health < 0 || station.Health > station.MaxHealth ||
                (!station.IsDestroyed && station.Health == 0))))
                yield return "Invalid station health: " + system.SystemId;
            if (!IsFinite(station.RepairDamageCredit) || station.RepairDamageCredit < 0 || station.RepairDamageCredit >= 1)
                yield return "Invalid station repair credit: " + system.SystemId;
            if (string.IsNullOrWhiteSpace(station.ConfigId)) yield return "Station config ID is empty: " + system.SystemId;
            if (!Enum.IsDefined(typeof(StationType), station.StationType)) yield return "Unknown station type: " + system.SystemId;
            if (HasInvalidClockOrIdentity(station.Development) || HasInvalidClockOrIdentity(station.Recovery))
                yield return "Invalid station operation clock/identity: " + system.SystemId;
            if (station.Recovery != null && (station.Recovery.Kind != StationOperationKind.Recovery ||
                !station.IsDestroyed || station.Recovery.TargetLevel != station.Level))
                yield return "Invalid station recovery: " + system.SystemId;
            if (station.Development != null &&
                ((station.Development.Kind != StationOperationKind.Construction && station.Development.Kind != StationOperationKind.Upgrade) ||
                station.Development.TargetLevel != station.Level + 1 || station.Development.TargetLevel > MaxLevel ||
                (station.Development.Kind == StationOperationKind.Construction) != (station.Level == 0)))
                yield return "Invalid station development: " + system.SystemId;
        }
    }

    private static bool HasInvalidClockOrIdentity(StationOperationRuntimeState operation) =>
        operation != null && (string.IsNullOrWhiteSpace(operation.OperationId) ||
            !IsFinite(operation.DurationSeconds) || operation.DurationSeconds < 0 ||
            !IsFinite(operation.ElapsedSeconds) || operation.ElapsedSeconds < 0 ||
            operation.ElapsedSeconds > operation.DurationSeconds);

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
