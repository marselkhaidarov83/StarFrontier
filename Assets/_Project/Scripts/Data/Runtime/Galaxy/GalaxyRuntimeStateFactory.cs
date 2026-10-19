using System.Collections.Generic;

public static class GalaxyRuntimeStateFactory
{
    public static GalaxyRuntimeState CreateNewGalaxyRuntimeState(
        GalaxyConfig config,
        bool sectorAllOpened,
        bool routeAllUnlocked,
        bool mobileTestMode = false,
        int unlockedSectorCount = 0,
        int alliedSectorCount = 0)
    {

        GalaxyRuntimeState state = new GalaxyRuntimeState
        {
            GalaxyDay = 1,
            CurrentSystemId = string.Empty,
            Sectors = new List<SectorRuntimeState>(),
            Systems = new List<StarSystemRuntimeState>(),
            Routes = new List<RouteRuntimeState>(),
            EnemyFactions = new List<EnemyFactionState>(),
            EnemyGroups = new List<EnemyGroupState>(),
            Invasions = new List<InvasionState>()
        };

        if (config == null || config.Sectors == null)
            return state;

        var orderedSectors = new List<SectorConfig>();
        foreach (SectorConfig sector in config.Sectors)
        {
            if (sector != null)
                orderedSectors.Add(sector);
        }
        if (mobileTestMode)
            orderedSectors.Sort((left, right) => left.Order.CompareTo(right.Order));

        for (int sectorIndex = 0; sectorIndex < orderedSectors.Count; sectorIndex++)
        {
            SectorConfig sectorConfig = orderedSectors[sectorIndex];

            SectorRuntimeState sectorState = new SectorRuntimeState
            {
                SectorId = sectorConfig.Id,
                IsUnlocked = mobileTestMode
                    ? sectorIndex < unlockedSectorCount
                    : sectorAllOpened || sectorConfig.IsUnlocked
            };

            state.Sectors.Add(sectorState);

            if (sectorConfig.Systems == null)
                continue;

            foreach (StarSystemConfig systemConfig in sectorConfig.Systems)
            {
                if (systemConfig == null)
                    continue;

                bool isEnemyAtStart =
                    mobileTestMode
                        ? sectorIndex >= System.Math.Max(0, alliedSectorCount)
                        : sectorConfig.IsEnemyAtStart;

                StarSystemRuntimeState systemState = new StarSystemRuntimeState
                {
                    SystemId = systemConfig.Id,
                    // IsDiscovered = isDiscoveredAtStart,
                    IsDiscovered = true,
                    IsVisited = systemConfig.IsStartSystem,
                    DevelopmentLevel = 1,
                    DangerLevel = 0,
                    Stability = 100,
                    SystemStatus = isEnemyAtStart
                        ? StarSystemStatus.Captured
                        : StarSystemStatus.Stable
                };

                state.Systems.Add(systemState);

                if (isEnemyAtStart)
                {
                    AddOwnedSystemToFaction(
                        state,
                        ToFactionId(sectorConfig.EnemyFactionAtStart),
                        systemConfig.Id);
                }

                if (systemConfig.IsStartSystem)
                {
                    state.CurrentSystemId = systemConfig.Id;
                }

                if (systemConfig.Routes == null)
                    continue;

                foreach (RouteConfig routeConfig in systemConfig.Routes)
                {
                    if (routeConfig == null)
                        continue;

                    RouteRuntimeState routeState = new RouteRuntimeState
                    {
                        RouteId = routeConfig.Id,
                        IsUnlocked = routeAllUnlocked || !routeConfig.IsLockedAtStart
                    };

                    state.Routes.Add(routeState);
                }
            }
        }

        RebuildInitialEnemyFrontlines(
            state,
            config);

        return state;
    }

