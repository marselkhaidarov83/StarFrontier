using System;
using System.Linq;

public static class StationInfrastructureRules
{
    public static int DefaultMaxHealth(int level) => 400 + 50 * (Math.Max(1, Math.Min(10, level)) - 1);

    public static double DamagePercent(StationRuntimeState station, int legacyDamage = 0) =>
        station != null && station.HealthVersion > 0 && station.MaxHealth > 0
            ? (station.MaxHealth - station.Health) * 100d / station.MaxHealth : legacyDamage;

    // Missing fields in old JSON are identified by HealthVersion, never by Health == 0.
    public static bool EnsureHealth(StarSystemRuntimeState system, int configuredMaximum = 0)
    {
        var station = system?.Station;
        if (station == null) return false;
        int maximum = configuredMaximum > 0 ? configuredMaximum :
            station.HealthVersion > 0 && station.HealthLevel == station.Level && station.MaxHealth > 0
                ? station.MaxHealth : DefaultMaxHealth(station.Level);
        bool changed = false;
        if (station.HealthVersion == 0)
        {
            int damage = Math.Max(0, Math.Min(100, system.InfrastructureDamage));
            station.Health = station.IsDestroyed || system.HasDestroyedInfrastructure() ? 0 :
                Math.Max(1, maximum - (int)Math.Ceiling(maximum * damage / 100d));
            station.MaxHealth = maximum; station.HealthLevel = station.Level; station.HealthVersion = 1;
            // Preserve both whole and fractional HP represented by old percentage-point credit.
            double credit = station.RepairDamageCredit * maximum / 100d;
            if (!station.IsDestroyed)
                station.Health = (int)Math.Min(maximum, station.Health + Math.Floor(credit));
            station.RepairDamageCredit = station.Health >= maximum || station.IsDestroyed ? 0 : credit % 1d;
            changed = true;
        }
        else if (station.MaxHealth != maximum || station.HealthLevel != station.Level)
        {
            int missing = Math.Max(0, station.MaxHealth - station.Health);
            station.MaxHealth = maximum; station.HealthLevel = station.Level;
            station.Health = station.IsDestroyed ? 0 : Math.Max(1, maximum - missing);
            changed = true;
        }
        if (station.IsDestroyed && station.Health != 0) { station.Health = 0; changed = true; }
        UpdateDamage(system);
        if (changed) station.Revision++;
        return changed;
    }

    public static void UpdateDamage(StarSystemRuntimeState system)
    {
        var station = system.Station;
        system.InfrastructureDamage = station.IsDestroyed || station.Health <= 0 ? 100 :
            station.Health >= station.MaxHealth ? 0 :
                Math.Max(1, Math.Min(99, (int)Math.Round(DamagePercent(station), MidpointRounding.AwayFromZero)));
        system.InfrastructureDamageState = system.InfrastructureDamage >= 100 ? SystemInfrastructureDamageState.Destroyed :
            system.InfrastructureDamage > 0 ? SystemInfrastructureDamageState.Damaged : SystemInfrastructureDamageState.Intact;
    }

    public static void RepairFully(StarSystemRuntimeState system)
    {
        EnsureHealth(system);
        system.Station.Health = system.Station.MaxHealth;
        system.Station.RepairDamageCredit = 0;
        UpdateDamage(system);
    }

    public static bool CanCapture(StarSystemRuntimeState system, bool hasMilitary) =>
        system != null && !hasMilitary && (system.Station == null || system.Station.Level <= 0 || system.Station.IsDestroyed);

    public static float Efficiency(StarSystemRuntimeState system, StationLevelEffectsSettings settings) =>
        system?.Station != null && system.Station.IsOperational && !system.Station.ConfigMissing &&
        system.SystemStatus != StarSystemStatus.Captured && settings != null
            ? settings.Efficiency(DamagePercent(system.Station, system.InfrastructureDamage)) : 0f;

    // Called only for completed game ticks, never for render frames or partial offline ticks.
    public static bool AutoRepair(StarSystemRuntimeState system, StationLevelEffectsSettings settings,
        bool hasEnemies, int completedTicks = 1)
    {
        var station = system?.Station;
        if (station == null || !station.IsOperational || station.ConfigMissing ||
            system.SystemStatus == StarSystemStatus.Captured || hasEnemies || settings == null ||
            settings.HealthPerTick <= 0 || completedTicks <= 0 ||
            (station.HealthVersion == 0 && system.InfrastructureDamage >= 100)) return false;
        // JsonUtility can materialize null operations as empty objects; those are not active recovery.
        StationLifecycleRules.NormalizeSerializedPlaceholders(system);
        if (station.Recovery != null) return false;
        EnsureHealth(system);
        if (station.Health <= 0 || station.Health >= station.MaxHealth) return false;
        long gain = (long)settings.HealthPerTick * completedTicks;
        station.Health = (int)Math.Min(station.MaxHealth, station.Health + gain);
        station.RepairDamageCredit = 0;
        UpdateDamage(system);
        station.Revision++;
        return true;
    }

    public static bool TryRepairQuote(StationConfig config, StationRuntimeState station, double damage,
        string optionId, out StationOperationRuntimeState quote)
    {
        quote = null;
        var options = config?.LevelEffects?.GetLevel(station?.Level ?? 0)?.RepairPaymentOptions;
        if (station == null || !station.IsOperational || station.ConfigMissing || damage <= 0 || options == null) return false;
        var matches = options.Where(x => x != null && x.Id == optionId).ToArray();
        if (matches.Length != 1 || !matches[0].TryCreateSnapshot(StationOperationKind.Repair, station.Level, out quote)) return false;
        quote.DurationSeconds = 0;
        foreach (var cost in quote.PaidCosts)
            cost.Amount = (int)Math.Max(1, Math.Ceiling(cost.Amount * Math.Min(100, damage) / 100d));
        return true;
    }
}
