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

        if (startSystemId == targetSystemId)
            return new List<string> { startSystemId };

        Dictionary<string, List<Neighbor>> graph = BuildGraph(systems, runtimeState);

        if (!graph.ContainsKey(startSystemId))
            return new List<string>();

        if (!graph.ContainsKey(targetSystemId))
            return new List<string>();

        Dictionary<string, int> distances = new();
        Dictionary<string, string> previous = new();
        HashSet<string> unvisited = new();

        foreach (string systemId in graph.Keys)
        {
            distances[systemId] = int.MaxValue;
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

                int currentDistance = distances[currentSystemId];

                if (currentDistance == int.MaxValue)
                    continue;

                int newDistance = currentDistance + neighbor.Cost;

                if (newDistance < distances[neighbor.SystemId])
                {
                    distances[neighbor.SystemId] = newDistance;
                    previous[neighbor.SystemId] = currentSystemId;
                }
            }
        }

        return RestorePath(previous, startSystemId, targetSystemId);
    }

    public int CalculatePathCost(
        IReadOnlyList<StarSystemConfig> systems,
        List<string> path)
    {
        if (systems == null || path == null || path.Count < 2)
            return 0;

        int totalCost = 0;

        for (int i = 0; i < path.Count - 1; i++)
        {
            StarSystemConfig fromSystem = systems.FirstOrDefault(system => system.Id == path[i]);

            if (fromSystem == null || fromSystem.LinkedSystems == null)
                continue;

            StarSystemLink link = fromSystem.LinkedSystems.FirstOrDefault(
                systemLink =>
                    systemLink != null &&
                    systemLink.LinkedSystem != null &&
                    systemLink.LinkedSystem.Id == path[i + 1]
            );

            if (link != null)
                totalCost += link.ParsecDistance;
        }

        return totalCost;
    }

    private Dictionary<string, List<Neighbor>> BuildGraph(
        IReadOnlyList<StarSystemConfig> systems,
        GalaxyRuntimeState runtimeState)
    {
        Dictionary<string, List<Neighbor>> graph = new();

        foreach (StarSystemConfig systemConfig in systems)
        {
            if (systemConfig == null)
                continue;

            if (!CanUseSystem(runtimeState, systemConfig.Id))
                continue;

            if (!graph.ContainsKey(systemConfig.Id))
                graph[systemConfig.Id] = new List<Neighbor>();

            if (systemConfig.LinkedSystems == null)
                continue;

            foreach (StarSystemLink link in systemConfig.LinkedSystems)
            {
                if (link == null || link.LinkedSystem == null)
                    continue;

                string linkedSystemId = link.LinkedSystem.Id;

                if (!CanUseSystem(runtimeState, linkedSystemId))
                    continue;

                int cost = link.ParsecDistance <= 0 ? 1 : link.ParsecDistance;

                AddNeighbor(graph, systemConfig.Id, linkedSystemId, cost);
                AddNeighbor(graph, linkedSystemId, systemConfig.Id, cost);
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
        if (runtimeState == null || runtimeState.Systems == null)
            return true;

        StarSystemRuntimeState state = runtimeState.Systems.FirstOrDefault(
            system => system.SystemId == systemId
        );

        return state != null && state.IsDiscovered;
    }

    private string GetClosestUnvisited(
        HashSet<string> unvisited,
        Dictionary<string, int> distances)
    {
        string closestSystemId = null;
        int closestDistance = int.MaxValue;

        foreach (string systemId in unvisited)
        {
            int distance = distances[systemId];

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