    private static void AddOwnedSystemToFaction(
        GalaxyRuntimeState state,
        string factionId,
        string systemId)
    {
        if (state == null ||
            string.IsNullOrWhiteSpace(factionId) ||
            string.IsNullOrWhiteSpace(systemId))
        {
            return;
        }

        if (state.EnemyFactions == null)
            state.EnemyFactions = new List<EnemyFactionState>();

        EnemyFactionState factionState = null;

        for (int i = 0; i < state.EnemyFactions.Count; i++)
        {
            EnemyFactionState item =
                state.EnemyFactions[i];

            if (item == null)
                continue;

            if (NormalizeFactionId(item.FactionId) == factionId)
            {
                factionState = item;
                break;
            }
        }

        if (factionState == null)
        {
            factionState = new EnemyFactionState
            {
                FactionId = factionId,
                DisplayName = GetFactionDisplayName(factionId),
                OwnedSystemIds = new List<string>(),
                TerritorySystemIds = new List<string>(),
                FrontlineSystemIds = new List<string>(),
                FrontierSystemIds = new List<string>(),
                ActiveGroupRuntimeIds = new List<string>(),
                ActiveInvasionIds = new List<string>()
            };

            state.EnemyFactions.Add(factionState);
        }

        factionState.EnsureWarTerritoryLists();

        AddUniqueId(
            factionState.OwnedSystemIds,
            systemId);

        AddUniqueId(
            factionState.TerritorySystemIds,
            systemId);
    }
    private static void RebuildInitialEnemyFrontlines(
        GalaxyRuntimeState state,
        GalaxyConfig config)
    {
        if (state == null ||
            state.EnemyFactions == null ||
            config == null)
        {
            return;
        }

        IReadOnlyDictionary<string, IReadOnlyList<string>> neighborSystemIdsBySystemId =
            BuildNeighborSystemIdMap(config);

        for (int i = 0; i < state.EnemyFactions.Count; i++)
        {
            EnemyFactionState factionState =
                state.EnemyFactions[i];

            if (factionState == null)
                continue;

            factionState.RebuildFrontlineAndFrontier(
                neighborSystemIdsBySystemId);
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildNeighborSystemIdMap(
        GalaxyConfig config)
    {
        Dictionary<string, IReadOnlyList<string>> result =
            new Dictionary<string, IReadOnlyList<string>>();

        if (config == null || config.Sectors == null)
            return result;

        foreach (SectorConfig sectorConfig in config.Sectors)
        {
            if (sectorConfig == null ||
                sectorConfig.Systems == null)
            {
                continue;
            }

            foreach (StarSystemConfig systemConfig in sectorConfig.Systems)
            {
                if (systemConfig == null ||
                    string.IsNullOrWhiteSpace(systemConfig.Id))
                {
                    continue;
                }

                List<string> neighborIds =
                    new List<string>();

                if (systemConfig.Routes != null)
                {
                    for (int routeIndex = 0;
                         routeIndex < systemConfig.Routes.Count;
                         routeIndex++)
                    {
                        RouteConfig routeConfig =
                            systemConfig.Routes[routeIndex];

                        if (routeConfig == null)
                            continue;

                        StarSystemConfig otherSystem =
                            routeConfig.GetOtherSystem(systemConfig.Id);

                        if (otherSystem == null ||
                            string.IsNullOrWhiteSpace(otherSystem.Id))
                        {
                            continue;
                        }

                        AddUniqueId(
                            neighborIds,
                            otherSystem.Id);
                    }
                }

                result[systemConfig.Id] = neighborIds;
            }
        }

        return result;
    }

    private static string ToFactionId(
    EnemyFaction2A faction)
    {
        switch (faction)
        {
            case EnemyFaction2A.Ancients:
                return "ancients";
            case EnemyFaction2A.Infected:
                return "infected";
            default:
                return "ai";
        }
    }

    private static string NormalizeFactionId(
      string factionId)
    {
        return string.IsNullOrWhiteSpace(factionId)
            ? string.Empty
            : factionId.Trim().ToLowerInvariant();
    }

    private static string GetFactionDisplayName(
     string factionId)
    {
        switch (factionId)
        {
            case "ai":
                return "AI";
            case "ancients":
                return "Ancients";
            case "infected":
                return "Infected";
            default:
                return factionId;
        }
    }


    private static void AddUniqueId(
    List<string> ids,
    string id)
    {
        if (ids == null ||
            string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        if (!ids.Contains(id))
            ids.Add(id);
    }
}