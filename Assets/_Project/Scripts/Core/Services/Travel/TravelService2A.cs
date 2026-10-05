using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class TravelService2A :
    CustomService,
    ITravelService
{
    private const int DefaultFuelCostPerJump = 99;

    private readonly GalaxyGraphModel _galaxyGraph;

    private readonly IGameSessionService
        _gameSessionService;

    private readonly IConfigService
        _configService;

    private readonly SimpleEventBus
        _eventBus;

    private readonly IGalaxyDiscoveryService
        _discoveryService;

    private readonly IRefuelService
        _refuelService;

    private GalaxyRuntimeState
        _galaxyRuntimeState;

    public TravelService2A()
    {
        _debugEnabled = true;

        _gameSessionService =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<IGameSessionService>();

        _configService =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<IConfigService>();

        _eventBus =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<SimpleEventBus>();

        _discoveryService =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<IGalaxyDiscoveryService>();

        _refuelService =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<IRefuelService>();

        ReinitializeGalaxyState();

        /*
         * Сохраняется для совместимости
         * со старым кодом GalaxyGraph().
         *
         * Основная проверка перелёта выполняется
         * через RouteConfig и RouteRuntimeState.
         */
        _galaxyGraph =
            GalaxyGraphFactory.CreateFromConfigs(
                _configService
                    .GetAllStarSystems());
    }

    public GalaxyGraphModel GalaxyGraph()
    {
        return _galaxyGraph;
    }

    private void ReinitializeGalaxyState()
    {
        if (_galaxyRuntimeState != null)
            return;

        if (_gameSessionService == null)
            return;

        if (_gameSessionService.State == null)
            return;

        _galaxyRuntimeState =
            _gameSessionService
                .State
                .Galaxy;
    }

    public bool CanTravel(
        string fromSystemId,
        string toSystemId)
    {
        return GetTravelFailReason(
                   fromSystemId,
                   toSystemId) ==
               TravelFailReason.None;
    }

    public TravelResult TryTravel(
        string toSystemId)
    {
        string fromSystemId =
            GetCurrentSystemId();

        /*
         * Сохраняем существующий контракт:
         * попытка перелёта публикует Started
         * до получения результата.
         */
        _eventBus.Publish(
            new TravelStartedEvent(
                fromSystemId,
                toSystemId));

        TravelFailReason failReason =
            GetTravelFailReason(
                fromSystemId,
                toSystemId);

        if (failReason !=
            TravelFailReason.None)
        {
            return CreateAndPublishFailedResult(
                failReason,
                fromSystemId,
                toSystemId);
        }

        string normalizedFromId =
            fromSystemId.Trim();

        string normalizedToId =
            toSystemId.Trim();

        /*
         * GetTravelFailReason уже подтвердил,
         * что RouteConfig существует.
         *
         * Тот же метод расчёта используется:
         * - в preview;
         * - в проверке;
         * - в фактическом расходе.
         */
        int fuelCost =
            CalculateFuelCost(
                normalizedFromId,
                normalizedToId);

        /*
         * Повторная атомарная проверка непосредственно
         * перед изменением Fuel.
         *
         * Между GetTravelFailReason и этой строкой
         * State теоретически мог измениться.
         */
        if (!_refuelService.Consume(
                fuelCost,
                out FuelConsumeFailReason
                    consumeReason))
        {
            Debug.LogWarning(
                "[TravelService2A] " +
                "Fuel consumption rejected. " +
                "From = " +
                normalizedFromId +
                " | To = " +
                normalizedToId +
                " | Cost = " +
                fuelCost +
                " | Reason = " +
                consumeReason);

            /*
             * Текущий TravelFailReason уже содержит
             * NotEnoughFuel.
             *
             * Пока отдельная player-facing причина
             * отсутствия Fuel State не вводится,
             * все отказы топливного слоя возвращаются
             * через существующий контракт.
             *
             * Точная техническая причина остаётся
             * в диагностическом логе.
             */
            return CreateAndPublishFailedResult(
                TravelFailReason.NotEnoughFuel,
                normalizedFromId,
                normalizedToId);
        }

        /*
         * После успешного Consume все проверки
         * завершены. Далее не должно быть обычных
         * веток, возвращающих отказ.
         */

        SetCurrentSystemId(
            normalizedToId);

        _discoveryService.VisitSystem(
            normalizedToId);

        TravelResult completedResult =
            TravelResult.Completed(
                normalizedFromId,
                normalizedToId,
                fuelCost);

        /*
         * SystemTravelService слушает это событие
         * и устанавливает позицию входа
         * в новой системе.
         */
        _eventBus.Publish(
            new TravelFinishedEvent(
                fromSystemId:
                    completedResult
                        .FromSystemId,

                toSystemId:
                    completedResult
                        .ToSystemId,

                success: true,

                fuelSpent:
                    completedResult
                        .FuelSpent,

                failReason:
                    TravelFailReason.None));

        _eventBus.Publish(
            new StarSystemEnteredEvent(
                completedResult
                    .ToSystemId));

        _eventBus.Publish(
            new CurrentSystemEnteredEvent(
                normalizedToId));

        /*
         * Сохранение вызывается последним.
         *
         * К этому моменту уже согласованы:
         * - Fuel активного корабля;
         * - Player.CurrentSystemId;
         * - Galaxy.CurrentSystemId;
         * - discovery state;
         * - обработчики TravelFinishedEvent,
         *   включая позицию входа.
         */
        _eventBus.Publish(
            new SaveNeedEvent());

        return completedResult;
    }

    public void TryTravelToPlanet(
        string planetId)
    {
        _gameSessionService
            .State
            .Player
            .CurrentPlanetId =
                planetId;

        _eventBus.Publish(
            new PlanetEnteredEvent(
                planetId));
    }

    public TravelFailReason GetTravelFailReason(
        string fromSystemId,
        string toSystemId)
    {
        if (string.IsNullOrWhiteSpace(
                fromSystemId))
        {
            return TravelFailReason
                .CurrentSystemMissing;
        }

        if (string.IsNullOrWhiteSpace(
                toSystemId))
        {
            return TravelFailReason
                .TargetSystemMissing;
        }

        string normalizedFromId =
            fromSystemId.Trim();

        string normalizedToId =
            toSystemId.Trim();

        StarSystemConfig fromSystemConfig =
            FindSystemConfig(
                normalizedFromId);

        if (fromSystemConfig == null)
        {
            return TravelFailReason
                .CurrentSystemMissing;
        }

        StarSystemConfig toSystemConfig =
            FindSystemConfig(
                normalizedToId);

        if (toSystemConfig == null)
        {
            return TravelFailReason
                .TargetSystemMissing;
        }

        if (normalizedFromId ==
            normalizedToId)
        {
            return TravelFailReason
                .TargetSystemIsCurrent;
        }

        if (!_discoveryService
                .IsSystemDiscovered(
                    normalizedToId))
        {
            return TravelFailReason
                .TargetSystemMissing;
        }

        RouteConfig routeConfig =
            FindRouteConfig(
                normalizedFromId,
                normalizedToId);

        if (routeConfig == null)
        {
            return TravelFailReason
                .SystemsAreNotNeighbors;
        }

        if (!IsRouteUnlocked(
                routeConfig))
        {
            return TravelFailReason
                .SystemsAreNotNeighbors;
        }

        int fuelCost =
            CalculateFuelCostByRoute(
                routeConfig);

        if (!_refuelService.CanConsume(
                fuelCost,
                out FuelConsumeFailReason
                    fuelReason))
        {
            LogCustom(
                "[TravelService2A] " +
                "Travel fuel validation failed. " +
                "From = " +
                normalizedFromId +
                " | To = " +
                normalizedToId +
                " | Cost = " +
                fuelCost +
                " | Reason = " +
                fuelReason);

            return TravelFailReason
                .NotEnoughFuel;
        }

        return TravelFailReason.None;
    }

    public int GetTravelCost(
        string fromSystemId,
        string toSystemId)
    {
        if (string.IsNullOrWhiteSpace(
                fromSystemId))
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(
                toSystemId))
        {
            return 0;
        }

        string normalizedFromId =
            fromSystemId.Trim();

        string normalizedToId =
            toSystemId.Trim();

        RouteConfig routeConfig =
            FindRouteConfig(
                normalizedFromId,
                normalizedToId);

        if (routeConfig == null)
            return 0;

        return CalculateFuelCostByRoute(
            routeConfig);
    }

    private TravelResult
        CreateAndPublishFailedResult(
            TravelFailReason failReason,
            string fromSystemId,
            string toSystemId)
    {
        TravelResult failedResult =
            TravelResult.Failed(
                failReason,
                fromSystemId,
                toSystemId);

        _eventBus.Publish(
            new TravelFinishedEvent(
                fromSystemId:
                    failedResult
                        .FromSystemId,

                toSystemId:
                    failedResult
                        .ToSystemId,

                success: false,

                fuelSpent: 0,

                failReason:
                    failedResult
                        .FailReason));

        return failedResult;
    }

    private int CalculateFuelCost(
        string fromSystemId,
        string toSystemId)
    {
        RouteConfig routeConfig =
            FindRouteConfig(
                fromSystemId,
                toSystemId);

        if (routeConfig == null)
            return DefaultFuelCostPerJump;

        return CalculateFuelCostByRoute(
            routeConfig);
    }

    private int CalculateFuelCostByRoute(
        RouteConfig routeConfig)
    {
        if (routeConfig == null)
            return DefaultFuelCostPerJump;

        if (routeConfig.ParsecDistance <= 0)
            return DefaultFuelCostPerJump;

        return routeConfig.ParsecDistance;
    }

    private StarSystemConfig FindSystemConfig(
        string systemId)
    {
        if (string.IsNullOrWhiteSpace(
                systemId))
        {
            return null;
        }

        IReadOnlyList<StarSystemConfig> systems =
            _configService
                .GetAllStarSystems();

        if (systems == null)
            return null;

        return systems.FirstOrDefault(
            system =>
                system != null &&
                system.Id == systemId);
    }

    private RouteConfig FindRouteConfig(
        string fromSystemId,
        string toSystemId)
    {
        if (string.IsNullOrWhiteSpace(
                fromSystemId))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(
                toSystemId))
        {
            return null;
        }

        IReadOnlyList<StarSystemConfig> systems =
            _configService
                .GetAllStarSystems();

        if (systems == null)
            return null;

        foreach (
            StarSystemConfig systemConfig
            in systems)
        {
            if (systemConfig == null)
                continue;

            if (systemConfig.Routes == null)
                continue;

            foreach (
                RouteConfig routeConfig
                in systemConfig.Routes)
            {
                if (routeConfig == null)
                    continue;

                if (IsRouteBetweenSystems(
                        routeConfig,
                        fromSystemId,
                        toSystemId))
                {
                    return routeConfig;
                }
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

        string routeFromSystemId =
            routeConfig
                .FromSystem
                .Id;

        string routeToSystemId =
            routeConfig
                .ToSystem
                .Id;

        bool direct =
            routeFromSystemId ==
                firstSystemId &&
            routeToSystemId ==
                secondSystemId;

        bool reverse =
            routeFromSystemId ==
                secondSystemId &&
            routeToSystemId ==
                firstSystemId;

        return direct || reverse;
    }

    private bool IsRouteUnlocked(RouteConfig routeConfig)
    {
        if (routeConfig == null)
            return false;

        if (routeConfig.FromSystem == null || routeConfig.ToSystem == null)
            return false;

        IRouteService routeService =
            Bootstrapper.Instance.ServiceRegistry.Get<IRouteService>();

        return routeService.HasUnlockedRoute(
            routeConfig.FromSystem.Id,
            routeConfig.ToSystem.Id
        );
    }

    private RouteRuntimeState
        FindRouteRuntimeState(
            string routeId)
    {
        ReinitializeGalaxyState();

        if (string.IsNullOrWhiteSpace(
                routeId))
        {
            return null;
        }

        if (_galaxyRuntimeState == null)
            return null;

        if (_galaxyRuntimeState.Routes == null)
            return null;

        return _galaxyRuntimeState
            .Routes
            .FirstOrDefault(
                route =>
                    route != null &&
                    route.RouteId ==
                        routeId);
    }

    private string GetCurrentSystemId()
    {
        ReinitializeGalaxyState();

        if (_galaxyRuntimeState != null &&
            !string.IsNullOrWhiteSpace(
                _galaxyRuntimeState
                    .CurrentSystemId))
        {
            return _galaxyRuntimeState
                .CurrentSystemId;
        }

        return _gameSessionService
            .State
            .Player
            .CurrentSystemId;
    }

    private void SetCurrentSystemId(
        string systemId)
    {
        ReinitializeGalaxyState();

        _gameSessionService
            .State
            .Player
            .CurrentSystemId =
                systemId;

        if (_galaxyRuntimeState != null)
        {
            _galaxyRuntimeState
                .CurrentSystemId =
                    systemId;
        }
    }
}