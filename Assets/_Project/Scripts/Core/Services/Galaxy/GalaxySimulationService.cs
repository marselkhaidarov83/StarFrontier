using System.Collections.Generic;
using UnityEngine;

public class GalaxySimulationService : CustomService, IGalaxySimulationService
{
    public GalaxyState CreateGalaxyState(GalaxyConfig config)
    {
        LogCustom("");
        if (config == null)
        {
            LogCustomError("GalaxySimulationService: GalaxyConfig is null.");
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
        LogCustom("config.Sectors.Count = " + config.Sectors.Count);
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
        LogCustom("config.Sectors.Count = " + config.Sectors.Count);
        foreach (var sectorConfig in config.Sectors)
        {
            if (sectorConfig == null)
                continue;

            if (sectorConfig.Systems == null)
                continue;

            LogCustom("sectorConfig.Systems.Length = " + sectorConfig.Systems.Length);
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
        LogCustom("config.Routes.Count = " + config.Routes.Count);
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