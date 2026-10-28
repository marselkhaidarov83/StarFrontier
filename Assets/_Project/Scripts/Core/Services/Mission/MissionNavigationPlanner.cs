using System;
using System.Collections.Generic;

/// <summary>Chooses the next in-system exit without opening the galaxy map.</summary>
public static class MissionNavigationPlanner
{
    public static void Destination(MissionInstanceData mission, out string systemId, out string planetId)
    {
        systemId = mission?.TargetSystemId;
        planetId = mission?.MissionType == MissionType.Delivery ? mission.TargetPlanetId : null;
        if (mission == null) return;

        bool routeSurveyed = mission.Status != MissionStatus.Available &&
            mission.MissionType == MissionType.RouteSearch &&
            mission.Objective?.RouteIndirectSurveyed == true &&
            (!mission.Objective.RouteIsUpgrade || mission.Objective.RouteDirectSurveyed);
        bool upgradeReturning = mission.Status != MissionStatus.Available &&
            mission.MissionType == MissionType.RouteSearch &&
            mission.Objective?.RouteIsUpgrade == true &&
            mission.Objective.RouteDirectSurveyed &&
            !mission.Objective.RouteIndirectSurveyed &&
            !mission.Objective.RouteReturnedAfterDirectSurvey;
        if (mission.Status != MissionStatus.ReadyToComplete && !mission.IsReadyToTurnIn &&
            !routeSurveyed && !upgradeReturning)
            return;

        if (mission.MissionType == MissionType.Recon || mission.MissionType == MissionType.RouteSearch)
        {
            systemId = mission.SourceSystemId;
            planetId = upgradeReturning ? null : mission.SourcePlanetId;
        }
        else if (MissionBattleRules.IsBattle(mission.MissionType))
        {
            systemId = mission.TargetSystemId;
            planetId = null; // Nearest inhabited planet is chosen after entering this system.
        }
    }

    public static List<string> FindShortestUnlockedPath(
        IReadOnlyList<StarSystemConfig> systems, GalaxyRuntimeState galaxy,
        IRouteService routeService, string startSystemId, string targetSystemId,
        bool avoidDirectEdge = false, string directEdgeSourceSystemId = null,
        ICollection<string> visitedSystemIds = null)
    {
        var empty = new List<string>();
        if (systems == null || routeService == null ||
            string.IsNullOrWhiteSpace(startSystemId) || string.IsNullOrWhiteSpace(targetSystemId))
            return empty;
        if (startSystemId == targetSystemId)
            return new List<string> { startSystemId };
        string directEdgeSource = string.IsNullOrWhiteSpace(directEdgeSourceSystemId)
            ? startSystemId : directEdgeSourceSystemId;

        var distance = new Dictionary<string, int>(StringComparer.Ordinal);
        var previous = new Dictionary<string, string>(StringComparer.Ordinal);
        var pending = new HashSet<string>(StringComparer.Ordinal);
        foreach (StarSystemConfig system in systems)
        {
            if (system == null || !IsDiscovered(galaxy, system.Id)) continue;
            distance[system.Id] = int.MaxValue;
            pending.Add(system.Id);
        }
        if (!distance.ContainsKey(startSystemId) || !distance.ContainsKey(targetSystemId))
            return empty;
        distance[startSystemId] = 0;
        List<RouteConfig> routes = RouteMissionRules.AllRoutes(systems);

        while (pending.Count > 0)
        {
            string current = null;
            int best = int.MaxValue;
            foreach (string id in pending)
            {
                int candidate = distance[id];
                if (candidate < best || (candidate == best && current != null &&
                    string.CompareOrdinal(id, current) < 0))
                {
                    best = candidate;
                    current = id;
                }
            }
            if (current == null || best == int.MaxValue) break;
            pending.Remove(current);
            if (current == targetSystemId) break;

            foreach (RouteConfig route in routes)
            {
                if (route == null || !route.ContainsSystem(current) ||
                    !routeService.IsRouteUnlocked(route.Id))
                    continue;
                StarSystemConfig other = route.GetOtherSystem(current);
                if (other == null || !pending.Contains(other.Id)) continue;
                if (avoidDirectEdge &&
                    route.ConnectsSystems(directEdgeSource, targetSystemId))
                    continue;
                if (avoidDirectEdge && directEdgeSource != startSystemId &&
                    other.Id == directEdgeSource)
                    continue;
                if (visitedSystemIds != null && other.Id != startSystemId &&
                    visitedSystemIds.Contains(other.Id))
                    continue;
                int cost = Math.Max(1, route.ParsecDistance);
                if (best > int.MaxValue - cost) continue;
                int next = best + cost;
                if (next >= distance[other.Id]) continue;
                distance[other.Id] = next;
                previous[other.Id] = current;
            }
        }

        if (!previous.ContainsKey(targetSystemId)) return empty;
        var path = new List<string> { targetSystemId };
        string at = targetSystemId;
        while (at != startSystemId)
        {
            if (!previous.TryGetValue(at, out at)) return empty;
            path.Add(at);
        }
        path.Reverse();
        return path;
    }

    private static bool IsDiscovered(GalaxyRuntimeState galaxy, string systemId)
    {
        if (galaxy?.Systems == null) return true;
        foreach (StarSystemRuntimeState system in galaxy.Systems)
            if (system != null && system.SystemId == systemId)
                return system.IsDiscovered;
        return false;
    }
}
