using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class TravelService2A : CustomService, ITravelService
{
    private const int DefaultFuelCostPerJump = 99;

    private readonly GalaxyGraphModel _galaxyGraph;
    private readonly IGameSessionService _gameSessionService;
    private readonly IConfigService _configService;
    private readonly SimpleEventBus _eventBus;

    private GalaxyRuntimeState _galaxyRuntimeState;
    private readonly IGalaxyDiscoveryService _discoveryService;

    public GalaxyGraphModel GalaxyGraph()
    {
        return _galaxyGraph;
    }

    public TravelService2A()
    {
        _debugEnabled = true;

        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _discoveryService = Bootstrapper.Instance.ServiceRegistry.Get<IGalaxyDiscoveryService>();
        ReinitializeGalaxyState();

        // Оставляем для совместимости со старым кодом, который может вызывать GalaxyGraph().
        // Но доступность перелёта ниже уже проверяется через RouteConfig + RouteRuntimeState.
        _galaxyGraph = GalaxyGraphFactory.CreateFromConfigs(_configService.GetAllStarSystems());
    }

    private void ReinitializeGalaxyState()
    {
        if (_galaxyRuntimeState == null && _gameSessionService.State != null)
            _galaxyRuntimeState = _gameSessionService.State.Galaxy;
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

        StarSystemConfig fromSystemConfig = FindSystemConfig(normalizedFromId);

        if (fromSystemConfig == null)
            return TravelFailReason.CurrentSystemMissing;

        StarSystemConfig toSystemConfig = FindSystemConfig(normalizedToId);

        if (toSystemConfig == null)
            return TravelFailReason.TargetSystemMissing;

        if (normalizedFromId == normalizedToId)
            return TravelFailReason.TargetSystemIsCurrent;

        if (!_discoveryService.IsSystemDiscovered(normalizedToId))
            return TravelFailReason.TargetSystemMissing;

        RouteConfig routeConfig = FindRouteConfig(
            normalizedFromId,
            normalizedToId
        );

        if (routeConfig == null)
            return TravelFailReason.SystemsAreNotNeighbors;

        if (!IsRouteUnlocked(routeConfig))
            return TravelFailReason.SystemsAreNotNeighbors;

        int fuelCost = CalculateFuelCostByRoute(routeConfig);
        // LogCustom("routeConfig = " + routeConfig);
        // LogCustom("fuelCost = " + fuelCost);

        if (_gameSessionService.State.Player.PlayerShipState.GetActiveShip().CurrentFuel < fuelCost)
            return TravelFailReason.NotEnoughFuel;

        return TravelFailReason.None;
    }

    public int GetTravelCost(string fromSystemId, string toSystemId)
    {
        // if (GetTravelFailReason(fromSystemId, toSystemId) != TravelFailReason.None)
        //     return 0;

        string normalizedFromId = fromSystemId.Trim();
        string normalizedToId = toSystemId.Trim();

        RouteConfig routeConfig = FindRouteConfig(
            normalizedFromId,
            normalizedToId
        );

        if (routeConfig == null)
            return 0;

        return CalculateFuelCostByRoute(routeConfig);
    }

    private int CalculateFuelCost(string fromSystemId, string toSystemId)
    {
        RouteConfig routeConfig = FindRouteConfig(
            fromSystemId,
            toSystemId
        );

        if (routeConfig == null)
            return DefaultFuelCostPerJump;

        return CalculateFuelCostByRoute(routeConfig);
    }

    private int CalculateFuelCostByRoute(RouteConfig routeConfig)
    {
        if (routeConfig == null)
            return DefaultFuelCostPerJump;

        if (routeConfig.ParsecDistance <= 0)
            return DefaultFuelCostPerJump;

        return routeConfig.ParsecDistance;
    }

    private StarSystemConfig FindSystemConfig(string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId))
            return null;

        IReadOnlyList<StarSystemConfig> systems = _configService.GetAllStarSystems();

        if (systems == null)
            return null;

        return systems.FirstOrDefault(
            system => system != null && system.Id == systemId
        );
    }

    private RouteConfig FindRouteConfig(string fromSystemId, string toSystemId)
    {
        if (string.IsNullOrWhiteSpace(fromSystemId))
            return null;

        if (string.IsNullOrWhiteSpace(toSystemId))
            return null;

        IReadOnlyList<StarSystemConfig> systems = _configService.GetAllStarSystems();

        if (systems == null)
            return null;

        foreach (StarSystemConfig systemConfig in systems)
        {
            if (systemConfig == null || systemConfig.Routes == null)
                continue;

            foreach (RouteConfig routeConfig in systemConfig.Routes)
            {
                if (routeConfig == null)
                    continue;

                if (IsRouteBetweenSystems(routeConfig, fromSystemId, toSystemId))
                    return routeConfig;
            }
        }

        return null;
    }

    private bool IsRouteBetweenSystems(
        RouteConfig routeConfig,
        string firstSystemId,
        string secondSystemId)
    {
        if (routeConfig == null)
            return false;

        if (routeConfig.FromSystem == null)
            return false;

        if (routeConfig.ToSystem == null)
            return false;

        string routeFromSystemId = routeConfig.FromSystem.Id;
        string routeToSystemId = routeConfig.ToSystem.Id;

        bool direct =
            routeFromSystemId == firstSystemId &&
            routeToSystemId == secondSystemId;

        bool reverse =
            routeFromSystemId == secondSystemId &&
            routeToSystemId == firstSystemId;

        return direct || reverse;
    }

    private bool IsRouteUnlocked(RouteConfig routeConfig)
    {
        if (routeConfig == null)
            return false;

        RouteRuntimeState routeState = FindRouteRuntimeState(routeConfig.Id);

        if (routeState != null)
            return routeState.IsUnlocked;

        // Защита для старых сохранений или временных тестов:
        // если состояния маршрута ещё нет, используем стартовую настройку из конфига.
        return routeConfig.IsLockedAtStart == false;
    }

    private RouteRuntimeState FindRouteRuntimeState(string routeId)
    {
        ReinitializeGalaxyState();

        if (string.IsNullOrWhiteSpace(routeId))
            return null;

        if (_galaxyRuntimeState == null || _galaxyRuntimeState.Routes == null)
            return null;

        return _galaxyRuntimeState.Routes.FirstOrDefault(
            route => route != null && route.RouteId == routeId
        );
    }

    private string GetCurrentSystemId()
    {
        ReinitializeGalaxyState();

        if (_galaxyRuntimeState != null && !string.IsNullOrWhiteSpace(_galaxyRuntimeState.CurrentSystemId))
            return _galaxyRuntimeState.CurrentSystemId;

        return _gameSessionService.State.Player.CurrentSystemId;
    }

    private void SetCurrentSystemId(string systemId)
    {
        ReinitializeGalaxyState();

        _gameSessionService.State.Player.CurrentSystemId = systemId;

        if (_galaxyRuntimeState != null)
            _galaxyRuntimeState.CurrentSystemId = systemId;
    }
}