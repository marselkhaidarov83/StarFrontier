using System.Collections.Generic;
using UnityEngine;

public sealed class TravelServiceV2 : ITravelService
{
    private const int DefaultFuelCostPerJump = 1;

    private readonly GalaxyGraphModel _galaxyGraph;
    private readonly IGameSessionService _gameSessionService;
    private readonly IConfigService _configService;
    private readonly SimpleEventBus _eventBus;

    private readonly GalaxyRuntimeState _galaxyRuntimeState;
    private readonly IGalaxyDiscoveryService _discoveryService;
    private readonly IRouteService _routeService;

    private bool _debugEnabled;

    public GalaxyGraphModel GalaxyGraph()
    {
        return _galaxyGraph;
    }

    public TravelServiceV2(
        GalaxyRuntimeState galaxyRuntimeState,
        IGalaxyDiscoveryService discoveryService,
        IRouteService routeService)
    {
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        _galaxyRuntimeState = galaxyRuntimeState;
        _discoveryService = discoveryService;
        _routeService = routeService;

        _galaxyGraph = GalaxyGraphFactory.CreateFromConfigs(_configService.GetAllStarSystems());
    }

    public bool CanTravel(string fromSystemId, string toSystemId)
    {
        return GetTravelFailReason(fromSystemId, toSystemId) == TravelFailReason.None;
    }

    public TravelResult TryTravel(string toSystemId)
    {
        string fromSystemId = GetCurrentSystemId();

        _eventBus.Publish(new TravelStartedEvent(fromSystemId, toSystemId));

        TravelFailReason failReason = GetTravelFailReason(fromSystemId, toSystemId);

        if (failReason != TravelFailReason.None)
        {
            TravelResult failedResult = TravelResult.Failed(
                failReason,
                fromSystemId,
                toSystemId
            );

            _eventBus.Publish(new TravelFinishedEvent(
                fromSystemId: failedResult.FromSystemId,
                toSystemId: failedResult.ToSystemId,
                success: false,
                fuelSpent: 0,
                failReason: failedResult.FailReason
            ));

            return failedResult;
        }

        string normalizedFromId = fromSystemId.Trim();
        string normalizedToId = toSystemId.Trim();

        int fuelCost = CalculateFuelCost(normalizedFromId, normalizedToId);

        _gameSessionService.State.Player.PlayerShipState
            .GetActiveShip()
            .CurrentFuel -= fuelCost;

        SetCurrentSystemId(normalizedToId);

        _discoveryService.VisitSystem(normalizedToId);

        TravelResult completedResult = TravelResult.Completed(
            normalizedFromId,
            normalizedToId,
            fuelCost
        );

        _eventBus.Publish(new TravelFinishedEvent(
            fromSystemId: completedResult.FromSystemId,
            toSystemId: completedResult.ToSystemId,
            success: true,
            fuelSpent: completedResult.FuelSpent,
            failReason: TravelFailReason.None
        ));

        _eventBus.Publish(new StarSystemEnteredEvent(completedResult.ToSystemId));

        _eventBus.Publish(new CurrentSystemEnteredEvent(
            normalizedToId
        ));

        return completedResult;
    }

    public void TryTravelToPlanet(string planetId)
    {
        _gameSessionService.State.Player.CurrentPlanetId = planetId;

        if (_eventBus != null)
            _eventBus.Publish(new PlanetEnteredEvent(planetId));
    }

    public TravelFailReason GetTravelFailReason(string fromSystemId, string toSystemId)
    {
        if (string.IsNullOrWhiteSpace(fromSystemId))
            return TravelFailReason.CurrentSystemMissing;

        if (string.IsNullOrWhiteSpace(toSystemId))
            return TravelFailReason.TargetSystemMissing;

        string normalizedFromId = fromSystemId.Trim();
        string normalizedToId = toSystemId.Trim();

        if (!_galaxyGraph.ContainsSystem(normalizedFromId))
            return TravelFailReason.CurrentSystemMissing;

        if (!_galaxyGraph.ContainsSystem(normalizedToId))
            return TravelFailReason.TargetSystemMissing;

        if (normalizedFromId == normalizedToId)
            return TravelFailReason.TargetSystemIsCurrent;

        if (!_galaxyGraph.AreNeighbors(normalizedFromId, normalizedToId))
            return TravelFailReason.SystemsAreNotNeighbors;

        if (!_discoveryService.IsSystemDiscovered(normalizedToId))
            return TravelFailReason.TargetSystemMissing;

        if (!_routeService.HasUnlockedRoute(normalizedFromId, normalizedToId))
            return TravelFailReason.SystemsAreNotNeighbors;

        int fuelCost = CalculateFuelCost(normalizedFromId, normalizedToId);

        if (_gameSessionService.State.Player.PlayerShipState.GetActiveShip().CurrentFuel < fuelCost)
            return TravelFailReason.NotEnoughFuel;

        return TravelFailReason.None;
    }

    public int GetTravelCost(string fromSystemId, string toSystemId)
    {
        if (GetTravelFailReason(fromSystemId, toSystemId) != TravelFailReason.None)
            return 0;

        return CalculateFuelCost(fromSystemId.Trim(), toSystemId.Trim());
    }

    private int CalculateFuelCost(string fromSystemId, string toSystemId)
    {
        IReadOnlyList<StarSystemConfig> systems = _configService.GetAllStarSystems();

        foreach (StarSystemConfig starSystemConfig in systems)
        {
            if (starSystemConfig.Id != fromSystemId)
                continue;

            foreach (StarSystemLink starSystemLink in starSystemConfig.LinkedSystems)
            {
                if (starSystemLink.LinkedSystem.Id == toSystemId)
                    return starSystemLink.ParsecDistance;
            }
        }

        return DefaultFuelCostPerJump;
    }

    private string GetCurrentSystemId()
    {
        if (_galaxyRuntimeState != null &&
            !string.IsNullOrWhiteSpace(_galaxyRuntimeState.CurrentSystemId))
        {
            return _galaxyRuntimeState.CurrentSystemId;
        }

        return _gameSessionService.State.Player.CurrentSystemId;
    }

    private void SetCurrentSystemId(string systemId)
    {
        _gameSessionService.State.Player.CurrentSystemId = systemId;

        if (_galaxyRuntimeState != null)
            _galaxyRuntimeState.CurrentSystemId = systemId;
    }
}