using System.Collections.Generic;
using UnityEngine;

public sealed class SystemTravelService : CustomService, ISystemTravelService
{
    private const float ArrivalDistanceThreshold = 3f;

    private readonly SimpleEventBus _eventBus;
    private readonly IGameSessionService _gameSessionService;
    private readonly IOrbitalMotionService _orbitalMotionService;
    private readonly IHangarService _hangarService;
    private readonly IConfigService _configService;
    private readonly IShipMovementService _shipMovementService;

    public SystemTravelState State { get; }

    public SystemTravelService()
    {
        _debugEnabled = true;
        // _debugStop = true;
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _orbitalMotionService = Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();
        _hangarService = Bootstrapper.Instance.ServiceRegistry.Get<IHangarService>();
        _shipMovementService = Bootstrapper.Instance.ServiceRegistry.Get<IShipMovementService>();

        State = new SystemTravelState();
        _eventBus.Subscribe<TravelFinishedEvent>(OnTravelFinished);
    }

    private void ApplyShipPositionAfterSystemJump(Vector3 position)
    {
        State.SetCurrentPosition(position);

        State.StartPosition = position;
        State.DestinationPosition = position;
        State.TravelDistance = 0f;
        State.TravelProgress01 = 1f;
        State.Status = SystemTravelStatus.Idle;
        State.Destination = SystemTravelDestination.None();

        _gameSessionService.State.Player.SystemMapShipPosition = position;

        if (_shipMovementService != null)
        {
            _shipMovementService.SetPosition(new Vector2(position.x, position.y));
            _shipMovementService.StopImmediately();
        }
    }

    private void OnTravelFinished(TravelFinishedEvent evt)
    {
        if (!evt.Success)
            return;

        if (evt.FailReason != TravelFailReason.None)
            return;

        if (string.IsNullOrWhiteSpace(evt.FromSystemId))
            return;

        if (string.IsNullOrWhiteSpace(evt.ToSystemId))
            return;

        if (evt.FromSystemId == evt.ToSystemId)
            return;

        RouteConfig routeConfig = FindRouteConfig(evt.FromSystemId, evt.ToSystemId);

        if (routeConfig == null)
        {
            Debug.LogWarning(
                "[SystemTravelService] Cannot set arrival position. RouteConfig not found. " +
                "From = " + evt.FromSystemId +
                " | To = " + evt.ToSystemId
            );

            return;
        }

        LogCustom("exitPoint = " + routeConfig.GetExitPoint(evt.FromSystemId));
        LogCustom("entryPoint = " + routeConfig.GetEntryPoint(evt.ToSystemId));

        Vector3 entryPoint = routeConfig.GetEntryPoint(evt.ToSystemId);

        SetCurrentSystem(evt.ToSystemId);
        ApplyShipPositionAfterSystemJump(entryPoint);

        State.StartPosition = entryPoint;
        State.DestinationPosition = entryPoint;
        State.TravelDistance = 0f;
        State.TravelProgress01 = 1f;
        State.Status = SystemTravelStatus.Idle;
        State.Destination = SystemTravelDestination.None();

        _gameSessionService.State.Player.SystemMapShipPosition = entryPoint;

        if (IsDebug())
        {
            Debug.Log(
                "[SystemTravelService] Ship arrival position set. " +
                "From = " + evt.FromSystemId +
                " | To = " + evt.ToSystemId +
                " | EntryPoint = " + entryPoint
            );
        }
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
            if (systemConfig == null)
                continue;

            if (systemConfig.Routes == null)
                continue;

            foreach (RouteConfig routeConfig in systemConfig.Routes)
            {
                if (routeConfig == null)
                    continue;

                if (routeConfig.ConnectsSystems(fromSystemId, toSystemId))
                    return routeConfig;
            }
        }

