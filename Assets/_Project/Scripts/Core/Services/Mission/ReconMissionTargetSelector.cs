using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ReconMissionCandidate
{
    public string SystemId;
    public string LastAlliedSystemId;
    public int EnemyJumps;
}

public static class ReconMissionTargetSelector
{
    public static bool IsEnemy(GalaxyRuntimeState galaxy, string systemId) =>
        galaxy?.Systems?.Any(s => s != null && s.SystemId == systemId && s.SystemStatus == StarSystemStatus.Captured) == true;

    public static List<ReconMissionCandidate> BuildCandidates(GalaxyRuntimeState galaxy,
        IReadOnlyList<StarSystemConfig> systems, string sourceSystemId, Func<RouteConfig, bool> canTraverse)
    {
        var result = new List<ReconMissionCandidate>();
        if (galaxy?.Systems == null || systems == null || string.IsNullOrWhiteSpace(sourceSystemId)) return result;
        var runtime = galaxy.Systems.Where(s => s != null && !string.IsNullOrWhiteSpace(s.SystemId))
            .GroupBy(s => s.SystemId).ToDictionary(g => g.Key, g => g.First());
        if (!runtime.TryGetValue(sourceSystemId, out var source) || source.SystemStatus == StarSystemStatus.Captured) return result;
        var graph = runtime.Keys.ToDictionary(id => id, id => new HashSet<string>());
        foreach (var system in systems)
        {
            if (system?.Routes == null) continue;
            foreach (var route in system.Routes)
            {
                if (route?.FromSystem == null || route.ToSystem == null || !canTraverse(route)) continue;
                string a = route.FromSystem.Id, b = route.ToSystem.Id;
                if (!graph.ContainsKey(a) || !graph.ContainsKey(b)) continue;
                graph[a].Add(b); graph[b].Add(a);
            }
        }
        // Restrict the search to the actual connected component of the issuer.
        var connected = new HashSet<string> { sourceSystemId };
        var queue = new Queue<string>(); queue.Enqueue(sourceSystemId);
        while (queue.Count > 0)
            foreach (var neighbor in graph[queue.Dequeue()])
                if (connected.Add(neighbor)) queue.Enqueue(neighbor);
        var depth = new Dictionary<string, int>();
        var boundary = new Dictionary<string, string>();
        foreach (var id in connected.OrderBy(id => id, StringComparer.Ordinal))
        {
            if (runtime[id].SystemStatus == StarSystemStatus.Captured) continue;
            depth[id] = 0; boundary[id] = id; queue.Enqueue(id);
        }
        // A multi-source search measures hostile depth from the LAST allied system.
        while (queue.Count > 0)
        {
            string id = queue.Dequeue();
            foreach (var next in graph[id].OrderBy(x => x, StringComparer.Ordinal))
            {
                if (!connected.Contains(next) || depth.ContainsKey(next) || runtime[next].SystemStatus != StarSystemStatus.Captured) continue;
                depth[next] = depth[id] + 1;
                boundary[next] = boundary[id];
                queue.Enqueue(next);
            }
        }
        foreach (var pair in depth)
            if (pair.Value > 0)
                result.Add(new ReconMissionCandidate { SystemId = pair.Key, EnemyJumps = pair.Value, LastAlliedSystemId = boundary[pair.Key] });
        return result;
    }

    public static ReconMissionCandidate Pick(IReadOnlyList<ReconMissionCandidate> candidates,
        ReconMissionTemplateConfig config, double distanceRoll, double systemRoll)
    {
        if (candidates == null || config == null) return null;
        var groups = candidates.GroupBy(c => c.EnemyJumps).OrderBy(g => g.Key)
            .Where(g => config.GetDistanceWeight(g.Key) > 0).ToList();
        double total = groups.Sum(g => config.GetDistanceWeight(g.Key));
        if (total <= 0 || double.IsNaN(total) || double.IsInfinity(total)) return null;
        double roll = Math.Max(0, Math.Min(1, distanceRoll)) * total;
        var chosen = groups[groups.Count - 1];
        foreach (var group in groups)
        {
            roll -= config.GetDistanceWeight(group.Key);
            if (roll < 0) { chosen = group; break; }
        }
        var items = chosen.OrderBy(c => c.SystemId, StringComparer.Ordinal).ToList();
        int index = Math.Min(items.Count - 1, (int)(Math.Max(0, Math.Min(1, systemRoll)) * items.Count));
        return items[index];
    }
}
