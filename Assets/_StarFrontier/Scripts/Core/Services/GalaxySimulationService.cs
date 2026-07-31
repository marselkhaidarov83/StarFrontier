using UnityEngine;

public class GalaxySimulationService
{
    private readonly GalaxyGenerationConfig _config;
    private readonly GalaxyState _state;
    private readonly SimpleEventBus _eventBus;

    public GalaxySimulationService(
        GalaxyGenerationConfig config,
        GalaxyState state,
        SimpleEventBus eventBus)
    {
        _config = config;
        _state = state;
        _eventBus = eventBus;
    }

    public GalaxyState State => _state;

    public void EnsureGenerated()
    {
        if (_state.IsGenerated)
            return;

        GenerateNewGalaxy();
    }

    public void GenerateNewGalaxy()
    {
        ClearState();

        Random.InitState(_config.Seed);

        CreateSectorsAndSystems();
        CreateRoutesInsideSectors();

        SetStartingSystem();

        _state.IsGenerated = true;

        _eventBus.Publish(new GalaxyGeneratedEvent(
            _state.Sectors.Count,
            _state.Systems.Count,
            _state.Routes.Count));
    }

    private void ClearState()
    {
        _state.IsGenerated = false;
        _state.CurrentSectorId = string.Empty;
        _state.CurrentSystemId = string.Empty;

        _state.Sectors.Clear();
        _state.Systems.Clear();
        _state.Routes.Clear();
    }

    private void CreateSectorsAndSystems()
    {
        for (int sectorIndex = 0; sectorIndex < _config.SectorCount; sectorIndex++)
        {
            string sectorId = CreateSectorId(sectorIndex);

            SectorState sector = new SectorState
            {
                Id = sectorId,
                DisplayName = $"Sector {sectorIndex + 1}",
                SectorIndex = sectorIndex,
                IsUnlocked = sectorIndex == 0,
                IsCompleted = false
            };

            Vector2 sectorCenter = new Vector2(
                sectorIndex * _config.SectorDistance,
                0f);

            for (int systemIndex = 0; systemIndex < _config.SystemsPerSector; systemIndex++)
            {
                string systemId = CreateSystemId(sectorIndex, systemIndex);
                Vector2 position = GetValidSystemPosition(sectorCenter);

                StarSystemState system = new StarSystemState
                {
                    Id = systemId,
                    SectorId = sectorId,
                    DisplayName = $"System {sectorIndex + 1}-{systemIndex + 1}",

                    MapX = position.x,
                    MapY = position.y,

                    IsDiscovered = sectorIndex == 0 && systemIndex == 0,
                    IsVisited = sectorIndex == 0 && systemIndex == 0,
                    IsCurrent = sectorIndex == 0 && systemIndex == 0,

                    DangerLevel = GetDangerLevelForSector(sectorIndex),
                    ContentType = GetContentTypeForSystem(sectorIndex, systemIndex)
                };

                sector.SystemIds.Add(system.Id);
                _state.Systems.Add(system);
            }

            _state.Sectors.Add(sector);
        }
    }

    private Vector2 GetValidSystemPosition(Vector2 sectorCenter)
    {
        const int maxAttempts = 50;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 candidate = sectorCenter + Random.insideUnitCircle * _config.SystemSpread;

            bool valid = true;

            foreach (StarSystemState existingSystem in _state.Systems)
            {
                Vector2 existingPosition = new Vector2(existingSystem.MapX, existingSystem.MapY);
                float distance = Vector2.Distance(candidate, existingPosition);

                if (distance < _config.MinSystemDistance)
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
                return candidate;
        }

        return sectorCenter + Random.insideUnitCircle * _config.SystemSpread;
    }

    private void CreateRoutesInsideSectors()
    {
        foreach (SectorState sector in _state.Sectors)
        {
            for (int i = 0; i < sector.SystemIds.Count - 1; i++)
            {
                string fromSystemId = sector.SystemIds[i];
                string toSystemId = sector.SystemIds[i + 1];

                RouteState route = new RouteState
                {
                    Id = CreateRouteId(fromSystemId, toSystemId),
                    FromSystemId = fromSystemId,
                    ToSystemId = toSystemId,

                    IsUnlocked = sector.IsUnlocked,
                    IsDiscovered = sector.IsUnlocked,

                    FuelCost = 1,
                    Difficulty = sector.SectorIndex + 1
                };

                _state.Routes.Add(route);
            }
        }
    }

    private void SetStartingSystem()
    {
        StarSystemState startingSystem = FindSystem(_config.StartingSystemId);

        if (startingSystem == null && _state.Systems.Count > 0)
            startingSystem = _state.Systems[0];

        if (startingSystem == null)
            return;

        foreach (StarSystemState system in _state.Systems)
            system.IsCurrent = false;

        startingSystem.IsCurrent = true;
        startingSystem.IsVisited = true;
        startingSystem.IsDiscovered = true;

        _state.CurrentSystemId = startingSystem.Id;
        _state.CurrentSectorId = startingSystem.SectorId;
    }

    private StarSystemState FindSystem(string systemId)
    {
        foreach (StarSystemState system in _state.Systems)
        {
            if (system.Id == systemId)
                return system;
        }

        return null;
    }

    private string CreateSectorId(int sectorIndex)
    {
        return $"sector_{sectorIndex + 1:000}";
    }

    private string CreateSystemId(int sectorIndex, int systemIndex)
    {
        return $"system_{sectorIndex + 1:000}_{systemIndex + 1:000}";
    }

    private string CreateRouteId(string fromSystemId, string toSystemId)
    {
        return $"route_{fromSystemId}_{toSystemId}";
    }

    private StarSystemDangerLevel GetDangerLevelForSector(int sectorIndex)
    {
        if (sectorIndex <= 0)
            return StarSystemDangerLevel.Low;

        if (sectorIndex == 1)
            return StarSystemDangerLevel.Medium;

        return StarSystemDangerLevel.High;
    }

    private StarSystemContentType GetContentTypeForSystem(int sectorIndex, int systemIndex)
    {
        if (sectorIndex == 0 && systemIndex == 0)
            return StarSystemContentType.Civilian;

        if (systemIndex % 5 == 0)
            return StarSystemContentType.Mining;

        if (systemIndex % 4 == 0)
            return StarSystemContentType.Trade;

        if (systemIndex % 3 == 0)
            return StarSystemContentType.Pirate;

        return StarSystemContentType.Empty;
    }
}