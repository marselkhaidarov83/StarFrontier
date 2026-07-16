using System.Collections.Generic;

public static class GalaxyRuntimeStateFactory
{
    private static bool _seectorAllOpened = true;
    private static bool _routeAllUnlocked = true;

    public static GalaxyRuntimeState CreateNewGalaxyRuntimeState(GalaxyConfig config)
    {
        GalaxyRuntimeState state = new GalaxyRuntimeState
        {
            GalaxyDay = 1,
            CurrentSystemId = string.Empty,
            Sectors = new List<SectorRuntimeState>(),
            Systems = new List<StarSystemRuntimeState>(),
            Routes = new List<RouteRuntimeState>()
        };

        if (config == null || config.Sectors == null)
            return state;

        foreach (SectorConfig sectorConfig in config.Sectors)
        {
            if (sectorConfig == null)
                continue;

            SectorRuntimeState sectorState = new SectorRuntimeState
            {
                SectorId = sectorConfig.Id,
                IsUnlocked = _seectorAllOpened || sectorConfig.IsUnlocked
            };

            state.Sectors.Add(sectorState);

            if (sectorConfig.Systems == null)
                continue;

            foreach (StarSystemConfig systemConfig in sectorConfig.Systems)
            {
                if (systemConfig == null)
                    continue;

                bool isDiscoveredAtStart = systemConfig.IsStartSystem || !systemConfig.IsHiddenAtStart;

                StarSystemRuntimeState systemState = new StarSystemRuntimeState
                {
                    SystemId = systemConfig.Id,
                    // IsDiscovered = isDiscoveredAtStart,
                    IsDiscovered = true,
                    IsVisited = systemConfig.IsStartSystem,
                    DevelopmentLevel = 1,
                    DangerLevel = 0,
                    Stability = 100
                };

                state.Systems.Add(systemState);

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
                        IsUnlocked = _routeAllUnlocked || !routeConfig.IsLockedAtStart
                    };

                    state.Routes.Add(routeState);
                }
            }
        }

        return state;
    }
}
