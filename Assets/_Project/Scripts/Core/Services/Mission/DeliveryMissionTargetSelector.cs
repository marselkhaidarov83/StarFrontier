using System;
using System.Collections.Generic;
using System.Linq;

public sealed class DeliveryMissionTarget
{
    public StarSystemConfig System;
    public PlanetConfig Planet;
    public int FuelCost;
}

public static class DeliveryMissionTargetSelector
{
    // Dijkstra uses the same per-route fuel cost as the travel preview. Unavailable routes
    // and systems outside the allied/discovered part of the galaxy cannot become targets.
    public static List<DeliveryMissionTarget> BuildCandidates(GalaxyRuntimeState galaxy,
        IReadOnlyList<StarSystemConfig> systems, string sourceId,
        Func<RouteConfig, bool> canTraverse, Func<string, string, int> fuelCost)
    {
        var result = new List<DeliveryMissionTarget>();
        if (galaxy?.Systems == null || systems == null || canTraverse == null || fuelCost == null ||
            string.IsNullOrWhiteSpace(sourceId)) return result;
        var states = galaxy.Systems.Where(s => s != null && !string.IsNullOrWhiteSpace(s.SystemId))
            .GroupBy(s => s.SystemId).ToDictionary(g => g.Key, g => g.First());
        var configs = systems.Where(s => s != null && !string.IsNullOrWhiteSpace(s.Id))
            .GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
        bool eligible(string id) => states.TryGetValue(id, out var state) && state.IsDiscovered &&
            state.SystemStatus != StarSystemStatus.Captured && configs.ContainsKey(id);
        if (!eligible(sourceId)) return result;
        var graph = new Dictionary<string, List<(string target, int cost)>>();
        foreach (var id in configs.Keys.Where(eligible)) graph[id] = new List<(string, int)>();
        var seenRoutes = new HashSet<string>();
        foreach (var system in configs.Values)
        {
            if (system.Routes == null) continue;
            foreach (var route in system.Routes)
            {
                if (route?.FromSystem == null || route.ToSystem == null ||
                    !seenRoutes.Add(route.Id) || !canTraverse(route)) continue;
                string a = route.FromSystem.Id, b = route.ToSystem.Id;
                if (!graph.ContainsKey(a) || !graph.ContainsKey(b)) continue;
                int cost = fuelCost(a, b);
                if (cost <= 0) continue;
                graph[a].Add((b, cost));
                graph[b].Add((a, cost));
            }
        }
        var distance = new Dictionary<string, long> { [sourceId] = 0 };
        var pending = new HashSet<string> { sourceId };
        while (pending.Count > 0)
        {
            string current = pending.OrderBy(id => distance[id]).ThenBy(id => id, StringComparer.Ordinal).First();
            pending.Remove(current);
            foreach (var edge in graph[current])
            {
                long next = distance[current] + edge.cost;
                if (next > int.MaxValue || (distance.TryGetValue(edge.target, out long old) && next >= old)) continue;
                distance[edge.target] = next;
                pending.Add(edge.target);
            }
        }
        foreach (var pair in distance.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (pair.Key == sourceId || pair.Value <= 0) continue;
            var targetSystem = configs[pair.Key];
            if (targetSystem.PlanetRefs == null) continue;
            foreach (var planet in targetSystem.PlanetRefs.Where(p => p != null && p.IsInhabited).OrderBy(p => p.Id, StringComparer.Ordinal))
                result.Add(new DeliveryMissionTarget { System = targetSystem, Planet = planet, FuelCost = (int)pair.Value });
        }
        return result;
    }

    public static DeliveryMissionTarget Pick(IReadOnlyList<DeliveryMissionTarget> candidates, double roll)
    {
        if (candidates == null || candidates.Count == 0) return null;
        int index = Math.Min(candidates.Count - 1, (int)(Math.Max(0, Math.Min(1, roll)) * candidates.Count));
        return candidates[index];
    }

    public static int CalculateCredits(int fuelCost, float deflator)
    {
        if (fuelCost <= 0 || deflator <= 0 || float.IsNaN(deflator) || float.IsInfinity(deflator)) return 0;
        double credits = Math.Ceiling(fuelCost * (double)deflator);
        return credits >= int.MaxValue ? int.MaxValue : (int)credits;
    }
}
