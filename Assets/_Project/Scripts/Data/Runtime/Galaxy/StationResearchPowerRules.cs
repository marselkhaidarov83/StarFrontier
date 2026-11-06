using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Undamaged potential of all science station slots in unlocked sectors at their reachable configured levels.</summary>
public static class StationResearchPowerRules
{
    public static double Maximum(GalaxyRuntimeState galaxy, IReadOnlyList<SectorConfig> sectors)
    {
        if (galaxy?.Sectors == null || sectors == null) return 0;
        var unlocked = new HashSet<string>(galaxy.Sectors.Where(x => x != null && x.IsUnlocked).Select(x => x.SectorId));
        int galaxyLevel = Math.Max(1, Math.Min(10, unlocked.Count));
        var seen = new HashSet<string>(); double total = 0;
        foreach (var sector in sectors)
        {
            if (sector == null || !unlocked.Contains(sector.Id)) continue;
            foreach (var system in sector.Systems ?? Array.Empty<StarSystemConfig>())
            {
                var station = system?.Station;
                if (station == null || !station.IsActive || station.StationType != StationType.Science || !seen.Add(system.Id)) continue;
                total += MaximumForLevels(station.LevelEffects, galaxyLevel);
            }
        }
        return total;
    }
    public static double MaximumForLevels(StationLevelEffectsConfig config, int galaxyLevel)
    {
        double maximum = 0;
        for (int level = 1; level <= StationLifecycleRules.MaxLevel; level++)
        {
            var settings = config?.GetLevel(level);
            if (settings == null || settings.RequiredGalaxyLevel > galaxyLevel) break;
            if (!float.IsNaN(settings.ScienceRate) && !float.IsInfinity(settings.ScienceRate))
                maximum = Math.Max(maximum, settings.ScienceRate);
        }
        return maximum;
    }
}
