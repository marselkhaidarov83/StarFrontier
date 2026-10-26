using System;
using System.Collections.Generic;
using System.Linq;

public static class RouteMissionRules
{
    public static List<RouteConfig> AllRoutes(IReadOnlyList<StarSystemConfig> systems) => systems?
        .Where(s => s != null && s.Routes != null).SelectMany(s => s.Routes)
        .Where(r => r != null && !string.IsNullOrWhiteSpace(r.Id)).GroupBy(r => r.Id)
        .Select(g => g.First()).OrderBy(r => r.Id, StringComparer.Ordinal).ToList() ?? new List<RouteConfig>();

    public static List<RouteConfig> BuildCandidates(GalaxyRuntimeState galaxy, IReadOnlyList<StarSystemConfig> systems,
        string sourceId, bool upgrade, Func<RouteConfig, bool> canTraverse, Func<string, bool> canUseSystem)
    {
        var result = new List<RouteConfig>();
        if (galaxy?.Systems == null || string.IsNullOrWhiteSpace(sourceId) || !canUseSystem(sourceId)) return result;
        var all = AllRoutes(systems);
        foreach (var route in all.Where(r => r.ContainsSystem(sourceId)))
        {
            var target = route.GetOtherSystem(sourceId);
            var targetState = galaxy.Systems.Find(s => s != null && s.SystemId == target?.Id);
            var state = galaxy.Routes?.Find(r => r != null && r.RouteId == route.Id);
            if (target == null || state == null || targetState == null || !targetState.IsDiscovered ||
                targetState.SystemStatus == StarSystemStatus.Captured || !canUseSystem(target.Id)) continue;
            if (upgrade ? !canTraverse(route) || MissionBattleRules.RouteLevel(state.DevelopmentLevel) >= 5 : state.IsUnlocked) continue;
            if (HasIndirectPath(all, sourceId, target.Id, canTraverse, canUseSystem)) result.Add(route);
        }
        return result;
    }

    public static bool HasIndirectPath(IReadOnlyList<RouteConfig> routes, string source, string target,
        Func<RouteConfig, bool> canTraverse, Func<string, bool> canUseSystem)
    {
        var graph = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var route in routes)
        {
            if (route?.FromSystem == null || route.ToSystem == null || route.ConnectsSystems(source, target) || !canTraverse(route)) continue;
            string a = route.FromSystem.Id, b = route.ToSystem.Id;
            if (!canUseSystem(a) || !canUseSystem(b)) continue;
            if (!graph.ContainsKey(a)) graph[a] = new List<string>();
            if (!graph.ContainsKey(b)) graph[b] = new List<string>();
            graph[a].Add(b); graph[b].Add(a);
        }
        var seen = new HashSet<string>(StringComparer.Ordinal) { source };
        var queue = new Queue<string>(); queue.Enqueue(source);
        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            if (!graph.TryGetValue(current, out var next)) continue;
            foreach (var id in next)
            {
                if (!seen.Add(id)) continue;
                if (id == target) return true;
                queue.Enqueue(id);
            }
        }
        return false;
    }

    // A survey is a continuous, loop-free trip from issuer system to destination.
    // Reverse trips and failed jumps never contribute; each survey can be performed in either order.
    public static void ObserveHop(MissionObjectiveData objective, string source, string target, string from, string to)
    {
        objective.RouteSurveySystemIds ??= new List<string>();
        var path = objective.RouteSurveySystemIds;
        if (from == source) { path.Clear(); path.Add(source); }
        if (path.Count == 0 || path[path.Count - 1] != from) { path.Clear(); return; }
        if (path.Contains(to))
        {
            path.Clear();
            if (to == source) path.Add(source);
            return;
        }
        path.Add(to);
        if (to != target) return;
        if (path.Count == 2) objective.RouteDirectSurveyed = true;
        else objective.RouteIndirectSurveyed = true;
        path.Clear();
    }
}