        return null;
    }

    public void SetCurrentSystem(string systemId)
    {
        State.CurrentSystemId = systemId;
        LogCustom("State = " + State);
    }

    public void SetCurrentPlanet(string planetId, Vector3 planetPosition)
    {
        State.CurrentPlanetId = planetId;
        State.SetCurrentPosition(planetPosition);
        State.StartPosition = planetPosition;
        State.DestinationPosition = planetPosition;
        State.Status = SystemTravelStatus.Idle;
        State.Destination = SystemTravelDestination.None();
        State.TravelProgress01 = 0f;
        LogCustom("State = " + State);
    }

    public void SetCurrentPosition(Vector3 position)
    {
        State.SetCurrentPosition(position);
    }

    public void SetPlanetDestination(PlanetConfig planetData)
    {
        if (planetData == null)
        {
            Debug.LogWarning("[SystemTravelService] Cannot set planet destination: planetData is null.");
            return;
        }

        State.Destination = SystemTravelDestination.Planet(planetData);
        State.DestinationPosition = GetCurrentDestinationPosition();
        State.Status = SystemTravelStatus.DestinationSelected;
        State.TravelProgress01 = 0f;

        _eventBus.Publish(new DestinationSelectedEvent(
            TravelDestinationType.Planet,
            State.DestinationPosition,
            planetData.Id,
            string.Empty
        ));

        LogCustom($"Planet destination selected: {planetData.Id}");
        LogCustom("State = " + State);

        StartTravelAutomaticallyIfPossible();
    }

    public void SetMapPointDestination(Vector3 mapPosition)
    {
        State.Destination = SystemTravelDestination.MapPoint(mapPosition);
        State.DestinationPosition = mapPosition;
        State.Status = SystemTravelStatus.DestinationSelected;
        State.TravelProgress01 = 0f;

        _eventBus.Publish(new DestinationSelectedEvent(
            TravelDestinationType.MapPoint,
            mapPosition,
            string.Empty,
            string.Empty
        ));

        LogCustom($"Map point destination selected: {mapPosition}");
        LogCustom("State = " + State);

        StartTravelAutomaticallyIfPossible();
    }

    public void SetSystemExitDestination(StarSystemLink link)
    {
        if (link == null)
        {
            Debug.LogWarning("[SystemTravelService] Cannot set system exit destination: link is null.");
            return;
        }

        if (link.LinkedSystem == null)
        {
            Debug.LogWarning("[SystemTravelService] Cannot set system exit destination: linked system is null.");
            return;
        }

        State.Destination = SystemTravelDestination.SystemExit(link);
        State.DestinationPosition = link.ExitPoint;
        State.Status = SystemTravelStatus.DestinationSelected;
        State.TravelProgress01 = 0f;

        _eventBus.Publish(new DestinationSelectedEvent(
            TravelDestinationType.SystemExit,
            link.ExitPoint,
            string.Empty,
            link.LinkedSystem.Id
        ));

        LogCustom($"System exit destination selected. Target system: {link.LinkedSystem.Id}");
        LogCustom("State = " + State);

        StartTravelAutomaticallyIfPossible();
    }

    public void SetSystemExitDestination(RouteExitMapChangedEvent evt)
    {
        if (evt == null)
        {
            Debug.LogError("[SystemTravelService] RouteExitMapChangedEvent is null");
            return;
        }

        State.Destination = SystemTravelDestination.SystemExit(evt);
        State.DestinationPosition = evt.ExitPoint;
        State.Status = SystemTravelStatus.DestinationSelected;
        State.TravelProgress01 = 0f;

        _eventBus.Publish(new DestinationSelectedEvent(
            TravelDestinationType.SystemExit,
            evt.ExitPoint,
            string.Empty,
            evt.ToSystemId
        ));

        if (IsDebug())
        {
            Debug.Log(
                "[SystemTravelService] Route exit destination set. " +
                "Route = " + (evt.RouteConfig != null ? evt.RouteConfig.Id : "null") +
                " | From = " + evt.FromSystemId +
                " | To = " + evt.ToSystemId +
                " | ExitPoint = " + evt.ExitPoint +
                " | EntryPoint = " + evt.EntryPoint
            );
        }

        LogCustom("State = " + State);

        StartTravelAutomaticallyIfPossible();
    }

    public void StartTravel()
    {
        if (!State.HasDestination)
        {
            Debug.LogWarning("[SystemTravelService] Cannot start travel: no destination selected.");
            return;
        }

        if (State.Status == SystemTravelStatus.Flying)
        {
            Debug.LogWarning("[SystemTravelService] Cannot start travel: already flying.");
            return;
        }

        State.StartPosition = State.GetCurrentPosition();
        State.DestinationPosition = GetCurrentDestinationPosition();
        State.TravelDistance = Vector3.Distance(State.StartPosition, State.DestinationPosition);
        State.TravelProgress01 = 0f;
        State.Status = SystemTravelStatus.Flying;

        _eventBus.Publish(new SystemTravelStartedEvent(
            State.Destination.Type,
            State.StartPosition,
            State.DestinationPosition
        ));

        LogCustom("Travel started.");
        LogCustom("State = " + State);
    }

    public void CancelTravel()
    {
        if (State.Status != SystemTravelStatus.Flying &&
            State.Status != SystemTravelStatus.DestinationSelected)
        {
            return;
        }

        State.Status = SystemTravelStatus.Cancelled;
        State.Destination = SystemTravelDestination.None();
        State.DestinationPosition = State.GetCurrentPosition();
        State.StartPosition = State.GetCurrentPosition();
        State.TravelDistance = 0f;
        State.TravelProgress01 = 0f;

        if (_gameSessionService != null &&
            _gameSessionService.State != null &&
            _gameSessionService.State.Player != null)
        {
            _gameSessionService.State.Player.SystemMapShipPosition = State.GetCurrentPosition();
        }

        _eventBus.Publish(new SystemTravelCancelledEvent());

        LogCustom("Travel cancelled.");
        LogCustom("State = " + State);
    }

    private float GetCurrentShipTravelSpeed()
    {
        var stats = _hangarService.GetActiveShipStats();

        if (stats == null || stats.Speed <= 0)
            return 1f;

        return stats.Speed;
    }

    public void Tick(float deltaTime, int quantTick)
    {
        if (State.Status != SystemTravelStatus.Flying)
            return;

        State.DestinationPosition = GetCurrentDestinationPosition();

        Vector3 direction = State.DestinationPosition - State.GetCurrentPosition();
        float distanceToDestination = direction.magnitude;

        // if (IsDebug())
        // {
        //     Debug.Log("[SystemTravelService] TickTravel.DestinationPosition = " + State.DestinationPosition);
        //     Debug.Log("[SystemTravelService] TickTravel.CurrentPosition = " + State.GetCurrentPosition());
        // }

        if (distanceToDestination <= ArrivalDistanceThreshold)
        {
            CompleteTravel();
            return;
        }

        Vector3 movementDirection = direction.normalized;
        // float movementDistance = State.TravelSpeedUnitsPerSecond * deltaTime;
        float movementDistance = GetCurrentShipTravelSpeed() * deltaTime;

        if (movementDistance >= distanceToDestination)
        {
            State.SetCurrentPosition(State.DestinationPosition);
            CompleteTravel();
            return;
        }

        State.SetCurrentPosition(State.GetCurrentPosition() + movementDirection * movementDistance);

        float remainingDistance = Vector3.Distance(State.GetCurrentPosition(), State.DestinationPosition);

        if (State.TravelDistance > 0f)
        {
            State.TravelProgress01 = Mathf.Clamp01(
                1f - remainingDistance / State.TravelDistance
            );
        }
        else
        {
            State.TravelProgress01 = 1f;
        }

        // LogCustom("State.TravelProgress01 = " + State.TravelProgress01);
        _gameSessionService.State.Player.SystemMapShipPosition = State.GetCurrentPosition();
        _eventBus.Publish(new SystemTravelProgressChangedEvent(
            State.GetCurrentPosition(),
            State.DestinationPosition,
            State.TravelProgress01
        ));
    }

    private void StartTravelAutomaticallyIfPossible()
    {
        if (!State.HasDestination)
            return;

        if (State.Status == SystemTravelStatus.Flying)
            return;

        StartTravel();
    }

    public void CompleteTravel()
    {
        State.SetCurrentPosition(State.DestinationPosition);
        State.Status = SystemTravelStatus.Arrived;
        State.TravelProgress01 = 1f;

        string planetId = State.Destination.PlanetId;
        string targetSystemId = State.Destination.TargetSystemId;
        StarSystemConfig targetSystemConfig = State.Destination.TargetSystemConfig;
        StarSystemLink systemLink = State.Destination.SystemLink;
        TravelDestinationType destinationType = State.Destination.Type;

        if (destinationType == TravelDestinationType.Planet)
            State.CurrentPlanetId = planetId;

        _eventBus.Publish(new SystemTravelCompletedEvent(
            destinationType,
            State.GetCurrentPosition(),
            planetId,
            targetSystemId,
            targetSystemConfig,
            systemLink
        ));

        LogCustom($"Travel completed. Type: {destinationType}");

        State.Destination = SystemTravelDestination.None();
        State.Status = SystemTravelStatus.Idle;

        _gameSessionService.State.Player.SystemMapShipPosition = State.GetCurrentPosition();
        if (destinationType == TravelDestinationType.Planet ||
            destinationType == TravelDestinationType.SystemExit)
        {
            _eventBus.Publish(new SaveNeedEvent());
            // _saveService.Save();
        }
        LogCustom("State = " + State);
    }

    public Vector3 GetCurrentDestinationPosition()
    {
        if (State.Destination == null)
            return State.GetCurrentPosition();

        switch (State.Destination.Type)
        {
            case TravelDestinationType.Planet:
                if (State.Destination.PlanetData == null)
                    return State.GetCurrentPosition();

                return _orbitalMotionService.GetPlanetCurrentPosition(
                    State.Destination.PlanetData.PlanetOrbit);
            // return _orbitalMotionService.GetPlanetPosition(
            //     State.Destination.PlanetData.PlanetOrbit,
            //     _gameTimeService.SimulationTimeSeconds
            // );
            // return new Vector3(0, 0, 0);

            case TravelDestinationType.MapPoint:
                return State.Destination.FixedMapPosition;

            case TravelDestinationType.SystemExit:
                return State.Destination.FixedMapPosition;

            default:
                return State.GetCurrentPosition();
        }
    }
}