using System.Collections.Generic;
using UnityEngine;

public class GalaxySimulationService : IGalaxySimulationService
{
    public GalaxySimulationService()
    {
        IGameSessionService gameSessionService  = Bootstrapper2A.Instance.ServiceRegistry.Get<IGameSessionService>();
        IConfigService configService = Bootstrapper2A.Instance.ServiceRegistry.Get<IConfigService>();
    }

    public GalaxyState CreateGalaxyState(GalaxyConfig config)
    {
        if (config == null)
        {
            Debug.LogError("GalaxySimulationService: GalaxyConfig is null.");
            return new GalaxyState();
        }

        var state = new GalaxyState
        {
            CurrentSystemId = config.StartSystemId,
            Sectors = new List<SectorState>(),
            Systems = new List<StarSystemState>(),
            Routes = new List<RouteState>()
        };

        CreateSectorStates(config, state);
        CreateStarSystemStates(config, state);
        CreateRouteStates(config, state);

        return state;
    }

    private void CreateSectorStates(GalaxyConfig config, GalaxyState state)
    {
        foreach (var sectorConfig in config.Sectors)
        {
            if (sectorConfig == null)
                continue;

            var sectorState = new SectorState
            {
                Id = sectorConfig.Id,
                IsUnlocked = sectorConfig.UnlockedAtStart,
                Stability = 100,
                ThreatLevel = 0
            };

            state.Sectors.Add(sectorState);
        }
    }

    private void CreateStarSystemStates(GalaxyConfig config, GalaxyState state)
    {
        foreach (var sectorConfig in config.Sectors)
        {
            if (sectorConfig == null)
                continue;

            if (sectorConfig.Systems == null)
                continue;

            foreach (var systemConfig in sectorConfig.Systems)
            {
                if (systemConfig == null)
                    continue;

                var systemState = new StarSystemState
                {
                    Id = systemConfig.Id,
                    SectorId = sectorConfig.Id,
                    IsDiscovered = systemConfig.DiscoveredAtStart,
                    IsUnlocked = systemConfig.UnlockedAtStart,
                    IsCaptured = systemConfig.CapturedAtStart,
                    ThreatLevel = systemConfig.StartThreatLevel
                };

                state.Systems.Add(systemState);
            }
        }
    }

    private void CreateRouteStates(GalaxyConfig config, GalaxyState state)
    {
        foreach (var routeConfig in config.Routes)
        {
            if (routeConfig == null)
                continue;

            var routeState = new RouteState
            {
                Id = routeConfig.Id,
                FromSystemId = routeConfig.FromSystemId,
                ToSystemId = routeConfig.ToSystemId,
                IsUnlocked = routeConfig.UnlockedAtStart,
                FuelCost = routeConfig.FuelCost
            };

            state.Routes.Add(routeState);
        }
    }
}