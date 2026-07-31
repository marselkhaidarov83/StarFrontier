using System.Collections.Generic;
using UnityEngine;

public sealed class SystemTravelService : CustomService, ISystemTravelService
{
    private const float SunAvoidanceSafetyMargin = 80f;
    private const int SunAvoidanceArcSegments = 18;

    private readonly List<Vector3> _travelPathBuffer = new List<Vector3>(32);
    private readonly List<Vector3> _routePreviewPathBuffer = new List<Vector3>(64);
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

    private void ApplyShipPositionAfterSystemJump(
    Vector3 position,
    StarSystemConfig destinationSystemConfig)
    {
        /*
         * Рассчитываем направление:
         *
         * точка входа корабля -> солнце новой системы.
         */
        Vector2 facingDirection =
            GetArrivalFacingDirection(
                position,
                destinationSystemConfig);

        State.SetCurrentPosition(
            position);

        State.StartPosition =
            position;

        State.DestinationPosition =
            position;

        State.TravelDistance =
            0f;

        State.TravelProgress01 =
            1f;

        State.Status =
            SystemTravelStatus.Idle;

        State.Destination =
            SystemTravelDestination.None();

        PlayerState playerState =
            null;

        if (_gameSessionService != null &&
            _gameSessionService.State != null)
        {
            playerState =
                _gameSessionService.State.Player;
        }

        /*
         * Записываем позицию и направление
         * в состояние игрока.
         *
         * Это необходимо для последующего
         * сохранения и восстановления игры.
         */
        if (playerState != null)
        {
            playerState.SystemMapShipPosition =
                position;

            playerState.SystemMapShipDirection =
                new Vector3(
                    facingDirection.x,
                    facingDirection.y,
                    0f);
        }

        if (_shipMovementService != null)
        {
            _shipMovementService.SetPosition(
                new Vector2(
                    position.x,
                    position.y));

            /*
             * Сначала полностью прекращаем
             * предыдущее движение.
             */
            _shipMovementService
                .StopImmediately();

            /*
             * Затем задаём итоговое направление.
             */
            _shipMovementService
                .SetFacingDirection(
                    facingDirection);

            /*
             * Синхронизируем состояние движения
             * с PlayerState.
             */
            if (playerState != null)
            {
                _shipMovementService
                    .WriteToPlayerState(
                        playerState);
            }
        }
    }

