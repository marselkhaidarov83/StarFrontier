using System.Collections.Generic;
using System.Linq;

public class GalaxyMapPathFinder
{
    private class Neighbor
    {
        public string SystemId;
        public int Cost;

        public Neighbor(string systemId, int cost)
        {
            SystemId = systemId;
            Cost = cost;
        }
    }

    public List<string> FindShortestPath(
        IReadOnlyList<StarSystemConfig> systems,
        GalaxyRuntimeState runtimeState,
        string startSystemId,
        string targetSystemId)
    {
        if (systems == null)
            return new List<string>();

        if (string.IsNullOrWhiteSpace(startSystemId))
            return new List<string>();

        if (string.IsNullOrWhiteSpace(targetSystemId))
            return new List<string>();

        Dictionary<string, List<Neighbor>> graph = BuildGraph(systems, runtimeState);

        if (!graph.ContainsKey(startSystemId))
            return new List<string>();

        if (!graph.ContainsKey(targetSystemId))
            return new List<string>();

        if (startSystemId == targetSystemId) return new List<string> { startSystemId };

        Dictionary<string, long> distances = new();
        Dictionary<string, string> previous = new();
        HashSet<string> unvisited = new();

        foreach (string systemId in graph.Keys)
        {
            distances[systemId] = long.MaxValue;
            unvisited.Add(systemId);
        }

        distances[startSystemId] = 0;

        while (unvisited.Count > 0)
        {
            string currentSystemId = GetClosestUnvisited(unvisited, distances);

            if (string.IsNullOrEmpty(currentSystemId))
                break;

            if (currentSystemId == targetSystemId)
                break;

            unvisited.Remove(currentSystemId);

            foreach (Neighbor neighbor in graph[currentSystemId])
            {
                if (!unvisited.Contains(neighbor.SystemId))
                    continue;

                long currentDistance = distances[currentSystemId];

                if (currentDistance == long.MaxValue)
                    continue;

                long newDistance = currentDistance + neighbor.Cost;

                if (newDistance < distances[neighbor.SystemId])
                {
                    distances[neighbor.SystemId] = newDistance;
                    previous[neighbor.SystemId] = currentSystemId;
                }
            }
        }

        return RestorePath(previous, startSystemId, targetSystemId);
    }

    // int.MaxValue обозначает недоступный путь; нулевой расход допустим только без переходов.
    public int CalculatePathCost(IReadOnlyList<StarSystemConfig> systems,
        GalaxyRuntimeState runtimeState, List<string> path)
    {
        if (systems == null || path == null || path.Count == 0) return int.MaxValue;
        var graph = BuildGraph(systems, runtimeState);
        if (path.Any(id => string.IsNullOrWhiteSpace(id) || !graph.ContainsKey(id))) return int.MaxValue;
        long total = 0;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            Neighbor edge = graph[path[i]].FirstOrDefault(n => n.SystemId == path[i + 1]);
            if (edge == null) return int.MaxValue;
            total += edge.Cost;
        }
        return (int)System.Math.Min(int.MaxValue, total);
    }

    private Dictionary<string, List<Neighbor>> BuildGraph(
        IReadOnlyList<StarSystemConfig> systems, GalaxyRuntimeState runtimeState)
    {
        var graph = new Dictionary<string, List<Neighbor>>();
        foreach (StarSystemConfig system in systems)
            if (system != null && !string.IsNullOrWhiteSpace(system.Id) && CanUseSystem(runtimeState, system.Id))
                graph[system.Id] = new List<Neighbor>();
        foreach (StarSystemConfig system in systems)
        {
            if (system?.Routes == null || !graph.ContainsKey(system.Id)) continue;
            foreach (RouteConfig route in system.Routes)
            {
                StarSystemConfig other = route?.GetOtherSystem(system.Id);
                if (other == null || !graph.ContainsKey(other.Id) || !route.IsUnlocked(runtimeState)) continue;
                AddNeighbor(graph, system.Id, other.Id, route.GetFuelCost(runtimeState));
            }
        }

        return graph;
    }

    private void AddNeighbor(
        Dictionary<string, List<Neighbor>> graph,
        string fromSystemId,
        string toSystemId,
        int cost)
    {
        if (!graph.ContainsKey(fromSystemId))
            graph[fromSystemId] = new List<Neighbor>();

        Neighbor existingNeighbor = graph[fromSystemId]
            .FirstOrDefault(neighbor => neighbor.SystemId == toSystemId);

        if (existingNeighbor == null)
        {
            graph[fromSystemId].Add(new Neighbor(toSystemId, cost));
        }
        else if (cost < existingNeighbor.Cost)
        {
            existingNeighbor.Cost = cost;
        }
    }

    private bool CanUseSystem(GalaxyRuntimeState runtimeState, string systemId)
    {
        if (runtimeState?.Systems == null) return false;

        StarSystemRuntimeState state = runtimeState.Systems.FirstOrDefault(
            system => system != null && system.SystemId == systemId
        );

        return state != null && state.IsDiscovered;
    }

    private string GetClosestUnvisited(
        HashSet<string> unvisited,
        Dictionary<string, long> distances)
    {
        string closestSystemId = null;
        long closestDistance = long.MaxValue;

        foreach (string systemId in unvisited)
        {
            long distance = distances[systemId];

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestSystemId = systemId;
            }
        }

        return closestSystemId;
    }

    private List<string> RestorePath(
        Dictionary<string, string> previous,
        string startSystemId,
        string targetSystemId)
    {
        List<string> path = new();

        string currentSystemId = targetSystemId;
        path.Add(currentSystemId);

        while (currentSystemId != startSystemId)
        {
            if (!previous.ContainsKey(currentSystemId))
                return new List<string>();

            currentSystemId = previous[currentSystemId];
            path.Add(currentSystemId);
        }

        path.Reverse();
        return path;
    }
}
