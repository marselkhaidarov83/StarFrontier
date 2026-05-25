using System;
using System.Collections.Generic;
using UnityEngine;

    public sealed class TravelService : ITravelService
    {
        private const int DefaultFuelCostPerJump = -1;

        private readonly GalaxyGraphModel _galaxyGraph;
        public GalaxyGraphModel GalaxyGraph() { return _galaxyGraph; }

        private readonly IGameSessionService _gameSessionService;
        private readonly IConfigService _configService;
        private readonly SimpleEventBus _eventBus;
        
        private bool _debugEnabled;

        public TravelService()
        {
            _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
            _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
            _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
            _galaxyGraph = GalaxyGraphFactory.CreateFromConfigs(_configService.GetAllStarSystems());
        }

        public bool CanTravel(string fromSystemId, string toSystemId)
        {
            var reason = GetTravelFailReason(fromSystemId, toSystemId);
            if (_debugEnabled)
                Debug.Log("TravelService | TravelFailReason: " + reason);
            
            return reason == TravelFailReason.None;
        }

        public TravelResult TryTravel(string toSystemId)
        {
            var session = _gameSessionService.CurrentSave;
            var fromSystemId = session.PlayerProfile.CurrentSystemId;

            _eventBus.Publish(new TravelStartedEvent(fromSystemId, toSystemId));
            if (_debugEnabled)
                Debug.Log($"[TravelService] EventPublished: TravelStartedEvent");

            var failReason = GetTravelFailReason(fromSystemId, toSystemId);

            if (failReason != TravelFailReason.None)
            {
                var failedResult = TravelResult.Failed(failReason, fromSystemId, toSystemId);

                _eventBus.Publish(new TravelFinishedEvent(
                    fromSystemId: failedResult.FromSystemId,
                    toSystemId: failedResult.ToSystemId,
                    success: false,
                    fuelSpent: 0,
                    failReason: failedResult.FailReason));
                Debug.Log($"[TravelService] EventPublished: TravelFinishedEvent");

                // _eventBus.Publish(new SystemEnteredEvent(failedResult.ToSystemId));

                return failedResult;
            }

            var fuelCost = CalculateFuelCost(fromSystemId, toSystemId);

            if (_debugEnabled)
                Debug.Log($"[TravelService] : Current fuel " + session.PlayerProfile.PlayerShipState.GetActiveShip().CurrentFuel);

            session.PlayerProfile.PlayerShipState.GetActiveShip().CurrentFuel -= fuelCost;
            if (_debugEnabled)
                Debug.Log($"[TravelService] : Current fuel " + session.PlayerProfile.PlayerShipState.GetActiveShip().CurrentFuel);

            session.PlayerProfile.CurrentSystemId = toSystemId.Trim();

            var completedResult = TravelResult.Completed(fromSystemId, toSystemId.Trim(), fuelCost);

            _eventBus.Publish(new TravelFinishedEvent(
                fromSystemId: completedResult.FromSystemId,
                toSystemId: completedResult.ToSystemId,
                success: true,
                fuelSpent: completedResult.FuelSpent,
                failReason: TravelFailReason.None));
            if (_debugEnabled)
                Debug.Log($"[TravelService] EventPublished: TravelFinishedEvent");

            _eventBus.Publish(new SystemEnteredEvent(completedResult.ToSystemId));
            if (_debugEnabled)
                Debug.Log($"[TravelService] EventPublished: SystemEnteredEvent");

            return completedResult;
        }

        public void TryTravelToPlanet(string planetId)
        {
            _gameSessionService.CurrentSave.PlayerProfile.CurrentPlanetId = planetId;
            Debug.Log("[TravelService] TryTravelToPlanet = " + planetId);

            if (_eventBus != null)
                _eventBus.Publish(new PlanetEnteredEvent(planetId));
        }

        public TravelFailReason GetTravelFailReason(string fromSystemId, string toSystemId)
        {
            if (string.IsNullOrWhiteSpace(fromSystemId))
                return TravelFailReason.CurrentSystemMissing;

            if (string.IsNullOrWhiteSpace(toSystemId))
                return TravelFailReason.TargetSystemMissing;

            var normalizedFromId = fromSystemId.Trim();
            var normalizedToId = toSystemId.Trim();

            if (!_galaxyGraph.ContainsSystem(normalizedFromId))
                return TravelFailReason.CurrentSystemMissing;

            if (!_galaxyGraph.ContainsSystem(normalizedToId))
                return TravelFailReason.TargetSystemMissing;

            if (fromSystemId == toSystemId)
                return TravelFailReason.TargetSystemIsCurrent;

            if (!_galaxyGraph.AreNeighbors(normalizedFromId, normalizedToId))
                return TravelFailReason.SystemsAreNotNeighbors;

            var fuelCost = CalculateFuelCost(normalizedFromId, normalizedToId);

            if (_debugEnabled)
                Debug.Log($"[TravelService] CurrentFuel: " + _gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip().CurrentFuel);
            if (_gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip().CurrentFuel < fuelCost)
                return TravelFailReason.NotEnoughFuel;

            return TravelFailReason.None;
        }

        public int GetTravelCost(string fromSystemId, string toSystemId)
        {
            if (string.IsNullOrWhiteSpace(fromSystemId))
                return 0;

            if (string.IsNullOrWhiteSpace(toSystemId))
                return 0;

            var normalizedFromId = fromSystemId.Trim();
            var normalizedToId = toSystemId.Trim();

            if (!_galaxyGraph.ContainsSystem(normalizedFromId))
                return 0;

            if (!_galaxyGraph.ContainsSystem(normalizedToId))
                return 0;

            if (!_galaxyGraph.AreNeighbors(normalizedFromId, normalizedToId))
                return 0;

            return CalculateFuelCost(normalizedFromId, normalizedToId);
        }

        private int CalculateFuelCost(string fromSystemId, string toSystemId)
        {
            IReadOnlyList<StarSystemConfig> list = _configService.GetAllStarSystems();
            foreach (StarSystemConfig starSystemConfig in _configService.GetAllStarSystems())
                if (starSystemConfig.Id == fromSystemId)
                {
                    foreach (StarSystemLink starSystemLink in starSystemConfig.LinkedSystems)
                        if (starSystemLink.LinkedSystem.Id == toSystemId)
                            return starSystemLink.ParsecDistance;
                    break;
                }

            return DefaultFuelCostPerJump;
        }
    }