    private Vector2 GetArrivalFacingDirection(
    Vector3 shipPosition,
    StarSystemConfig destinationSystemConfig)
    {
        /*
         * Запасное направление используется,
         * если конфигурация новой системы
         * или её солнца отсутствует.
         */
        if (destinationSystemConfig == null ||
            destinationSystemConfig.Sun == null)
        {
            return Vector2.up;
        }

        Vector2 shipPosition2D =
            new Vector2(
                shipPosition.x,
                shipPosition.y);

        Vector2 sunPosition =
            destinationSystemConfig
                .Sun
                .LocalOffset;

        Vector2 directionToSun =
            sunPosition -
            shipPosition2D;

        bool hasInvalidValue =
            float.IsNaN(directionToSun.x) ||
            float.IsNaN(directionToSun.y) ||
            float.IsInfinity(directionToSun.x) ||
            float.IsInfinity(directionToSun.y);

        if (hasInvalidValue ||
            directionToSun.sqrMagnitude <=
                0.0001f)
        {
            return Vector2.up;
        }

        return directionToSun.normalized;
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

        Vector3 entryPoint =
    routeConfig.GetEntryPoint(
        evt.ToSystemId);

        /*
         * Получаем конфигурацию именно новой системы.
         * Она нужна для определения положения её солнца.
         */
        StarSystemConfig destinationSystemConfig =
            routeConfig.GetOtherSystem(
                evt.FromSystemId);

        SetCurrentSystem(
            evt.ToSystemId);

        ApplyShipPositionAfterSystemJump(
            entryPoint,
            destinationSystemConfig);

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

        float movementDistance = GetCurrentShipTravelSpeed() * deltaTime;

        bool destinationReached;
        Vector3 nextPosition = CalculateNextTravelPositionByPath(
            State.GetCurrentPosition(),
            State.DestinationPosition,
            movementDistance,
            out destinationReached
        );

        State.SetCurrentPosition(nextPosition);

        if (destinationReached ||
            Vector3.Distance(State.GetCurrentPosition(), State.DestinationPosition) <= ArrivalDistanceThreshold)
        {
            CompleteTravel();
            return;
        }

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

    private Vector3 CalculateNextTravelPosition(
    Vector3 currentPosition,
    Vector3 destinationPosition,
    float movementDistance,
    out bool destinationReached
)
    {
        Vector3 direction = destinationPosition - currentPosition;
        float distanceToDestination = direction.magnitude;

        if (distanceToDestination <= ArrivalDistanceThreshold)
        {
            destinationReached = true;
            return destinationPosition;
        }

        if (movementDistance >= distanceToDestination)
        {
            destinationReached = true;
            return destinationPosition;
        }

        destinationReached = false;
        return currentPosition + direction.normalized * movementDistance;
    }

    public TravelRoutePreview2A GetCurrentRoutePreview2A(
    float smallDotSpacing,
    int maxBigDots,
    int maxSmallDots,
    float secondsPerTick)
    {
        TravelRoutePreview2A preview =
            new TravelRoutePreview2A();

        if (State == null)
            return preview;

        if (!State.HasDestination)
            return preview;

        if (
            State.Status !=
                SystemTravelStatus.DestinationSelected &&
            State.Status !=
                SystemTravelStatus.Flying)
        {
            return preview;
        }

        float safeSmallDotSpacing =
            Mathf.Max(
                0.01f,
                smallDotSpacing);

        int safeMaxBigDots =
            Mathf.Max(
                1,
                maxBigDots);

        int safeMaxSmallDots =
            Mathf.Max(
                0,
                maxSmallDots);

        float safeSecondsPerTick =
            Mathf.Max(
                0.01f,
                secondsPerTick);

        float speed =
            Mathf.Max(
                0.01f,
                GetCurrentShipTravelSpeed());

        float distancePerTick =
            speed *
            safeSecondsPerTick;

        if (
            State.Destination != null &&
            State.Destination.Type ==
                TravelDestinationType.Planet)
        {
            BuildLivePlanetRoutePreview2A(
                preview,
                safeSmallDotSpacing,
                safeMaxBigDots,
                safeMaxSmallDots,
                distancePerTick
            );
        }
        else
        {
            BuildStaticRoutePreview2A(
                preview,
                safeSmallDotSpacing,
                safeMaxBigDots,
                safeMaxSmallDots,
                distancePerTick
            );
        }

        return preview;
    }

    private void BuildStaticRoutePreview2A(
     TravelRoutePreview2A preview,
     float smallDotSpacing,
     int maxBigDots,
     int maxSmallDots,
     float distancePerTick)
    {
        BuildRoutePreview2A(
            preview,
            smallDotSpacing,
            maxBigDots,
            maxSmallDots,
            distancePerTick
        );
    }

    private void BuildLivePlanetRoutePreview2A(
        TravelRoutePreview2A preview,
        float smallDotSpacing,
        int maxBigDots,
        int maxSmallDots,
        float distancePerTick)
    {
        BuildRoutePreview2A(
            preview,
            smallDotSpacing,
            maxBigDots,
            maxSmallDots,
            distancePerTick
        );
    }

    private void BuildRoutePreview2A(
        TravelRoutePreview2A preview,
        float smallDotSpacing,
        int maxBigDots,
        int maxSmallDots,
        float distancePerTick)
    {
        Vector3 routeStart =
            State.StartPosition;

        Vector3 currentPosition =
            State.GetCurrentPosition();

        Vector3 destinationPosition =
            GetCurrentDestinationPosition();

        BuildCurrentTravelPath(
            routeStart,
            destinationPosition,
            _routePreviewPathBuffer
        );

        float totalPathLength =
            GetPathLength(
                _routePreviewPathBuffer);

        if (
            totalPathLength <=
            ArrivalDistanceThreshold)
        {
            return;
        }

        float passedDistance =
            GetClosestDistanceOnPath(
                _routePreviewPathBuffer,
                currentPosition
            );

        passedDistance =
            Mathf.Clamp(
                passedDistance,
                0f,
                totalPathLength);

        for (
            int tickIndex = 1;
            tickIndex <= maxBigDots;
            tickIndex++)
        {
            /*
             * Начало участка текущего временного тика.
             * Координата всегда считается от начала
             * всего маршрута, а не от корабля.
             */
            float intervalStartDistance =
                Mathf.Min(
                    (tickIndex - 1) *
                        distancePerTick,
                    totalPathLength
                );

            float distanceAtTick =
                Mathf.Min(
                    tickIndex *
                        distancePerTick,
                    totalPathLength
                );

            /*
             * Большая точка уже пройдена.
             * Весь соответствующий участок маршрута
             * больше не отображается.
             */
            if (
                distanceAtTick <=
                passedDistance + 0.001f)
            {
                continue;
            }

            AddSmallRoutePreviewDots2A(
                preview,
                _routePreviewPathBuffer,
                intervalStartDistance,
                distanceAtTick,
                passedDistance,
                tickIndex,
                smallDotSpacing,
                maxSmallDots
            );

            Vector3 tickPosition =
                GetPointOnPathAtDistance(
                    _routePreviewPathBuffer,
                    distanceAtTick
                );

            preview.AddBigDot(
                tickPosition,
                tickIndex);

            if (
                distanceAtTick >=
                totalPathLength)
            {
                break;
            }
        }
    }

    private void AddSmallRoutePreviewDots2A(
    TravelRoutePreview2A preview,
    List<Vector3> path,
    float intervalStartDistance,
    float intervalEndDistance,
    float passedDistance,
    int tickIndex,
    float smallDotSpacing,
    int maxSmallDots)
    {
        if (preview == null)
            return;

        if (path == null || path.Count == 0)
            return;

        if (preview.SmallDotCount >= maxSmallDots)
            return;

        float safeSpacing =
            Mathf.Max(
                0.01f,
                smallDotSpacing);

        float safeStartDistance =
            Mathf.Max(
                0f,
                intervalStartDistance);

        float safeEndDistance =
            Mathf.Max(
                safeStartDistance,
                intervalEndDistance);

        /*
         * Каждая точка получает постоянную координату
         * вдоль полного маршрута:
         *
         * начало участка + один шаг,
         * начало участка + два шага и так далее.
         */
        for (
            float dotDistance =
                safeStartDistance +
                safeSpacing;

            dotDistance <
                safeEndDistance -
                0.001f;

            dotDistance +=
                safeSpacing)
        {
            if (
                preview.SmallDotCount >=
                maxSmallDots)
            {
                return;
            }

            /*
             * Корабль уже прошёл эту точку.
             * Не переносим её вперёд, а исключаем.
             */
            if (
                dotDistance <=
                passedDistance +
                0.001f)
            {
                continue;
            }

            Vector3 position =
                GetPointOnPathAtDistance(
                    path,
                    dotDistance
                );

            preview.AddSmallDot(
                position,
                tickIndex);
        }
    }

    private void BuildCurrentTravelPath(
    Vector3 from,
    Vector3 to,
    List<Vector3> path
)
    {
        if (path == null)
            return;

        StarSystemConfig currentSystem = _configService.GetCurrentSystemConfig();

        if (currentSystem == null || currentSystem.Sun == null)
        {
            path.Clear();
            path.Add(from);
            path.Add(to);
            return;
        }

        SunConfig sun = currentSystem.Sun;

        Vector3 sunCenter = new Vector3(
            sun.LocalOffset.x,
            sun.LocalOffset.y,
            from.z
        );

        float sunRadius = Mathf.Max(0f, sun.VisualSize * 0.5f);
        float avoidanceRadius = sunRadius + SunAvoidanceSafetyMargin;

        SystemTravelSunAvoidancePath2A.BuildPath(
            path,
            from,
            to,
            sunCenter,
            avoidanceRadius,
            SunAvoidanceArcSegments
        );
    }

    private Vector3 CalculateNextTravelPositionByPath(
    Vector3 currentPosition,
    Vector3 destinationPosition,
    float movementDistance,
    out bool destinationReached
)
    {
        BuildCurrentTravelPath(
            currentPosition,
            destinationPosition,
            _travelPathBuffer
        );

        destinationReached = false;

        if (_travelPathBuffer == null || _travelPathBuffer.Count <= 1)
            return destinationPosition;

        Vector3 position = currentPosition;
        float remainingMovement = movementDistance;

        for (int i = 1; i < _travelPathBuffer.Count; i++)
        {
            Vector3 waypoint = _travelPathBuffer[i];
            Vector3 segment = waypoint - position;
            float segmentDistance = segment.magnitude;

            if (segmentDistance <= ArrivalDistanceThreshold)
            {
                position = waypoint;
                continue;
            }

            if (remainingMovement >= segmentDistance)
            {
                position = waypoint;
                remainingMovement -= segmentDistance;
                continue;
            }

            destinationReached = false;
            return position + segment.normalized * remainingMovement;
        }

        destinationReached = true;
        return destinationPosition;
    }

    private float GetPathLength(List<Vector3> path)
    {
        if (path == null || path.Count <= 1)
            return 0f;

        float length = 0f;

        for (int i = 1; i < path.Count; i++)
            length += Vector3.Distance(path[i - 1], path[i]);

        return length;
    }

    private Vector3 GetPointOnPathAtDistance(List<Vector3> path, float distance)
    {
        if (path == null || path.Count == 0)
            return Vector3.zero;

        if (path.Count == 1)
            return path[0];

        float remainingDistance = Mathf.Max(0f, distance);

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 from = path[i - 1];
            Vector3 to = path[i];

            float segmentDistance = Vector3.Distance(from, to);

            if (segmentDistance <= ArrivalDistanceThreshold)
                continue;

            if (remainingDistance <= segmentDistance)
            {
                float t = remainingDistance / segmentDistance;
                return Vector3.Lerp(from, to, t);
            }

            remainingDistance -= segmentDistance;
        }

        return path[path.Count - 1];
    }

    private float GetClosestDistanceOnPath(
        List<Vector3> path,
        Vector3 point
    )
    {
        if (path == null || path.Count <= 1)
            return 0f;

        float bestDistanceToPathSqr = float.MaxValue;
        float bestDistanceAlongPath = 0f;
        float accumulatedDistance = 0f;

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 segmentStart = path[i - 1];
            Vector3 segmentEnd = path[i];

            Vector3 segment = segmentEnd - segmentStart;
            float segmentLength = segment.magnitude;

            if (segmentLength <= ArrivalDistanceThreshold)
                continue;

            float t = Vector3.Dot(point - segmentStart, segment) / (segmentLength * segmentLength);
            t = Mathf.Clamp01(t);

            Vector3 closestPoint = segmentStart + segment * t;
            float distanceToPathSqr = (point - closestPoint).sqrMagnitude;

            if (distanceToPathSqr < bestDistanceToPathSqr)
            {
                bestDistanceToPathSqr = distanceToPathSqr;
                bestDistanceAlongPath = accumulatedDistance + segmentLength * t;
            }

            accumulatedDistance += segmentLength;
        }

        return bestDistanceAlongPath;
    }
}