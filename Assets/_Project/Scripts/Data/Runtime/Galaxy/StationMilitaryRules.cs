using System;
using System.Collections.Generic;
using System.Linq;

public static class StationMilitaryRules
{
    private const string Prefix = "station-military|";
    // Kept in SpawnRuleId so the existing compact/incremental NPC saves retain source identity.
    public static string RuleId(string systemId, string configId, string ruleId) => Prefix + systemId + "|" + configId + "|" + ruleId;
    public static string SourceSystem(SystemNpcRuntimeState npc)
    {
        string rule = npc?.SpawnRuleId;
        if (rule == null || !rule.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        var parts = rule.Split('|');
        return parts.Length == 4 ? parts[1] : null;
    }
    public static string SourceStationId(SystemNpcRuntimeState npc)
    {
        if (SourceSystem(npc) == null) return null;
        return npc.SpawnRuleId.Split('|')[2];
    }
    public static int AliveFromStation(IEnumerable<SystemNpcRuntimeState> npcs, string systemId, string stationId) =>
        string.IsNullOrEmpty(systemId) || string.IsNullOrEmpty(stationId) ? 0 :
        npcs?.Count(n => n != null && n.IsAlive && IsStationMilitary(n) &&
            SourceSystem(n) == systemId && SourceStationId(n) == stationId) ?? 0;
    public static int MaximumCapacity(StationLevelEffectsConfig config, int galaxyLevel)
    {
        int maximum = 0;
        galaxyLevel = Math.Max(1, Math.Min(10, galaxyLevel));
        for (int level = 1; level <= StationLifecycleRules.MaxLevel; level++)
        {
            var settings = config?.GetLevel(level);
            if (settings == null || settings.RequiredGalaxyLevel > galaxyLevel) break;
            maximum = Math.Max(maximum, Capacity(settings, galaxyLevel));
        }
        return maximum;
    }
    public static int Capacity(StationLevelEffectsSettings settings, int galaxyLevel)
    {
        var counts = new Dictionary<string, int>();
        foreach (var rule in settings?.Population?.AllySpawnRules ?? Array.Empty<AllySpawnRuleConfig>())
        {
            if (rule == null || rule.GetSpawnIntervalSeconds(galaxyLevel) <= 0) continue;
            foreach (var entry in rule.GetAlliesForGalaxyLevel(galaxyLevel))
            {
                if (entry?.AllyConfig == null || !entry.IsValid() || entry.AllyConfig.Role != AllyRole2A.Military) continue;
                // Production counts living ships per ally config across all rules of this base.
                string id = entry.AllyConfig.Id;
                if (string.IsNullOrEmpty(id)) continue;
                counts.TryGetValue(id, out int previous); counts[id] = Math.Max(previous, entry.MaxCount);
            }
        }
        return counts.Values.Sum();
    }
    public static bool IsStationMilitary(SystemNpcRuntimeState npc) => npc != null && npc.IsAlly &&
        npc.AllyRole == AllyRole2A.Military && SourceSystem(npc) != null;
    public static SectorConfig Sector(IEnumerable<SectorConfig> sectors, string systemId) =>
        sectors?.FirstOrDefault(s => s != null && s.Systems != null && s.Systems.Any(x => x != null && x.Id == systemId));
    public static bool CanTravel(SystemNpcRuntimeState npc, string target, IEnumerable<SectorConfig> sectors, GalaxyRuntimeState galaxy)
    {
        if (!IsStationMilitary(npc)) return true;
        var sector = Sector(sectors, SourceSystem(npc));
        return sector != null && sector.Systems.Any(x => x != null && x.Id == target) &&
            galaxy?.Systems?.Any(x => x != null && x.SystemId == target && x.SystemStatus != StarSystemStatus.Captured) == true;
    }
}
