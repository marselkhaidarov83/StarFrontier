using System.Collections.Generic;
using UnityEngine;

public sealed class SystemTravelService : CustomService, ISystemTravelService
{
    private const float SunAvoidanceSafetyMargin = 80f;
    private const float SunCollisionSafetyMargin = 2f;
    private const int SunAvoidanceArcSegments = 18;
    private const float SunAvoidanceTurnRouteReserveMultiplier = 1.5f;
    private const int RoutePlanMaxSteps = 8192;
    private const float RouteSegmentEpsilon = 0.0001f;

    private readonly struct RouteAdjustment
    {
        public RouteAdjustment(
            float turnFactor,
            float speedFactor)
        {
            TurnFactor = Mathf.Clamp01(turnFactor);
            SpeedFactor = Mathf.Clamp01(speedFactor);
        }

        public float TurnFactor { get; }
        public float SpeedFactor { get; }
    }

    private readonly struct SunAvoidanceObstacle
    {
        public SunAvoidanceObstacle(
            bool hasObstacle,
            Vector3 center,
            float radius,
            float blockingRadius)
        {
            HasObstacle = hasObstacle;
            Center = center;
            Radius = radius;
            BlockingRadius = blockingRadius;
        }

        public bool HasObstacle { get; }
        public Vector3 Center { get; }
        public float Radius { get; }
        public float BlockingRadius { get; }
    }

    private enum RouteDestinationCase
    {
        Unknown,
        ForbiddenDestination,
        MovingTarget,
        NearSunStartAwayForward,
        NearSunStartAwaySide,
        NearSunStartAwayBehind,
        NearSunStartTangentForward,
        NearSunStartTangentSideAway,
        NearSunStartTangentSideToward,
        NearSunStartTangentBehind,
        NearSunStartTowardForward,
        NearSunStartTowardSide,
        NearSunStartTowardBehind,
        NearSunStartExit,
        NearSunStart,
        NearSunDestination,
        SunBlocked,
        NearForward,
        NearSide,
        NearBehind,
        FarForward,
        FarSide,
        FarBehind
    }

    private readonly struct RouteDestinationClassification
    {
        public RouteDestinationClassification(
            RouteDestinationCase destinationCase,
            float directDistance,
            float bearingAngleDegrees,
            float nearDistanceThreshold,
            bool isNear,
            bool requiresSunAvoidance,
            bool startInsideSunSafety,
            bool targetInsideSunSafety,
            bool targetInsideSunBody,
            float startDistanceFromSun,
            float targetDistanceFromSun,
            SunAvoidanceObstacle obstacle)
        {
            DestinationCase = destinationCase;
            DirectDistance = directDistance;
            BearingAngleDegrees = bearingAngleDegrees;
            NearDistanceThreshold = nearDistanceThreshold;
            IsNear = isNear;
            RequiresSunAvoidance = requiresSunAvoidance;
            StartInsideSunSafety = startInsideSunSafety;
            TargetInsideSunSafety = targetInsideSunSafety;
            TargetInsideSunBody = targetInsideSunBody;
            StartDistanceFromSun = startDistanceFromSun;
            TargetDistanceFromSun = targetDistanceFromSun;
            Obstacle = obstacle;
        }

        public RouteDestinationCase DestinationCase { get; }
        public float DirectDistance { get; }
        public float BearingAngleDegrees { get; }
        public float NearDistanceThreshold { get; }
        public bool IsNear { get; }
        public bool RequiresSunAvoidance { get; }
        public bool StartInsideSunSafety { get; }
        public bool TargetInsideSunSafety { get; }
        public bool TargetInsideSunBody { get; }
        public float StartDistanceFromSun { get; }
        public float TargetDistanceFromSun { get; }
        public SunAvoidanceObstacle Obstacle { get; }
    }

    private readonly struct SunStartTangentEscapeOption
    {
        public SunStartTangentEscapeOption(
            Vector2 direction,
            float distance,
            float score)
        {
            Direction = direction;
            Distance = distance;
            Score = score;
        }

        public Vector2 Direction { get; }
        public float Distance { get; }
        public float Score { get; }
    }

    private readonly List<Vector3> _directTravelPathBuffer = new List<Vector3>(2);
    private readonly List<Vector3> _travelPathBuffer = new List<Vector3>(32);
    private readonly List<Vector3> _routePreviewPathBuffer = new List<Vector3>(64);
    private readonly List<Vector3> _activeTravelRoutePath = new List<Vector3>(256);
    private readonly List<Vector3> _routeProbePathBuffer = new List<Vector3>(256);
    private readonly List<Vector3> _routeSegmentPathBuffer = new List<Vector3>(128);
    private readonly List<Vector3> _routeSegmentWaypointsBuffer = new List<Vector3>(3);
    private const float ArrivalDistanceThreshold = 3f;

    private readonly SimpleEventBus _eventBus;
    private readonly IGameSessionService _gameSessionService;
    private readonly IOrbitalMotionService _orbitalMotionService;
    private readonly IHangarService _hangarService;
    private readonly IConfigService _configService;
    private readonly IShipMovementService _shipMovementService;
    private readonly IShipStatsService _shipStatsService;
    private readonly ITargetService2A _targetService;
    private ISystemNpcRuntimeService _npcRuntimeService;

    private Vector2 _travelFacingDirection = Vector2.up;
    private Vector2 _routePreviewStartFacingDirection = Vector2.up;
    private float _routeTurnAdjustmentFactor = 1f;
    private float _routeSpeedAdjustmentFactor = 1f;
    private bool _useRouteInitialSpeedLimit;
    private float _routeInitialSpeedLimitFactor = 1f;
    private float _routeInitialSpeedLimitDistance;
    private float _activeRouteDistanceTravelled;
    private int _lastNpcDestinationRefreshTick = -1;
    private int _mapPointRouteTraceFrameCount;
    private int _mapPointRouteBuildId;
    private bool _isMapPointRouteBuildTraceEnabled;
    private RouteDestinationCase _activeRouteDestinationCase =
        RouteDestinationCase.Unknown;

    public SystemTravelState State { get; }
    public float CurrentEffectiveTravelSpeed => GetCurrentEffectiveTravelSpeed();

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
        _shipStatsService = Bootstrapper.Instance.ServiceRegistry.Get<IShipStatsService>();

        if (Bootstrapper.Instance.ServiceRegistry.TryGet(
                out ITargetService2A targetService))
        {
            _targetService = targetService;
        }

        State = new SystemTravelState();
        _eventBus.Subscribe<TravelFinishedEvent>(OnTravelFinished);
        _eventBus.Subscribe<GameTickStartedEvent>(OnGameTickStarted);
        _eventBus.Subscribe<GameTimeQuantumAdvancedEvent>(OnGameTimeQuantumAdvanced);
    }

    private void OnGameTickStarted(GameTickStartedEvent evt)
    {
        RefreshNpcDestinationAtTickStart(evt.CurrentTick);
    }

    private void OnGameTimeQuantumAdvanced(GameTimeQuantumAdvancedEvent evt)
    {
        RefreshNpcDestinationAtTickStart(
            evt.CurrentDay + 1,
            force: true);
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
        State.DestinationPosition = GetCurrentActiveRouteDestinationPosition();
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

    public void SetStationDestination(StationConfig stationData)
    {
        if (stationData == null)
        {
            Debug.LogWarning("[SystemTravelService] Cannot set station destination: stationData is null.");
            return;
        }

        State.Destination = SystemTravelDestination.Station(stationData);
        State.DestinationPosition = stationData.LocalOffset;
        State.Status = SystemTravelStatus.DestinationSelected;
        State.TravelProgress01 = 0f;

        _eventBus.Publish(new DestinationSelectedEvent(
            TravelDestinationType.Station,
            State.DestinationPosition,
            string.Empty,
            string.Empty,
            stationData.Id
        ));

        LogCustom($"Station destination selected: {stationData.Id}");
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

    public void SetNpcDestination(string runtimeNpcId)
    {
        if (!TryGetNpcDestinationPosition(runtimeNpcId, out Vector3 npcPosition))
        {
            Debug.LogWarning(
                "[SystemTravelService] Cannot set NPC destination: NPC is missing or inactive. RuntimeNpcId: " +
                runtimeNpcId);

            return;
        }

        ClearPreviousTravelSelectionForNpcDestination();

        State.Destination = SystemTravelDestination.Npc(
            runtimeNpcId,
            npcPosition);

        State.DestinationPosition = npcPosition;
        State.Status = SystemTravelStatus.DestinationSelected;
        State.TravelProgress01 = 0f;
        _lastNpcDestinationRefreshTick = -1;

        _eventBus.Publish(new DestinationSelectedEvent(
            TravelDestinationType.Npc,
            npcPosition,
            string.Empty,
            string.Empty,
            string.Empty,
            runtimeNpcId
        ));

        LogCustom($"NPC destination selected: {runtimeNpcId}");
        LogCustom("State = " + State);

        StartTravelAutomaticallyIfPossible();
    }

    private void ClearPreviousTravelSelectionForNpcDestination()
    {
        if (State.Status == SystemTravelStatus.Flying ||
            State.Status == SystemTravelStatus.DestinationSelected)
        {
            CancelTravel();
        }
        else
        {
            _eventBus.Publish(new SystemTravelCancelledEvent());
        }

        _targetService?.ClearTarget();
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
        State.TravelProgress01 = 0f;
        State.Status = SystemTravelStatus.Flying;
        _travelFacingDirection = GetCurrentShipFacingDirection();
        _routePreviewStartFacingDirection = _travelFacingDirection;
        _activeTravelRoutePath.Clear();
        _activeRouteDistanceTravelled = 0f;
        _routeTurnAdjustmentFactor = 1f;
        _routeSpeedAdjustmentFactor = 1f;
        ClearRouteInitialSpeedLimit();
        _mapPointRouteTraceFrameCount = 0;
        if (IsMapPointDestination())
        {
            _mapPointRouteBuildId++;
            _isMapPointRouteBuildTraceEnabled = true;
        }

        RouteDestinationClassification routeClassification =
            ClassifyRouteDestination(
                State.StartPosition,
                State.DestinationPosition,
                _routePreviewStartFacingDirection);
        _activeRouteDestinationCase =
            routeClassification.DestinationCase;

        if (IsMapPointDestination())
        {
            LogMapPointRouteCaseTrace(
                routeClassification);

            LogRouteDebugSnapshot(
                "Before route build",
                State.StartPosition,
                State.DestinationPosition,
                _routePreviewStartFacingDirection);
        }

        if (_activeRouteDestinationCase ==
            RouteDestinationCase.ForbiddenDestination)
        {
            Debug.LogWarning("[SystemTravelService] Cannot start travel: destination is inside the sun blocking radius.");
            if (IsMapPointDestination())
            {
                RejectMapPointTravelDestination(
                    "destination is inside the sun blocking radius");
                return;
            }

            ResetFailedTravelToDestinationSelected();
            return;
        }

        RouteAdjustment routeAdjustment =
            ResolveRouteAdjustment(
                State.StartPosition,
                State.DestinationPosition,
                _routePreviewStartFacingDirection);
        _routeTurnAdjustmentFactor =
            routeAdjustment.TurnFactor;
        _routeSpeedAdjustmentFactor =
            routeAdjustment.SpeedFactor;
        RebuildTravelRoutePath(
            _activeTravelRoutePath,
            State.StartPosition,
            State.DestinationPosition,
            _routePreviewStartFacingDirection,
            GetAdjustedShipTurnRadius(_routeTurnAdjustmentFactor),
            GetCurrentEffectiveTravelSpeed());
        ApplyShortRouteSpeedAdjustment();
        ConfigureRouteInitialSpeedLimit(
            routeClassification,
            State.StartPosition,
            _routePreviewStartFacingDirection);

        if (_activeTravelRoutePath.Count <= 1)
        {
            Debug.LogWarning("[SystemTravelService] Cannot start travel: route could not be built.");
            if (IsMapPointDestination())
            {
                RejectMapPointTravelDestination(
                    "route could not be built");
                return;
            }

            ResetFailedTravelToDestinationSelected();
            return;
        }

        if (_activeTravelRoutePath.Count > 0)
        {
            State.DestinationPosition =
                _activeTravelRoutePath[_activeTravelRoutePath.Count - 1];
        }

        State.TravelDistance =
            GetPathLength(_activeTravelRoutePath);

        if (IsMapPointDestination())
        {
            LogRouteDebugSnapshot(
                "After route build",
                State.StartPosition,
                State.DestinationPosition,
                _routePreviewStartFacingDirection);
            _isMapPointRouteBuildTraceEnabled = false;
        }

        _eventBus.Publish(new SystemTravelStartedEvent(
            State.Destination.Type,
            State.StartPosition,
            State.DestinationPosition
        ));

        LogCustom("Travel started.");
        LogCustom("State = " + State);
    }

    private void ResetFailedTravelToDestinationSelected()
    {
        _isMapPointRouteBuildTraceEnabled = false;
        State.Status = SystemTravelStatus.DestinationSelected;
        State.TravelDistance = 0f;
        State.TravelProgress01 = 0f;
        _activeRouteDistanceTravelled = 0f;
        _activeTravelRoutePath.Clear();
        ClearRouteInitialSpeedLimit();
    }

    private void RejectMapPointTravelDestination(
        string reason)
    {
        LogCustom(
            "[RouteTrace.RejectMapPoint] " +
            "RouteBuildId = " +
            _mapPointRouteBuildId +
            " | RouteCase = " +
            _activeRouteDestinationCase +
            " | Reason = " +
            reason);

        _isMapPointRouteBuildTraceEnabled = false;
        CancelTravel();
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
        _activeTravelRoutePath.Clear();
        _activeRouteDistanceTravelled = 0f;
        _routeTurnAdjustmentFactor = 1f;
        _routeSpeedAdjustmentFactor = 1f;
        ClearRouteInitialSpeedLimit();

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

    private float GetCurrentEffectiveTravelSpeed()
    {
        float speed =
            GetCurrentShipTravelSpeed();

        if (State == null ||
            State.Status != SystemTravelStatus.Flying)
        {
            return speed;
        }

        return speed *
               GetCurrentEffectiveTravelSpeedFactor();
    }

    private float GetCurrentEffectiveTravelSpeedFactor()
    {
        float speedFactor =
            Mathf.Clamp01(_routeSpeedAdjustmentFactor);

        if (_useRouteInitialSpeedLimit &&
            _activeRouteDistanceTravelled <
            _routeInitialSpeedLimitDistance - RouteSegmentEpsilon)
        {
            speedFactor =
                Mathf.Min(
                    speedFactor,
                    Mathf.Clamp01(_routeInitialSpeedLimitFactor));
        }

        return speedFactor;
    }

    private float GetCurrentPreviewTravelSpeed()
    {
        float speed =
            GetCurrentShipTravelSpeed();

        if (State == null ||
            !State.HasDestination)
        {
            return speed;
        }

        if (State.Status == SystemTravelStatus.Flying)
        {
            return speed *
                   Mathf.Clamp01(_routeSpeedAdjustmentFactor);
        }

        Vector3 startPosition =
            GetCurrentShipPosition();

        Vector3 destinationPosition =
            GetCurrentDestinationPosition();

        Vector2 facingDirection =
            GetCurrentShipFacingDirection();

        RouteAdjustment adjustment =
            ResolveRouteAdjustment(
                startPosition,
                destinationPosition,
                facingDirection);

        return speed *
               adjustment.SpeedFactor;
    }

    private void LogRouteDebugSnapshot(
        string label,
        Vector3 shipPosition,
        Vector3 targetPosition,
        Vector2 shipFacingDirection)
    {
        if (!ShouldLogMapPointRouteBuildTrace())
            return;

        Vector3 directVector =
            targetPosition - shipPosition;

        float directDistance =
            directVector.magnitude;

        SunAvoidanceObstacle obstacle =
            GetSunAvoidanceObstacle(
                shipPosition.z,
                targetPosition);

        float targetDistanceFromSun =
            obstacle.HasObstacle
                ? Vector2.Distance(
                    new Vector2(
                        targetPosition.x,
                        targetPosition.y),
                    new Vector2(
                        obstacle.Center.x,
                        obstacle.Center.y))
                : 0f;

        LogCustom(
            "[RouteDebug] " +
            label +
            " | RouteBuildId = " +
            _mapPointRouteBuildId +
            " | RouteCase = " +
            _activeRouteDestinationCase +
            " | DirectDistance = " +
            directDistance.ToString("0.###") +
            " | ShipPosition = " +
            FormatVector3(shipPosition) +
            " | TargetPosition = " +
            FormatVector3(targetPosition) +
            " | ShipFacingDirection = " +
            FormatVector2(shipFacingDirection) +
            " | BaseSpeed = " +
            GetCurrentShipTravelSpeed().ToString("0.###") +
            " | EffectiveSpeed = " +
            GetCurrentEffectiveTravelSpeed().ToString("0.###") +
            " | BaseTurnRadius = " +
            GetCurrentShipTurnRadius().ToString("0.###") +
            " | EffectiveTurnRadius = " +
            GetAdjustedShipTurnRadius(_routeTurnAdjustmentFactor).ToString("0.###") +
            " | AdjustmentFactor = " +
            _routeTurnAdjustmentFactor.ToString("0.###") +
            " | SpeedAdjustmentFactor = " +
            _routeSpeedAdjustmentFactor.ToString("0.###") +
            " | ActiveRouteLength = " +
            GetPathLength(_activeTravelRoutePath).ToString("0.###") +
            " | ActiveRoutePoints = " +
            _activeTravelRoutePath.Count +
            " | TargetDistanceFromSun = " +
            targetDistanceFromSun.ToString("0.###") +
            " | SunAvoidanceRadius = " +
            obstacle.Radius.ToString("0.###") +
            " | SunBlockingRadius = " +
            obstacle.BlockingRadius.ToString("0.###"));
    }

    private RouteDestinationClassification ClassifyRouteDestination(
        Vector3 shipPosition,
        Vector3 targetPosition,
        Vector2 shipFacingDirection)
    {
        Vector3 directVector =
            targetPosition - shipPosition;

        Vector2 directDirection =
            new Vector2(
                directVector.x,
                directVector.y);

        float directDistance =
            directDirection.magnitude;

        float turnRadius =
            GetCurrentShipTurnRadius();

        float nearDistanceThreshold =
            GetRouteNearDistanceThreshold(turnRadius);

        float bearingAngleDegrees =
            directDirection.sqrMagnitude > RouteSegmentEpsilon
                ? Vector2.Angle(
                    TurnRadiusRouteMath2A.NormalizeDirectionOrUp(
                        shipFacingDirection),
                    directDirection.normalized)
                : 0f;

        SunAvoidanceObstacle obstacle =
            GetSunAvoidanceObstacle(
                shipPosition.z,
                targetPosition);

        float startDistanceFromSun =
            obstacle.HasObstacle
                ? GetDistanceToObstacleCenter(
                    shipPosition,
                    obstacle)
                : 0f;

        float targetDistanceFromSun =
            obstacle.HasObstacle
                ? GetDistanceToObstacleCenter(
                    targetPosition,
                    obstacle)
                : 0f;

        bool startInsideSunSafety =
            obstacle.HasObstacle &&
            startDistanceFromSun < obstacle.Radius;

        bool targetInsideSunSafety =
            obstacle.HasObstacle &&
            targetDistanceFromSun < obstacle.Radius;

        bool targetInsideSunBody =
            obstacle.HasObstacle &&
            targetDistanceFromSun < obstacle.BlockingRadius;

        bool requiresSunAvoidance =
            obstacle.HasObstacle &&
            SegmentIntersectsCircle(
                shipPosition,
                targetPosition,
                obstacle.Center,
                obstacle.Radius);

        bool isNear =
            directDistance <= nearDistanceThreshold;

        RouteDestinationCase destinationCase =
            GetRouteDestinationCase(
                shipPosition,
                targetPosition,
                shipFacingDirection,
                obstacle,
                directDistance,
                bearingAngleDegrees,
                isNear,
                requiresSunAvoidance,
                startInsideSunSafety,
                targetInsideSunSafety,
                targetInsideSunBody);

        return new RouteDestinationClassification(
            destinationCase,
            directDistance,
            bearingAngleDegrees,
            nearDistanceThreshold,
            isNear,
            requiresSunAvoidance,
            startInsideSunSafety,
            targetInsideSunSafety,
            targetInsideSunBody,
            startDistanceFromSun,
            targetDistanceFromSun,
            obstacle);
    }

    private RouteDestinationCase GetRouteDestinationCase(
        Vector3 shipPosition,
        Vector3 targetPosition,
        Vector2 shipFacingDirection,
        SunAvoidanceObstacle obstacle,
        float directDistance,
        float bearingAngleDegrees,
        bool isNear,
        bool requiresSunAvoidance,
        bool startInsideSunSafety,
        bool targetInsideSunSafety,
        bool targetInsideSunBody)
    {
        /*
         * Контракт классификации маршрута:
         * 1. Сначала безопасность солнца: запретная зона, старт/цель внутри safety-зоны.
         * 2. Затем пересечение прямого пути с safety-зоной солнца.
         * 3. Затем обычная геометрия по дистанции и углу к носу корабля.
         *
         * Для тестов:
         * - StartInsideSunSafety => один из NearSunStart* по ориентации к солнцу и сектору цели.
         * - Нос от солнца: angle(facing, ship-sun radial) <= ForwardSectorAngle.
         * - Нос на солнце: angle(facing, ship-sun radial) >= BehindSectorAngle.
         * - Между ними => нос примерно по касательной.
         * - DirectDistance <= NearDistanceThreshold => Near, иначе Far.
         * - BearingAngle <= ForwardSectorAngle => Forward.
         * - BearingAngle >= BehindSectorAngle => Behind.
         * - Между ними => Side.
         * - Near + Behind + не пересекает SunBlockingRadius => NearBehind до SunBlocked.
         */
        if (State != null &&
            State.Destination != null &&
            State.Destination.Type == TravelDestinationType.Npc)
        {
            return RouteDestinationCase.MovingTarget;
        }

        if (targetInsideSunBody)
            return RouteDestinationCase.ForbiddenDestination;

        if (startInsideSunSafety)
        {
            return GetNearSunStartDestinationCase(
                shipPosition,
                targetPosition,
                shipFacingDirection,
                obstacle,
                bearingAngleDegrees);
        }

        if (targetInsideSunSafety)
            return RouteDestinationCase.NearSunDestination;

        if (directDistance <= ArrivalDistanceThreshold)
            return RouteDestinationCase.NearForward;

        float forwardAngle =
            GetRouteForwardSectorAngleDegrees();

        float behindAngle =
            GetRouteBehindSectorAngleDegrees();

        if (isNear &&
            bearingAngleDegrees >= behindAngle &&
            !RouteSegmentIntersectsSunBody(
                shipPosition,
                targetPosition,
                obstacle))
        {
            return RouteDestinationCase.NearBehind;
        }

        if (requiresSunAvoidance)
            return RouteDestinationCase.SunBlocked;

        if (bearingAngleDegrees <= forwardAngle)
        {
            return isNear
                ? RouteDestinationCase.NearForward
                : RouteDestinationCase.FarForward;
        }

        if (bearingAngleDegrees >= behindAngle)
        {
            return isNear
                ? RouteDestinationCase.NearBehind
                : RouteDestinationCase.FarBehind;
        }

        return isNear
            ? RouteDestinationCase.NearSide
            : RouteDestinationCase.FarSide;
    }

    private RouteDestinationCase GetNearSunStartDestinationCase(
        Vector3 shipPosition,
        Vector3 targetPosition,
        Vector2 shipFacingDirection,
        SunAvoidanceObstacle obstacle,
        float targetBearingAngleDegrees)
    {
        if (!obstacle.HasObstacle)
            return RouteDestinationCase.NearSunStart;

        Vector2 radialAwayFromSun =
            new Vector2(
                shipPosition.x - obstacle.Center.x,
                shipPosition.y - obstacle.Center.y);

        if (radialAwayFromSun.sqrMagnitude <= RouteSegmentEpsilon)
            radialAwayFromSun = Vector2.right;

        radialAwayFromSun =
            radialAwayFromSun.normalized;

        Vector2 facingDirection =
            TurnRadiusRouteMath2A.NormalizeDirectionOrUp(
                shipFacingDirection);

        float sunFacingAngle =
            Vector2.Angle(
                facingDirection,
                radialAwayFromSun);

        float forwardAngle =
            GetRouteForwardSectorAngleDegrees();

        float behindAngle =
            GetRouteBehindSectorAngleDegrees();

        bool targetForward =
            targetBearingAngleDegrees <= forwardAngle;

        bool targetBehind =
            targetBearingAngleDegrees >= behindAngle;

        if (sunFacingAngle <= forwardAngle)
        {
            if (targetForward)
                return RouteDestinationCase.NearSunStartAwayForward;

            if (targetBehind)
                return RouteDestinationCase.NearSunStartAwayBehind;

            return RouteDestinationCase.NearSunStartAwaySide;
        }

        if (sunFacingAngle >= behindAngle)
        {
            if (targetForward)
                return RouteDestinationCase.NearSunStartTowardForward;

            if (targetBehind)
                return RouteDestinationCase.NearSunStartTowardBehind;

            return RouteDestinationCase.NearSunStartTowardSide;
        }

        if (targetForward)
            return RouteDestinationCase.NearSunStartTangentForward;

        if (targetBehind)
            return RouteDestinationCase.NearSunStartTangentBehind;

        Vector2 targetDirection =
            new Vector2(
                targetPosition.x - shipPosition.x,
                targetPosition.y - shipPosition.y);

        if (targetDirection.sqrMagnitude <= RouteSegmentEpsilon ||
            Vector2.Dot(targetDirection.normalized, radialAwayFromSun) >= 0f)
        {
            return RouteDestinationCase.NearSunStartTangentSideAway;
        }

        return RouteDestinationCase.NearSunStartTangentSideToward;
    }

    private void LogMapPointRouteCaseTrace(
        RouteDestinationClassification classification)
    {
        if (!ShouldLogMapPointRouteBuildTrace())
            return;

        LogCustom(
            "[RouteTrace.Case] " +
            "RouteBuildId = " +
            _mapPointRouteBuildId +
            " | " +
            "Case = " +
            classification.DestinationCase +
            " | DirectDistance = " +
            classification.DirectDistance.ToString("0.###") +
            " | NearDistanceThreshold = " +
            classification.NearDistanceThreshold.ToString("0.###") +
            " | BearingAngle = " +
            classification.BearingAngleDegrees.ToString("0.###") +
            " | ForwardSectorAngle = " +
            GetRouteForwardSectorAngleDegrees().ToString("0.###") +
            " | BehindSectorAngle = " +
            GetRouteBehindSectorAngleDegrees().ToString("0.###") +
            " | IsNear = " +
            classification.IsNear +
            " | RequiresSunAvoidance = " +
            classification.RequiresSunAvoidance +
            " | StartInsideSunSafety = " +
            classification.StartInsideSunSafety +
            " | TargetInsideSunSafety = " +
            classification.TargetInsideSunSafety +
            " | TargetInsideSunBody = " +
            classification.TargetInsideSunBody +
            " | StartDistanceFromSun = " +
            classification.StartDistanceFromSun.ToString("0.###") +
            " | TargetDistanceFromSun = " +
            classification.TargetDistanceFromSun.ToString("0.###") +
            " | SunAvoidanceRadius = " +
            classification.Obstacle.Radius.ToString("0.###") +
            " | SunBlockingRadius = " +
            classification.Obstacle.BlockingRadius.ToString("0.###"));
    }

    private static float GetDistanceToObstacleCenter(
        Vector3 position,
        SunAvoidanceObstacle obstacle)
    {
        return Vector2.Distance(
            new Vector2(
                position.x,
                position.y),
            new Vector2(
                obstacle.Center.x,
                obstacle.Center.y));
    }

    private bool IsNearSunStartFacingSun(
        RouteDestinationClassification classification,
        Vector3 shipPosition,
        Vector2 shipFacingDirection)
    {
        if (!IsNearSunStartTowardSunDestinationCase(classification.DestinationCase) ||
            !classification.Obstacle.HasObstacle)
        {
            return false;
        }

        Vector2 toSun =
            new Vector2(
                classification.Obstacle.Center.x - shipPosition.x,
                classification.Obstacle.Center.y - shipPosition.y);

        if (toSun.sqrMagnitude <= RouteSegmentEpsilon)
            return true;

        float angleToSun =
            Vector2.Angle(
                TurnRadiusRouteMath2A.NormalizeDirectionOrUp(
                    shipFacingDirection),
                toSun.normalized);

        return angleToSun <= GetRouteForwardSectorAngleDegrees();
    }

    private static bool IsNearSunStartTowardSunDestinationCase(
        RouteDestinationCase destinationCase)
    {
        return destinationCase == RouteDestinationCase.NearSunStartTowardForward ||
               destinationCase == RouteDestinationCase.NearSunStartTowardSide ||
               destinationCase == RouteDestinationCase.NearSunStartTowardBehind;
    }

    private static bool ShouldStartNearSunCaseWithMinimumTurnRadius(
        RouteDestinationCase destinationCase)
    {
        return destinationCase == RouteDestinationCase.NearSunStartAwaySide ||
               destinationCase == RouteDestinationCase.NearSunStartAwayBehind ||
               destinationCase == RouteDestinationCase.NearSunStartTangentSideAway ||
               destinationCase == RouteDestinationCase.NearSunStartTangentSideToward ||
               destinationCase == RouteDestinationCase.NearSunStartTangentBehind ||
               destinationCase == RouteDestinationCase.NearSunStartTowardForward ||
               destinationCase == RouteDestinationCase.NearSunStartTowardSide ||
               destinationCase == RouteDestinationCase.NearSunStartTowardBehind;
    }

    private void ClearRouteInitialSpeedLimit()
    {
        _useRouteInitialSpeedLimit = false;
        _routeInitialSpeedLimitFactor = 1f;
        _routeInitialSpeedLimitDistance = 0f;
    }

    private void ConfigureRouteInitialSpeedLimit(
        RouteDestinationClassification classification,
        Vector3 shipPosition,
        Vector2 shipFacingDirection)
    {
        ClearRouteInitialSpeedLimit();

        if (!IsNearSunStartFacingSun(
                classification,
                shipPosition,
                shipFacingDirection))
        {
            return;
        }

        float slowDistance =
            GetRouteDistanceUntilNoLongerFacingSun(
                _activeTravelRoutePath,
                classification.Obstacle);

        if (slowDistance <= RouteSegmentEpsilon)
            return;

        _useRouteInitialSpeedLimit = true;
        _routeInitialSpeedLimitFactor =
            GetMinRouteTurnRadiusAdjustmentFactor();
        _routeInitialSpeedLimitDistance =
            slowDistance;

        LogMapPointInitialSpeedLimitTrace(
            "Enabled",
            slowDistance,
            _routeInitialSpeedLimitFactor);
    }

    private float GetRouteDistanceUntilNoLongerFacingSun(
        IReadOnlyList<Vector3> routePath,
        SunAvoidanceObstacle obstacle)
    {
        if (routePath == null ||
            routePath.Count <= 1 ||
            !obstacle.HasObstacle)
        {
            return 0f;
        }

        float distance =
            0f;

        for (int i = 1; i < routePath.Count; i++)
        {
            Vector3 from =
                routePath[i - 1];

            Vector3 to =
                routePath[i];

            Vector3 segment3 =
                to - from;

            Vector2 segment =
                new Vector2(
                    segment3.x,
                    segment3.y);

            float segmentDistance =
                segment.magnitude;

            if (segmentDistance <= RouteSegmentEpsilon)
                continue;

            if (!IsDirectionFacingSunFromPoint(
                    from,
                    segment.normalized,
                    obstacle))
            {
                break;
            }

            distance += segmentDistance;
        }

        return distance;
    }

    private bool IsDirectionFacingSunFromPoint(
        Vector3 point,
        Vector2 direction,
        SunAvoidanceObstacle obstacle)
    {
        Vector2 toSun =
            new Vector2(
                obstacle.Center.x - point.x,
                obstacle.Center.y - point.y);

        if (toSun.sqrMagnitude <= RouteSegmentEpsilon)
            return true;

        float angleToSun =
            Vector2.Angle(
                TurnRadiusRouteMath2A.NormalizeDirectionOrUp(direction),
                toSun.normalized);

        return angleToSun <= GetRouteForwardSectorAngleDegrees();
    }

    private void LogMapPointInitialSpeedLimitTrace(
        string phase,
        float slowDistance,
        float speedFactor)
    {
        if (!ShouldLogMapPointRouteBuildTrace())
            return;

        LogCustom(
            "[RouteTrace.InitialSpeedLimit] " +
            phase +
            " | RouteBuildId = " +
            _mapPointRouteBuildId +
            " | RouteCase = " +
            _activeRouteDestinationCase +
            " | SlowDistance = " +
            slowDistance.ToString("0.###") +
            " | SpeedFactor = " +
            speedFactor.ToString("0.###") +
            " | EffectiveSpeed = " +
            (GetCurrentShipTravelSpeed() *
             Mathf.Clamp01(speedFactor)).ToString("0.###"));
    }

    private static string FormatVector3(
        Vector3 value)
    {
        return "(" +
               value.x.ToString("0.###") +
               ", " +
               value.y.ToString("0.###") +
               ", " +
               value.z.ToString("0.###") +
               ")";
    }

    private static string FormatVector2(
        Vector2 value)
    {
        return "(" +
               value.x.ToString("0.###") +
               ", " +
               value.y.ToString("0.###") +
               ")";
    }

    private void LogMapPointRouteTraceTick(
        string phase,
        float deltaTime,
        int quantTick,
        Vector3 currentPosition,
        Vector3 destinationPosition,
        float movementDistance,
        float actualPositionDelta,
        string completionReason,
        bool isTerminal)
    {
        if (!IsMapPointDestination())
            return;

        bool suspiciousPositionDelta =
            actualPositionDelta >
            movementDistance + ArrivalDistanceThreshold;

        if (!isTerminal &&
            !suspiciousPositionDelta &&
            _mapPointRouteTraceFrameCount >= 20)
        {
            return;
        }

        _mapPointRouteTraceFrameCount++;

        float totalPathLength =
            GetPathLength(_activeTravelRoutePath);

        float remainingRouteDistance =
            Mathf.Max(
                0f,
                totalPathLength -
                _activeRouteDistanceTravelled);

        LogCustom(
            "[RouteTrace.Tick] " +
            phase +
            " | RouteBuildId = " +
            _mapPointRouteBuildId +
            " | RouteCase = " +
            _activeRouteDestinationCase +
            " | Frame = " +
            _mapPointRouteTraceFrameCount +
            " | QuantTick = " +
            quantTick +
            " | DeltaTime = " +
            deltaTime.ToString("0.####") +
            " | CurrentPosition = " +
            FormatVector3(currentPosition) +
            " | DestinationPosition = " +
            FormatVector3(destinationPosition) +
            " | DirectDistanceNow = " +
            Vector3.Distance(
                currentPosition,
                destinationPosition).ToString("0.###") +
            " | MovementDistance = " +
            movementDistance.ToString("0.###") +
            " | ActualPositionDelta = " +
            actualPositionDelta.ToString("0.###") +
            " | RouteDistanceTravelled = " +
            _activeRouteDistanceTravelled.ToString("0.###") +
            " | RemainingRouteDistance = " +
            remainingRouteDistance.ToString("0.###") +
            " | RouteLength = " +
            totalPathLength.ToString("0.###") +
            " | EffectiveSpeed = " +
            GetCurrentEffectiveTravelSpeed().ToString("0.###") +
            " | EffectiveSpeedFactor = " +
            GetCurrentEffectiveTravelSpeedFactor().ToString("0.###") +
            " | InitialSpeedLimitDistance = " +
            _routeInitialSpeedLimitDistance.ToString("0.###") +
            " | Facing = " +
            FormatVector2(_travelFacingDirection) +
            " | ActiveRoutePoints = " +
            _activeTravelRoutePath.Count +
            " | CompletionReason = " +
            completionReason +
            " | Terminal = " +
            isTerminal);
    }

    public void Tick(float deltaTime, int quantTick)
    {
        if (State.Status != SystemTravelStatus.Flying)
            return;

        State.DestinationPosition =
            GetCurrentActiveRouteDestinationPosition();

        if (State.Status != SystemTravelStatus.Flying)
            return;

        Vector3 direction = State.DestinationPosition - State.GetCurrentPosition();
        float distanceToDestination = direction.magnitude;
        bool hasActiveTravelRoute =
            HasActiveTravelRoute();

        LogMapPointRouteTraceTick(
            "BeforeMove",
            deltaTime,
            quantTick,
            State.GetCurrentPosition(),
            State.DestinationPosition,
            0f,
            0f,
            "None",
            false);

        if (!hasActiveTravelRoute &&
            distanceToDestination <= ArrivalDistanceThreshold)
        {
            LogMapPointRouteTraceTick(
                "ArrivalDistanceBeforeMove",
                deltaTime,
                quantTick,
                State.GetCurrentPosition(),
                State.DestinationPosition,
                0f,
                0f,
                "DirectDistanceBeforeMoveNoActiveRoute",
                true);

            if (IsNpcDestination())
            {
                PublishTravelProgress(1f);
                return;
            }

            CompleteTravel();
            return;
        }

        float movementDistance = GetCurrentEffectiveTravelSpeed() * deltaTime;
        Vector3 positionBeforeMove =
            State.GetCurrentPosition();

        bool destinationReached;
        Vector3 nextPosition = CalculateNextTravelPositionOnActiveRoute(
            movementDistance,
            out destinationReached);

        State.SetCurrentPosition(nextPosition);
        SyncShipMovementStateWithTravel(
            nextPosition,
            GetCurrentEffectiveTravelSpeed());
        LogMapPointRouteTraceTick(
            "AfterMove",
            deltaTime,
            quantTick,
            nextPosition,
            State.DestinationPosition,
            movementDistance,
            Vector3.Distance(
                positionBeforeMove,
                nextPosition),
            destinationReached
                ? "ReachedEndOfActiveRoute"
                : "None",
            destinationReached);

        if (destinationReached ||
             (!hasActiveTravelRoute &&
             Vector3.Distance(State.GetCurrentPosition(), State.DestinationPosition) <= ArrivalDistanceThreshold))
        {
            string completionReason =
                destinationReached
                    ? "ReachedEndOfActiveRoute"
                    : "DirectDistanceAfterMoveNoActiveRoute";

            LogMapPointRouteTraceTick(
                "CompleteConditionAfterMove",
                deltaTime,
                quantTick,
                State.GetCurrentPosition(),
                State.DestinationPosition,
                movementDistance,
                Vector3.Distance(
                    positionBeforeMove,
                    State.GetCurrentPosition()),
                completionReason,
                true);

            if (IsNpcDestination())
            {
                PublishTravelProgress(1f);
                return;
            }

            CompleteTravel();
            return;
        }

        if (State.TravelDistance > 0f)
        {
            State.TravelProgress01 = Mathf.Clamp01(
                _activeRouteDistanceTravelled /
                State.TravelDistance
            );
        }
        else
        {
            State.TravelProgress01 = 1f;
        }

        PublishTravelProgress(State.TravelProgress01);
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
        if (IsMapPointDestination())
        {
            LogCustom(
                "[RouteTrace.CompleteTravel] " +
                "BeforeSnap | RouteBuildId = " +
                _mapPointRouteBuildId +
                " | RouteCase = " +
                _activeRouteDestinationCase +
                " | CurrentPosition = " +
                FormatVector3(State.GetCurrentPosition()) +
                " | DestinationPosition = " +
                FormatVector3(State.DestinationPosition) +
                " | SnapDistance = " +
                Vector3.Distance(
                    State.GetCurrentPosition(),
                    State.DestinationPosition).ToString("0.###") +
                " | RouteDistanceTravelled = " +
                _activeRouteDistanceTravelled.ToString("0.###") +
                " | RouteLength = " +
                GetPathLength(_activeTravelRoutePath).ToString("0.###") +
                " | ActiveRoutePoints = " +
                _activeTravelRoutePath.Count);
        }

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
        _activeTravelRoutePath.Clear();
        _activeRouteDistanceTravelled = 0f;
        _routeTurnAdjustmentFactor = 1f;
        _routeSpeedAdjustmentFactor = 1f;
        ClearRouteInitialSpeedLimit();

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

            case TravelDestinationType.Station:
                return State.Destination.FixedMapPosition;

            case TravelDestinationType.Npc:
                if (TryGetNpcDestinationPosition(
                        State.Destination.RuntimeNpcId,
                        out Vector3 npcPosition))
                {
                    State.DestinationPosition = npcPosition;
                    State.Destination.FixedMapPosition = npcPosition;
                    return npcPosition;
                }

                return State.DestinationPosition;

            case TravelDestinationType.SystemExit:
                return State.Destination.FixedMapPosition;

            default:
                return State.GetCurrentPosition();
        }
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
                GetCurrentPreviewTravelSpeed());

        float distancePerTick =
            speed *
            safeSecondsPerTick;

        if (
            State.Destination != null &&
            (State.Destination.Type ==
                TravelDestinationType.Planet ||
             State.Destination.Type ==
                TravelDestinationType.Npc))
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
        Vector3 currentPosition =
            GetCurrentShipPosition();

        Vector3 destinationPosition =
            GetCurrentDestinationPosition();

        float distanceToDestination =
            Vector3.Distance(
                currentPosition,
                destinationPosition);

        if (
            distanceToDestination <=
            ArrivalDistanceThreshold)
        {
            return;
        }

        Vector3 routeStartPosition =
            State.Status == SystemTravelStatus.Flying
                ? State.StartPosition
                : currentPosition;

        Vector2 routeStartFacingDirection =
            State.Status == SystemTravelStatus.Flying
                ? _routePreviewStartFacingDirection
                : GetCurrentShipFacingDirection();

        RouteAdjustment routeAdjustment =
            State.Status == SystemTravelStatus.Flying
                ? new RouteAdjustment(
                    _routeTurnAdjustmentFactor,
                    _routeSpeedAdjustmentFactor)
                : ResolveRouteAdjustment(
                    routeStartPosition,
                    destinationPosition,
                    routeStartFacingDirection);

        if (State.Status == SystemTravelStatus.Flying &&
            _activeTravelRoutePath.Count > 1)
        {
            _routePreviewPathBuffer.Clear();
            _routePreviewPathBuffer.AddRange(_activeTravelRoutePath);
        }
        else
        {
            RebuildTravelRoutePath(
                _routePreviewPathBuffer,
                routeStartPosition,
                destinationPosition,
                routeStartFacingDirection,
                GetAdjustedShipTurnRadius(routeAdjustment.TurnFactor),
                GetCurrentShipTravelSpeed() *
                routeAdjustment.SpeedFactor);
        }

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
            State.Status == SystemTravelStatus.Flying
                ? _activeRouteDistanceTravelled
                : GetClosestDistanceOnPath(
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
        BuildSunAwareTravelPath(
            from,
            to,
            path,
            false);
    }

    private void BuildForcedSunAvoidanceTravelPath(
        Vector3 from,
        Vector3 to,
        List<Vector3> path,
        Vector2 startFacingDirection,
        float turnRadius)
    {
        BuildSunAwareTravelPath(
            from,
            to,
            path,
            true,
            startFacingDirection,
            turnRadius);
    }

    private void BuildSunStartExitTravelPath(
        Vector3 from,
        Vector3 to,
        List<Vector3> path,
        SunAvoidanceObstacle obstacle)
    {
        if (path == null)
            return;

        path.Clear();
        path.Add(from);

        if (!obstacle.HasObstacle ||
            obstacle.Radius <= 0f)
        {
            path.Add(to);
            return;
        }

        Vector2 fromCenter =
            new Vector2(
                from.x - obstacle.Center.x,
                from.y - obstacle.Center.y);

        Vector2 exitDirection =
            fromCenter.sqrMagnitude > RouteSegmentEpsilon
                ? fromCenter.normalized
                : new Vector2(to.x - obstacle.Center.x, to.y - obstacle.Center.y).normalized;

        if (exitDirection.sqrMagnitude <= RouteSegmentEpsilon)
            exitDirection = Vector2.right;

        Vector2 exitPoint =
            new Vector2(
                obstacle.Center.x,
                obstacle.Center.y) +
            exitDirection *
            (obstacle.Radius + ArrivalDistanceThreshold);

        path.Add(
            new Vector3(
                exitPoint.x,
                exitPoint.y,
                from.z));
        path.Add(to);
    }

    private void BuildSunStartTowardTangentEscapeTravelPath(
        Vector3 from,
        Vector3 to,
        List<Vector3> path,
        SunAvoidanceObstacle obstacle)
    {
        if (path == null)
            return;

        path.Clear();
        path.Add(from);

        if (!obstacle.HasObstacle ||
            obstacle.Radius <= 0f)
        {
            path.Add(to);
            return;
        }

        Vector2 center =
            new Vector2(
                obstacle.Center.x,
                obstacle.Center.y);

        Vector2 from2 =
            new Vector2(
                from.x,
                from.y);

        Vector2 to2 =
            new Vector2(
                to.x,
                to.y);

        Vector2 radialAway =
            from2 - center;

        if (radialAway.sqrMagnitude <= RouteSegmentEpsilon)
            radialAway = to2 - center;

        if (radialAway.sqrMagnitude <= RouteSegmentEpsilon)
            radialAway = Vector2.right;

        radialAway =
            radialAway.normalized;

        Vector2 tangentA =
            new Vector2(
                -radialAway.y,
                radialAway.x);

        Vector2 tangentB =
            new Vector2(
                radialAway.y,
                -radialAway.x);

        float startDistanceFromSun =
            Vector2.Distance(
                from2,
                center);

        SunStartTangentEscapeOption tangentOption =
            GetBestSunStartTangentEscapeOption(
                from,
                to,
                center,
                tangentA,
                tangentB,
                obstacle,
                startDistanceFromSun);

        Vector2 tangentExit =
            from2 +
            tangentOption.Direction *
            tangentOption.Distance;

        path.Add(
            new Vector3(
                tangentExit.x,
                tangentExit.y,
                from.z));
        path.Add(to);

        LogMapPointSunStartTangentEscapeTrace(
            tangentOption.Direction,
            tangentOption.Distance,
            path);
    }

    private static SunStartTangentEscapeOption GetBestSunStartTangentEscapeOption(
        Vector3 from,
        Vector3 to,
        Vector2 center,
        Vector2 tangentA,
        Vector2 tangentB,
        SunAvoidanceObstacle obstacle,
        float startDistanceFromSun)
    {
        float safeRadius =
            obstacle.Radius + ArrivalDistanceThreshold;

        float tangentDistance =
            GetSunStartTangentEscapeDistance(
                startDistanceFromSun,
                safeRadius);

        SunStartTangentEscapeOption optionA =
            GetSunStartTangentEscapeOption(
                from,
                to,
                center,
                tangentA,
                tangentDistance,
                obstacle);

        SunStartTangentEscapeOption optionB =
            GetSunStartTangentEscapeOption(
                from,
                to,
                center,
                tangentB,
                tangentDistance,
                obstacle);

        return optionA.Score <= optionB.Score
                ? optionA
                : optionB;
    }

    private static SunStartTangentEscapeOption GetSunStartTangentEscapeOption(
        Vector3 from,
        Vector3 to,
        Vector2 center,
        Vector2 tangentDirection,
        float tangentDistance,
        SunAvoidanceObstacle obstacle)
    {
        Vector2 from2 =
            new Vector2(
                from.x,
                from.y);

        Vector2 to2 =
            new Vector2(
                to.x,
                to.y);

        float maxDistance =
            tangentDistance +
            Vector2.Distance(from2, to2) +
            obstacle.Radius * 4f;

        float stepDistance =
            Mathf.Max(
                ArrivalDistanceThreshold,
                obstacle.Radius * 0.1f);

        SunStartTangentEscapeOption bestOption =
            ScoreSunStartTangentEscapeOption(
                from,
                to,
                center,
                tangentDirection,
                tangentDistance,
                obstacle);

        for (float distance = tangentDistance + stepDistance;
             distance <= maxDistance;
             distance += stepDistance)
        {
            SunStartTangentEscapeOption option =
                ScoreSunStartTangentEscapeOption(
                    from,
                    to,
                    center,
                    tangentDirection,
                    distance,
                    obstacle);

            if (option.Score < bestOption.Score)
                bestOption = option;

            if (option.Score < 100000f)
                break;
        }

        return bestOption;
    }

    private static SunStartTangentEscapeOption ScoreSunStartTangentEscapeOption(
        Vector3 from,
        Vector3 to,
        Vector2 center,
        Vector2 tangentDirection,
        float tangentDistance,
        SunAvoidanceObstacle obstacle)
    {
        Vector2 from2 =
            new Vector2(
                from.x,
                from.y);

        Vector2 to2 =
            new Vector2(
                to.x,
                to.y);

        Vector2 tangentExit =
            from2 +
            tangentDirection *
            tangentDistance;

        Vector3 tangentExit3 =
            new Vector3(
                tangentExit.x,
                tangentExit.y,
                from.z);

        float score =
            Vector2.Distance(tangentExit, to2);

        if (RouteSegmentIntersectsSunBody(
                from,
                tangentExit3,
                obstacle))
        {
            score += 100000f;
        }

        if (RouteSegmentIntersectsSunBody(
                tangentExit3,
                to,
                obstacle))
        {
            score += 100000f;
        }

        float exitDistanceFromSun =
            Vector2.Distance(
                tangentExit,
                center);

        if (exitDistanceFromSun < obstacle.Radius)
            score += 100000f;

        score += tangentDistance * 0.01f;

        return new SunStartTangentEscapeOption(
            tangentDirection,
            tangentDistance,
            score);
    }

    private static float GetSunStartTangentEscapeDistance(
        float startDistanceFromSun,
        float safeRadius)
    {
        float tangentDistance =
            Mathf.Sqrt(
                Mathf.Max(
                    0f,
                    safeRadius * safeRadius -
                    startDistanceFromSun * startDistanceFromSun));

        return Mathf.Max(
            tangentDistance,
            ArrivalDistanceThreshold * 2f);
    }

    private void BuildSunAdjustmentTravelPath(
        Vector3 from,
        Vector3 to,
        List<Vector3> path,
        Vector2 startFacingDirection,
        float turnRadius,
        SunAvoidanceObstacle obstacle,
        RouteDestinationCase destinationCase)
    {
        if (IsNearSunStartTowardSunDestinationCase(destinationCase))
        {
            BuildSunStartTowardTangentEscapeTravelPath(
                from,
                to,
                path,
                obstacle);
            return;
        }

        if (IsNearSunStartDestinationCase(destinationCase))
        {
            BuildSunStartExitTravelPath(
                from,
                to,
                path,
                obstacle);
            return;
        }

        BuildForcedSunAvoidanceTravelPath(
            from,
            to,
            path,
            startFacingDirection,
            turnRadius);
    }

    private void BuildSunAwareTravelPath(
    Vector3 from,
    Vector3 to,
    List<Vector3> path,
    bool forceAvoidance
)
    {
        BuildSunAwareTravelPath(
            from,
            to,
            path,
            forceAvoidance,
            Vector2.up,
            0f);
    }

    private void BuildSunAwareTravelPath(
    Vector3 from,
    Vector3 to,
    List<Vector3> path,
    bool forceAvoidance,
    Vector2 startFacingDirection,
    float turnRadius
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

        float sunRadius = Mathf.Max(0f, GetSunWorldSize(sun) * 0.5f);
        float avoidanceRadius =
            GetRoutePlanningAvoidanceRadius(
                to,
                sunCenter,
                sunRadius);

        SystemTravelSunAvoidancePath2A.BuildPath(
            path,
            from,
            to,
            sunCenter,
            avoidanceRadius,
            SunAvoidanceArcSegments,
            forceAvoidance,
            startFacingDirection,
            turnRadius
        );
    }

    private static void BuildDirectTravelPath(
        Vector3 from,
        Vector3 to,
        List<Vector3> path)
    {
        if (path == null)
            return;

        path.Clear();
        path.Add(from);
        path.Add(to);
    }

    private SunAvoidanceObstacle GetSunAvoidanceObstacle(
        float z)
    {
        return GetSunAvoidanceObstacle(
            z,
            null);
    }

    private SunAvoidanceObstacle GetSunAvoidanceObstacle(
        float z,
        Vector3? destinationPosition)
    {
        StarSystemConfig currentSystem =
            _configService.GetCurrentSystemConfig();

        if (currentSystem == null ||
            currentSystem.Sun == null)
        {
            return new SunAvoidanceObstacle(
                false,
                Vector3.zero,
                0f,
                0f);
        }

        SunConfig sun =
            currentSystem.Sun;

        Vector3 sunCenter =
            new Vector3(
                sun.LocalOffset.x,
                sun.LocalOffset.y,
                z);

        float sunRadius =
            Mathf.Max(
                0f,
                GetSunWorldSize(sun) * 0.5f);

        float planningRadius =
            sunRadius + SunAvoidanceSafetyMargin;

        if (destinationPosition.HasValue)
        {
            planningRadius =
                GetRoutePlanningAvoidanceRadius(
                    destinationPosition.Value,
                    sunCenter,
                    sunRadius);
        }

        return new SunAvoidanceObstacle(
            true,
            sunCenter,
            planningRadius,
            sunRadius + SunCollisionSafetyMargin);
    }

    private static float GetRoutePlanningAvoidanceRadius(
        Vector3 destinationPosition,
        Vector3 sunCenter,
        float sunRadius)
    {
        float defaultRadius =
            sunRadius + SunAvoidanceSafetyMargin;

        float destinationDistanceFromSun =
            Vector2.Distance(
                new Vector2(
                    destinationPosition.x,
                    destinationPosition.y),
                new Vector2(
                    sunCenter.x,
                    sunCenter.y));

        if (destinationDistanceFromSun >= defaultRadius)
            return defaultRadius;

        return Mathf.Max(
            sunRadius + SunCollisionSafetyMargin,
            destinationDistanceFromSun - ArrivalDistanceThreshold);
    }

    private static bool RoutePathAvoidsSunBody(
        IReadOnlyList<Vector3> path,
        SunAvoidanceObstacle obstacle)
    {
        return RoutePathAvoidsSun(
            path,
            obstacle,
            obstacle.BlockingRadius);
    }

    private static bool RoutePathAvoidsSun(
        IReadOnlyList<Vector3> path,
        SunAvoidanceObstacle obstacle)
    {
        return RoutePathAvoidsSun(
            path,
            obstacle,
            obstacle.Radius);
    }

    private static bool RoutePathAvoidsSun(
        IReadOnlyList<Vector3> path,
        SunAvoidanceObstacle obstacle,
        float radius)
    {
        if (!obstacle.HasObstacle ||
            radius <= 0f ||
            path == null ||
            path.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 point =
                path[i];

            if (Vector2.Distance(
                    new Vector2(point.x, point.y),
                    new Vector2(obstacle.Center.x, obstacle.Center.y)) < radius)
            {
                return false;
            }

            if (i == 0)
                continue;

            if (SegmentIntersectsCircle(
                    path[i - 1],
                    point,
                    obstacle.Center,
                    radius))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RoutePathAvoidsSunForWaypoints(
        IReadOnlyList<Vector3> routePath,
        IReadOnlyList<Vector3> travelPath,
        SunAvoidanceObstacle obstacle,
        RouteDestinationCase destinationCase)
    {
        if (PathUsesSunAvoidance(travelPath))
        {
            return RoutePathAvoidsSunBody(
                routePath,
                obstacle);
        }

        if (RouteCaseAllowsDirectSunSafetyExit(destinationCase))
        {
            return RoutePathAvoidsSunBody(
                routePath,
                obstacle);
        }

        return RoutePathAvoidsSun(
            routePath,
            obstacle);
    }

    private static bool RouteCaseAllowsDirectSunSafetyExit(
        RouteDestinationCase destinationCase)
    {
        return IsNearSunStartDestinationCase(destinationCase) ||
               destinationCase == RouteDestinationCase.NearBehind;
    }

    private static bool RouteSegmentIntersectsSunBody(
        Vector3 from,
        Vector3 to,
        SunAvoidanceObstacle obstacle)
    {
        return obstacle.HasObstacle &&
               obstacle.BlockingRadius > 0f &&
               SegmentIntersectsCircle(
                   from,
                   to,
                   obstacle.Center,
                   obstacle.BlockingRadius);
    }

    private static bool SegmentIntersectsCircle(
        Vector3 from,
        Vector3 to,
        Vector3 center,
        float radius)
    {
        Vector2 from2 =
            new Vector2(from.x, from.y);

        Vector2 to2 =
            new Vector2(to.x, to.y);

        Vector2 center2 =
            new Vector2(center.x, center.y);

        Vector2 segment =
            to2 - from2;

        if (segment.sqrMagnitude <= RouteSegmentEpsilon)
            return Vector2.Distance(from2, center2) < radius;

        float projection =
            Vector2.Dot(
                center2 - from2,
                segment) /
            segment.sqrMagnitude;

        projection =
            Mathf.Clamp01(projection);

        Vector2 closestPoint =
            from2 + segment * projection;

        return Vector2.Distance(
            closestPoint,
            center2) < radius;
    }

    private void RebuildTravelRoutePath(
        List<Vector3> routePath,
        Vector3 routeStartPosition,
        Vector3 destinationPosition,
        Vector2 routeStartFacingDirection,
        float turnRadius,
        float speed)
    {
        if (routePath == null)
            return;

        float routeStepDistance =
            GetRoutePlanStepDistance(speed);

        BuildDirectTravelPath(
            routeStartPosition,
            destinationPosition,
            _directTravelPathBuffer);

        SunAvoidanceObstacle obstacle =
            GetSunAvoidanceObstacle(
                routeStartPosition.z,
                destinationPosition);

        RouteDestinationClassification routeClassification =
            ClassifyRouteDestination(
                routeStartPosition,
                destinationPosition,
                routeStartFacingDirection);
        _activeRouteDestinationCase =
            routeClassification.DestinationCase;

        if (routeClassification.DestinationCase ==
            RouteDestinationCase.ForbiddenDestination)
        {
            routePath.Clear();
            routePath.Add(routeStartPosition);
            return;
        }

        bool directTravelPathNeedsSunAvoidance =
            RouteCaseUsesSunAvoidance(
                routeClassification.DestinationCase);

        if (TryRebuildRoutePathFromWaypoints(
                routePath,
                _directTravelPathBuffer,
                routeStartFacingDirection,
                turnRadius,
                speed,
                routeStepDistance,
                "RebuildTravelRoutePath.Direct",
                obstacle) &&
            RoutePathAvoidsSunForWaypoints(
                routePath,
                _directTravelPathBuffer,
                obstacle,
                routeClassification.DestinationCase))
        {
            return;
        }

        if (!directTravelPathNeedsSunAvoidance)
            return;

        BuildSunAdjustmentTravelPath(
            routeStartPosition,
            destinationPosition,
            _travelPathBuffer,
            routeStartFacingDirection,
            turnRadius,
            obstacle,
            routeClassification.DestinationCase);

        if (IsNearSunStartTowardSunDestinationCase(
                routeClassification.DestinationCase) &&
            TryRebuildNearSunStartTowardTangentRoutePath(
                routePath,
                _travelPathBuffer,
                routeStartFacingDirection,
                turnRadius,
                speed,
                routeStepDistance,
                obstacle))
        {
            return;
        }

        TryRebuildRoutePathFromWaypoints(
            routePath,
            _travelPathBuffer,
            routeStartFacingDirection,
            turnRadius,
            speed,
            routeStepDistance,
            "RebuildTravelRoutePath.SunAvoidance",
            obstacle);
    }

    private bool TryRebuildRoutePathFromWaypoints(
        List<Vector3> routePath,
        IReadOnlyList<Vector3> travelPath,
        Vector2 routeStartFacingDirection,
        float turnRadius,
        float speed,
        float routeStepDistance,
        string logPhase,
        SunAvoidanceObstacle obstacle)
    {
        if (routePath == null)
            return false;

        float travelPathLength =
            GetPathLength(travelPath);

        int maxSteps =
            GetRoutePlanMaxSteps(
                travelPath,
                travelPathLength,
                turnRadius,
                routeStepDistance);

        float intermediateWaypointArrivalDistanceThreshold =
            GetIntermediateWaypointArrivalDistanceThreshold(
                travelPath,
                routeStepDistance);

        bool routeBuilt =
            TurnRadiusRouteMath2A.TryBuildWaypointPreviewPath(
                routePath,
                travelPath,
                routeStartFacingDirection,
                routeStepDistance,
                turnRadius,
                ArrivalDistanceThreshold,
                maxSteps,
                intermediateWaypointArrivalDistanceThreshold);

        float routeLength =
            GetPathLength(routePath);

        float maxAllowedRouteLength =
            GetMaxAllowedRouteLength(
                travelPath,
                travelPathLength,
                turnRadius,
                routeStepDistance,
                routeStartFacingDirection);

        if (routeBuilt &&
            maxAllowedRouteLength > 0f &&
            routeLength > maxAllowedRouteLength)
        {
            routeBuilt = false;
        }

        bool routeAvoidsSun =
            !routeBuilt ||
            RoutePathAvoidsSunForWaypoints(
                routePath,
                travelPath,
                obstacle,
                _activeRouteDestinationCase);

        if (routeBuilt &&
            !routeAvoidsSun)
        {
            routeBuilt = false;
        }

        LogMapPointRouteBuildTrace(
            logPhase,
            travelPath != null && travelPath.Count > 0
                ? travelPath[0]
                : Vector3.zero,
            travelPath != null && travelPath.Count > 0
                ? travelPath[travelPath.Count - 1]
                : Vector3.zero,
            routeStartFacingDirection,
            turnRadius,
            speed,
            routeStepDistance,
            maxSteps,
            intermediateWaypointArrivalDistanceThreshold,
            maxAllowedRouteLength,
            routeAvoidsSun,
            routeBuilt,
            travelPath,
            routePath);

        if (!routeBuilt)
        {
            routePath.Clear();
            if (travelPath != null && travelPath.Count > 0)
                routePath.Add(travelPath[0]);
        }

        return routeBuilt;
    }

    private bool TryRebuildNearSunStartTowardTangentRoutePath(
        List<Vector3> routePath,
        IReadOnlyList<Vector3> travelPath,
        Vector2 routeStartFacingDirection,
        float turnRadius,
        float speed,
        float routeStepDistance,
        SunAvoidanceObstacle obstacle)
    {
        if (routePath == null ||
            travelPath == null ||
            travelPath.Count < 3)
        {
            return false;
        }

        routePath.Clear();

        Vector3 routeStart =
            travelPath[0];

        Vector3 tangentPoint =
            travelPath[1];

        Vector3 destination =
            travelPath[travelPath.Count - 1];

        _routeSegmentWaypointsBuffer.Clear();
        _routeSegmentWaypointsBuffer.Add(routeStart);
        _routeSegmentWaypointsBuffer.Add(tangentPoint);

        bool firstBuilt =
            TryRebuildRoutePathFromWaypoints(
                routePath,
                _routeSegmentWaypointsBuffer,
                routeStartFacingDirection,
                turnRadius,
                speed,
                routeStepDistance,
                "RebuildTravelRoutePath.SunStartTangentEscape",
                obstacle);

        if (!firstBuilt ||
            routePath.Count <= 1)
        {
            return false;
        }

        Vector3 continuationStart =
            routePath[routePath.Count - 1];

        Vector2 tangentDirection =
            GetDirectionFromPoints(
                routePath[routePath.Count - 2],
                continuationStart,
                GetDirectionFromPoints(
                    routeStart,
                    tangentPoint,
                    routeStartFacingDirection));

        _routeSegmentWaypointsBuffer.Clear();
        _routeSegmentWaypointsBuffer.Add(continuationStart);
        _routeSegmentWaypointsBuffer.Add(destination);

        bool continuationBuilt =
            TryRebuildRoutePathFromWaypoints(
                _routeSegmentPathBuffer,
                _routeSegmentWaypointsBuffer,
                tangentDirection,
                turnRadius,
                speed,
                routeStepDistance,
                "RebuildTravelRoutePath.SunStartTangentContinuation",
                obstacle);

        if (!continuationBuilt ||
            _routeSegmentPathBuffer.Count <= 1)
        {
            return false;
        }

        for (int i = 1; i < _routeSegmentPathBuffer.Count; i++)
        {
            routePath.Add(_routeSegmentPathBuffer[i]);
        }

        return RoutePathAvoidsSunBody(
            routePath,
            obstacle);
    }

    private static Vector2 GetDirectionFromPoints(
        Vector3 from,
        Vector3 to,
        Vector2 fallbackDirection)
    {
        Vector2 direction =
            new Vector2(
                to.x - from.x,
                to.y - from.y);

        if (direction.sqrMagnitude <= RouteSegmentEpsilon)
        {
            return TurnRadiusRouteMath2A.NormalizeDirectionOrUp(
                fallbackDirection);
        }

        return direction.normalized;
    }

    private float GetRoutePlanStepDistance(
        float speed)
    {
        float planningSpeed =
            GetCurrentShipTravelSpeed();

        if (planningSpeed <= 0f)
            planningSpeed = speed;

        return Mathf.Clamp(
            Mathf.Max(0.01f, planningSpeed) * 0.05f,
            0.5f,
            5f);
    }

    private int GetRoutePlanMaxSteps(
        IReadOnlyList<Vector3> travelPath,
        float travelPathLength,
        float turnRadius,
        float routeStepDistance)
    {
        int maneuverCount =
            Mathf.Max(
                1,
                travelPath != null
                    ? travelPath.Count - 1
                    : 1);

        float maneuverReserveMultiplier =
            PathUsesSunAvoidance(travelPath)
                ? 4f
                : 1f;

        float estimatedRouteLength =
            travelPathLength +
            turnRadius *
            Mathf.PI *
            2f *
            maneuverCount *
            maneuverReserveMultiplier;

        return Mathf.Clamp(
            Mathf.CeilToInt(
                estimatedRouteLength /
                Mathf.Max(0.01f, routeStepDistance)) + maneuverCount + 4,
            1,
            RoutePlanMaxSteps);
    }

    private void LogMapPointRouteBuildTrace(
        string phase,
        Vector3 routeStartPosition,
        Vector3 destinationPosition,
        Vector2 routeStartFacingDirection,
        float turnRadius,
        float speed,
        float routeStepDistance,
        int maxSteps,
        float intermediateWaypointArrivalDistanceThreshold,
        float maxAllowedRouteLength,
        bool routeAvoidsSun,
        bool routeBuilt,
        IReadOnlyList<Vector3> travelPath,
        IReadOnlyList<Vector3> routePath)
    {
        if (!ShouldLogMapPointRouteBuildTrace())
            return;

        LogCustom(
            "[RouteTrace.Build] " +
            phase +
            " | RouteBuildId = " +
            _mapPointRouteBuildId +
            " | RouteCase = " +
            _activeRouteDestinationCase +
            " | Built = " +
            routeBuilt +
            " | RouteStart = " +
            FormatVector3(routeStartPosition) +
            " | Destination = " +
            FormatVector3(destinationPosition) +
            " | StartFacing = " +
            FormatVector2(routeStartFacingDirection) +
            " | TurnRadius = " +
            turnRadius.ToString("0.###") +
            " | Speed = " +
            speed.ToString("0.###") +
            " | StepDistance = " +
            routeStepDistance.ToString("0.###") +
            " | MaxSteps = " +
            maxSteps +
            " | IntermediateThreshold = " +
            intermediateWaypointArrivalDistanceThreshold.ToString("0.###") +
            " | MaxAllowedRouteLength = " +
            maxAllowedRouteLength.ToString("0.###") +
            " | RouteAvoidsSun = " +
            routeAvoidsSun +
            " | TravelPathLength = " +
            GetPathLength(travelPath).ToString("0.###") +
            " | TravelPathPoints = " +
            (travelPath != null ? travelPath.Count : 0) +
            " | RouteLength = " +
            GetPathLength(routePath).ToString("0.###") +
            " | RoutePoints = " +
            (routePath != null ? routePath.Count : 0) +
            " | TravelPathSample = " +
            FormatPathSample(travelPath, 8) +
            " | FirstRoutePoints = " +
            FormatPathSample(routePath, 8) +
            " | LastRoutePoints = " +
            FormatPathTail(routePath, 8));
    }

    private void LogMapPointSunStartTangentEscapeTrace(
        Vector2 tangentDirection,
        float tangentDistance,
        IReadOnlyList<Vector3> travelPath)
    {
        if (!ShouldLogMapPointRouteBuildTrace())
            return;

        LogCustom(
            "[RouteTrace.SunStartTangentEscape] " +
            "RouteBuildId = " +
            _mapPointRouteBuildId +
            " | RouteCase = " +
            _activeRouteDestinationCase +
            " | TangentDirection = " +
            FormatVector2(tangentDirection) +
            " | TangentDistance = " +
            tangentDistance.ToString("0.###") +
            " | TravelPathLength = " +
            GetPathLength(travelPath).ToString("0.###") +
            " | TravelPathPoints = " +
            (travelPath != null ? travelPath.Count : 0) +
            " | TravelPathSample = " +
            FormatPathSample(travelPath, 8));
    }

    private static string FormatPathSample(
        IReadOnlyList<Vector3> path,
        int maxPoints)
    {
        if (path == null || path.Count == 0)
            return "[]";

        int count =
            Mathf.Min(
                Mathf.Max(1, maxPoints),
                path.Count);

        string result =
            "[";

        for (int i = 0; i < count; i++)
        {
            if (i > 0)
                result += " -> ";

            result += FormatVector3(path[i]);
        }

        if (path.Count > count)
            result += " -> ...";

        result += "]";

        return result;
    }

    private static string FormatPathTail(
        IReadOnlyList<Vector3> path,
        int maxPoints)
    {
        if (path == null || path.Count == 0)
            return "[]";

        int count =
            Mathf.Min(
                Mathf.Max(1, maxPoints),
                path.Count);

        int startIndex =
            Mathf.Max(
                0,
                path.Count - count);

        string result =
            "[";

        if (startIndex > 0)
            result += "... -> ";

        for (int i = startIndex; i < path.Count; i++)
        {
            if (i > startIndex)
                result += " -> ";

            result += FormatVector3(path[i]);
        }

        result += "]";

        return result;
    }

    private float GetSunWorldSize(SunConfig sun)
    {
        if (_configService != null &&
            _configService.SystemVisualConfig != null)
        {
            return _configService
                .SystemVisualConfig
                .GetSunWorldSize(sun);
        }

        return sun != null
            ? sun.VisualSize
            : 0f;
    }

    private Vector3 CalculateNextTravelPositionOnActiveRoute(
        float movementDistance,
        out bool destinationReached)
    {
        destinationReached = false;

        if (_activeTravelRoutePath == null ||
            _activeTravelRoutePath.Count <= 1)
        {
            return State.GetCurrentPosition();
        }

        float totalPathLength =
            GetPathLength(_activeTravelRoutePath);

        if (totalPathLength <= ArrivalDistanceThreshold)
        {
            destinationReached = true;
            return _activeTravelRoutePath[_activeTravelRoutePath.Count - 1];
        }

        _activeRouteDistanceTravelled =
            Mathf.Clamp(
                _activeRouteDistanceTravelled,
                0f,
                totalPathLength);

        float nextDistanceOnRoute =
            Mathf.Clamp(
                _activeRouteDistanceTravelled +
                Mathf.Max(0f, movementDistance),
                0f,
                totalPathLength);

        Vector3 nextPosition =
            GetPointOnPathAtDistance(
                _activeTravelRoutePath,
                nextDistanceOnRoute);

        Vector2 routeDirection =
            GetDirectionOnPathAtDistance(
                _activeTravelRoutePath,
                nextDistanceOnRoute);

        if (routeDirection.sqrMagnitude > 0.0001f)
        {
            _travelFacingDirection =
                TurnRadiusRouteMath2A.NormalizeDirectionOrUp(
                    routeDirection);
        }

        _activeRouteDistanceTravelled =
            nextDistanceOnRoute;

        if (totalPathLength - nextDistanceOnRoute <=
            ArrivalDistanceThreshold)
        {
            destinationReached = true;
            return _activeTravelRoutePath[_activeTravelRoutePath.Count - 1];
        }

        return nextPosition;
    }

    private void SyncShipMovementStateWithTravel(
        Vector3 position,
        float speed)
    {
        if (_shipMovementService == null ||
            _shipMovementService.State == null)
        {
            return;
        }

        _shipMovementService.State.SetRouteMotion(
            new Vector2(
                position.x,
                position.y),
            _travelFacingDirection,
            speed);
    }

    private Vector3 GetCurrentActiveRouteDestinationPosition()
    {
        if (_activeTravelRoutePath != null &&
            _activeTravelRoutePath.Count > 0)
        {
            return _activeTravelRoutePath[_activeTravelRoutePath.Count - 1];
        }

        return GetCurrentDestinationPosition();
    }

    private bool HasActiveTravelRoute()
    {
        return _activeTravelRoutePath != null &&
               _activeTravelRoutePath.Count > 1 &&
               State.TravelDistance > ArrivalDistanceThreshold;
    }

    private void ApplyShortRouteSpeedAdjustment()
    {
        int maxAdjustmentSteps =
            Mathf.CeilToInt(
                1f /
                GetRouteSpeedAdjustmentStepFactor()) + 1;

        for (int i = 0; i < maxAdjustmentSteps; i++)
        {
            float adjustedFactor =
                ResolveShortRouteSpeedAdjustmentFactor(
                    _routeSpeedAdjustmentFactor,
                    _activeTravelRoutePath);

            if (Mathf.Approximately(
                    adjustedFactor,
                    _routeSpeedAdjustmentFactor))
            {
                return;
            }

            _routeSpeedAdjustmentFactor =
                adjustedFactor;

            RebuildTravelRoutePath(
                _activeTravelRoutePath,
                State.StartPosition,
                State.DestinationPosition,
                _routePreviewStartFacingDirection,
                GetAdjustedShipTurnRadius(_routeTurnAdjustmentFactor),
                GetCurrentEffectiveTravelSpeed());
        }
    }

    private float ResolveShortRouteSpeedAdjustmentFactor(
        float currentFactor,
        IReadOnlyList<Vector3> routePath)
    {
        float baseSpeed =
            GetCurrentShipTravelSpeed();

        float routeLength =
            GetPathLength(routePath);

        if (baseSpeed <= 0f ||
            routeLength <= ArrivalDistanceThreshold)
        {
            LogMapPointShortRouteTrace(
                "Skip",
                currentFactor,
                currentFactor,
                routeLength,
                0f);
            return currentFactor;
        }

        float secondsPerTick =
            GameTimeState.SecondsPerDay > 0f
                ? GameTimeState.SecondsPerDay
                : 1f;

        float maxDistancePerTick =
            Mathf.Max(
                ArrivalDistanceThreshold,
                routeLength);

        float maxFactor =
            Mathf.Clamp01(
                maxDistancePerTick /
                (baseSpeed * secondsPerTick));

        if (currentFactor <= maxFactor)
        {
            LogMapPointShortRouteTrace(
                "AlreadyWithinLimit",
                currentFactor,
                currentFactor,
                routeLength,
                maxFactor);
            return currentFactor;
        }

        float stepFactor =
            GetRouteSpeedAdjustmentStepFactor();

        float minFactor =
            GetMinRouteTurnRadiusAdjustmentFactor();

        float adjustedFactor =
            currentFactor;

        while (adjustedFactor - stepFactor > maxFactor &&
               adjustedFactor - stepFactor >= minFactor)
        {
            adjustedFactor -= stepFactor;
        }

        if (adjustedFactor > maxFactor)
        {
            adjustedFactor =
                Mathf.Max(
                    minFactor,
                    maxFactor);
        }

        adjustedFactor =
            Mathf.Clamp01(adjustedFactor);

        LogMapPointShortRouteTrace(
            "Adjusted",
            currentFactor,
            adjustedFactor,
            routeLength,
            maxFactor);

        return adjustedFactor;
    }

    private void LogMapPointShortRouteTrace(
        string phase,
        float currentFactor,
        float adjustedFactor,
        float routeLength,
        float maxFactor)
    {
        if (!ShouldLogMapPointRouteBuildTrace())
            return;

        LogCustom(
            "[RouteTrace.ShortRoute] " +
            phase +
            " | RouteBuildId = " +
            _mapPointRouteBuildId +
            " | RouteCase = " +
            _activeRouteDestinationCase +
            " | CurrentFactor = " +
            currentFactor.ToString("0.###") +
            " | AdjustedFactor = " +
            adjustedFactor.ToString("0.###") +
            " | MaxFactor = " +
            maxFactor.ToString("0.###") +
            " | RouteLength = " +
            routeLength.ToString("0.###") +
            " | BaseSpeed = " +
            GetCurrentShipTravelSpeed().ToString("0.###") +
            " | EffectiveSpeed = " +
            (GetCurrentShipTravelSpeed() *
             Mathf.Clamp01(adjustedFactor)).ToString("0.###"));
    }

    private static bool PathUsesSunAvoidance(
        IReadOnlyList<Vector3> path)
    {
        return path != null &&
               path.Count > 2;
    }

    private static bool RouteCaseUsesSunAvoidance(
        RouteDestinationCase destinationCase)
    {
        return IsNearSunStartDestinationCase(destinationCase) ||
               destinationCase == RouteDestinationCase.NearSunDestination ||
               destinationCase == RouteDestinationCase.SunBlocked;
    }

    private static bool IsNearSunStartDestinationCase(
        RouteDestinationCase destinationCase)
    {
        return destinationCase == RouteDestinationCase.NearSunStartAwayForward ||
               destinationCase == RouteDestinationCase.NearSunStartAwaySide ||
               destinationCase == RouteDestinationCase.NearSunStartAwayBehind ||
               destinationCase == RouteDestinationCase.NearSunStartTangentForward ||
               destinationCase == RouteDestinationCase.NearSunStartTangentSideAway ||
               destinationCase == RouteDestinationCase.NearSunStartTangentSideToward ||
               destinationCase == RouteDestinationCase.NearSunStartTangentBehind ||
               destinationCase == RouteDestinationCase.NearSunStartTowardForward ||
               destinationCase == RouteDestinationCase.NearSunStartTowardSide ||
               destinationCase == RouteDestinationCase.NearSunStartTowardBehind ||
               destinationCase == RouteDestinationCase.NearSunStartExit ||
               destinationCase == RouteDestinationCase.NearSunStart;
    }

    private RouteAdjustment ResolveRouteAdjustment(
        Vector3 routeStartPosition,
        Vector3 destinationPosition,
        Vector2 routeStartFacingDirection)
    {
        float turnRadius =
            GetCurrentShipTurnRadius();

        if (turnRadius <= 0f)
        {
            LogMapPointAdjustmentTrace(
                "NoTurnRadius",
                1f,
                1f,
                true,
                0f,
                0f,
                0f,
                0f,
                true);
            return new RouteAdjustment(1f, 1f);
        }

        BuildCurrentTravelPath(
            routeStartPosition,
            destinationPosition,
            _routePreviewPathBuffer);

        BuildDirectTravelPath(
            routeStartPosition,
            destinationPosition,
            _directTravelPathBuffer);

        SunAvoidanceObstacle obstacle =
            GetSunAvoidanceObstacle(
                routeStartPosition.z,
                destinationPosition);

        RouteDestinationClassification routeClassification =
            ClassifyRouteDestination(
                routeStartPosition,
                destinationPosition,
                routeStartFacingDirection);
        _activeRouteDestinationCase =
            routeClassification.DestinationCase;

        float distanceToDestination =
            GetPathLength(_routePreviewPathBuffer);

        bool requiresSunAvoidance =
            RouteCaseUsesSunAvoidance(
                routeClassification.DestinationCase);

        float turnStepFactor =
            GetRouteTurnRadiusAdjustmentStepFactor();

        float speedStepFactor =
            GetRouteSpeedAdjustmentStepFactor();

        float minFactor =
            GetMinRouteTurnRadiusAdjustmentFactor();

        float turnAdjustmentFactor =
            1f;

        float speedAdjustmentFactor =
            1f;

        if (ShouldStartNearSunCaseWithMinimumTurnRadius(
                routeClassification.DestinationCase))
        {
            turnAdjustmentFactor =
                minFactor;

            LogMapPointAdjustmentTrace(
                "NearSunStartMinimumTurnRadius",
                turnAdjustmentFactor,
                speedAdjustmentFactor,
                false,
                distanceToDestination,
                turnRadius * turnAdjustmentFactor,
                0f,
                0f,
                false);
        }

        if (IsNearSunStartFacingSun(
                routeClassification,
                routeStartPosition,
                routeStartFacingDirection))
        {
            turnAdjustmentFactor =
                minFactor;

            LogMapPointAdjustmentTrace(
                "NearSunStartFacingSunEscape",
                turnAdjustmentFactor,
                speedAdjustmentFactor,
                false,
                distanceToDestination,
                turnRadius * turnAdjustmentFactor,
                0f,
                0f,
                false);
        }

        while (turnAdjustmentFactor >= minFactor - 0.0001f)
        {
            turnAdjustmentFactor =
                Mathf.Max(
                    minFactor,
                    turnAdjustmentFactor);

            float probeRouteLength;
            float maxAllowedRouteLength;
            bool canReach =
                CanReachRouteWithTurnRadius(
                    _directTravelPathBuffer,
                    routeStartFacingDirection,
                    turnRadius * turnAdjustmentFactor,
                    speedAdjustmentFactor,
                    out probeRouteLength,
                    out maxAllowedRouteLength);

            bool directRouteAvoidsSun =
                RoutePathAvoidsSunForWaypoints(
                    canReach
                        ? _routeProbePathBuffer
                        : _directTravelPathBuffer,
                    _directTravelPathBuffer,
                    obstacle,
                    routeClassification.DestinationCase);

            if (canReach &&
                !directRouteAvoidsSun)
            {
                canReach = false;
            }

            LogMapPointAdjustmentTrace(
                "ProbeDirect",
                turnAdjustmentFactor,
                speedAdjustmentFactor,
                canReach,
                GetPathLength(_directTravelPathBuffer),
                turnRadius * turnAdjustmentFactor,
                probeRouteLength,
                maxAllowedRouteLength,
                directRouteAvoidsSun);

            if (canReach)
            {
                return new RouteAdjustment(
                    turnAdjustmentFactor,
                    speedAdjustmentFactor);
            }

            if (!requiresSunAvoidance)
            {
                LogMapPointAdjustmentTrace(
                    "SkipSunAvoidanceNotRequired",
                    turnAdjustmentFactor,
                    speedAdjustmentFactor,
                    false,
                    GetPathLength(_directTravelPathBuffer),
                    turnRadius * turnAdjustmentFactor,
                    probeRouteLength,
                    maxAllowedRouteLength,
                    true);

                turnAdjustmentFactor -= turnStepFactor;
                speedAdjustmentFactor =
                    Mathf.Max(
                        minFactor,
                        speedAdjustmentFactor - speedStepFactor);

                if (Mathf.Approximately(
                        turnAdjustmentFactor,
                        minFactor))
                {
                    turnAdjustmentFactor =
                        minFactor;
                }

                continue;
            }

            BuildSunAdjustmentTravelPath(
                routeStartPosition,
                destinationPosition,
                _routePreviewPathBuffer,
                routeStartFacingDirection,
                turnRadius * turnAdjustmentFactor,
                obstacle,
                routeClassification.DestinationCase);

            distanceToDestination =
                GetPathLength(_routePreviewPathBuffer);

            if (IsNearSunStartTowardSunDestinationCase(
                    routeClassification.DestinationCase))
            {
                canReach =
                    CanReachNearSunStartTowardTangentRoute(
                        _routePreviewPathBuffer,
                        routeStartFacingDirection,
                        turnRadius * turnAdjustmentFactor,
                        speedAdjustmentFactor,
                        obstacle,
                        out probeRouteLength,
                        out maxAllowedRouteLength);
            }
            else
            {
                canReach =
                    CanReachRouteWithTurnRadius(
                        _routePreviewPathBuffer,
                        routeStartFacingDirection,
                        turnRadius * turnAdjustmentFactor,
                        speedAdjustmentFactor,
                        out probeRouteLength,
                        out maxAllowedRouteLength);
            }

            bool sunAvoidanceRouteAvoidsSun =
                !canReach ||
                RoutePathAvoidsSunBody(
                    _routeProbePathBuffer,
                    obstacle);

            if (canReach &&
                !sunAvoidanceRouteAvoidsSun)
            {
                canReach = false;
            }

            LogMapPointAdjustmentTrace(
                "ProbeSunAvoidance",
                turnAdjustmentFactor,
                speedAdjustmentFactor,
                canReach,
                distanceToDestination,
                turnRadius * turnAdjustmentFactor,
                probeRouteLength,
                maxAllowedRouteLength,
                sunAvoidanceRouteAvoidsSun);

            if (canReach)
            {
                return new RouteAdjustment(
                    turnAdjustmentFactor,
                    speedAdjustmentFactor);
            }

            turnAdjustmentFactor -= turnStepFactor;
            speedAdjustmentFactor =
                Mathf.Max(
                    minFactor,
                    speedAdjustmentFactor - speedStepFactor);

            if (Mathf.Approximately(
                    turnAdjustmentFactor,
                    minFactor))
            {
                turnAdjustmentFactor =
                    minFactor;
            }
        }

        return new RouteAdjustment(
            minFactor,
            speedAdjustmentFactor);
    }

    private void LogMapPointAdjustmentTrace(
        string phase,
        float turnAdjustmentFactor,
        float speedAdjustmentFactor,
        bool canReach,
        float pathLength,
        float turnRadius,
        float routeLength,
        float maxAllowedRouteLength,
        bool routeAvoidsSun)
    {
        if (!ShouldLogMapPointRouteBuildTrace())
            return;

        LogCustom(
            "[RouteTrace.Adjust] " +
            phase +
            " | RouteBuildId = " +
            _mapPointRouteBuildId +
            " | RouteCase = " +
            _activeRouteDestinationCase +
            " | Factor = " +
            turnAdjustmentFactor.ToString("0.###") +
            " | SpeedFactor = " +
            speedAdjustmentFactor.ToString("0.###") +
            " | CanReach = " +
            canReach +
            " | PathLength = " +
            pathLength.ToString("0.###") +
            " | TurnRadius = " +
            turnRadius.ToString("0.###") +
            " | ProbeRouteLength = " +
            routeLength.ToString("0.###") +
            " | MaxAllowedRouteLength = " +
            maxAllowedRouteLength.ToString("0.###") +
            " | RouteAvoidsSun = " +
            routeAvoidsSun +
            " | EffectiveSpeed = " +
            (GetCurrentShipTravelSpeed() *
             Mathf.Clamp01(speedAdjustmentFactor)).ToString("0.###"));
    }

    private bool CanReachRouteWithTurnRadius(
        IReadOnlyList<Vector3> waypoints,
        Vector2 startFacingDirection,
        float turnRadius,
        float speedFactor,
        out float routeLength,
        out float maxAllowedRouteLength)
    {
        routeLength = 0f;
        maxAllowedRouteLength = 0f;

        if (waypoints == null ||
            waypoints.Count <= 1)
        {
            return true;
        }

        float speed =
            GetCurrentShipTravelSpeed() *
            Mathf.Clamp01(speedFactor);

        float routeStepDistance =
            GetRoutePlanStepDistance(speed);

        float travelPathLength =
            GetPathLength(waypoints);

        int maxSteps =
            GetRoutePlanMaxSteps(
                waypoints,
                travelPathLength,
                turnRadius,
                routeStepDistance);

        bool routeBuilt =
            TurnRadiusRouteMath2A.TryBuildWaypointPreviewPath(
                _routeProbePathBuffer,
                waypoints,
                startFacingDirection,
                routeStepDistance,
                turnRadius,
                ArrivalDistanceThreshold,
                maxSteps,
                GetIntermediateWaypointArrivalDistanceThreshold(
                    waypoints,
                    routeStepDistance));

        routeLength =
            GetPathLength(_routeProbePathBuffer);

        maxAllowedRouteLength =
            GetMaxAllowedRouteLength(
                waypoints,
                travelPathLength,
                turnRadius,
                routeStepDistance,
                startFacingDirection);

        if (!routeBuilt)
            return false;

        if (maxAllowedRouteLength > 0f &&
            routeLength > maxAllowedRouteLength)
        {
            return false;
        }

        return true;
    }

    private bool CanReachNearSunStartTowardTangentRoute(
        IReadOnlyList<Vector3> waypoints,
        Vector2 startFacingDirection,
        float turnRadius,
        float speedFactor,
        SunAvoidanceObstacle obstacle,
        out float routeLength,
        out float maxAllowedRouteLength)
    {
        routeLength = 0f;
        maxAllowedRouteLength = 0f;

        if (waypoints == null ||
            waypoints.Count < 3)
        {
            return CanReachRouteWithTurnRadius(
                waypoints,
                startFacingDirection,
                turnRadius,
                speedFactor,
                out routeLength,
                out maxAllowedRouteLength);
        }

        float speed =
            GetCurrentShipTravelSpeed() *
            Mathf.Clamp01(speedFactor);

        float routeStepDistance =
            GetRoutePlanStepDistance(speed);

        bool routeBuilt =
            TryRebuildNearSunStartTowardTangentRoutePath(
                _routeProbePathBuffer,
                waypoints,
                startFacingDirection,
                turnRadius,
                speed,
                routeStepDistance,
                obstacle);

        routeLength =
            GetPathLength(_routeProbePathBuffer);

        float travelPathLength =
            GetPathLength(waypoints);

        maxAllowedRouteLength =
            GetMaxAllowedRouteLength(
                waypoints,
                travelPathLength,
                turnRadius,
                routeStepDistance,
                startFacingDirection);

        if (!routeBuilt)
            return false;

        if (maxAllowedRouteLength > 0f &&
            routeLength > maxAllowedRouteLength)
        {
            return false;
        }

        return RoutePathAvoidsSunBody(
            _routeProbePathBuffer,
            obstacle);
    }

    private float GetMaxAllowedRouteLength(
        IReadOnlyList<Vector3> waypoints,
        float travelPathLength,
        float turnRadius,
        float routeStepDistance,
        Vector2 startFacingDirection)
    {
        float turnReserveLength =
            GetTurnReserveLength(
                waypoints,
                startFacingDirection,
                turnRadius);

        if (!PathUsesSunAvoidance(waypoints))
        {
            return travelPathLength +
                   turnReserveLength *
                   1.35f +
                   routeStepDistance * 12f;
        }

        return travelPathLength +
               turnReserveLength *
               SunAvoidanceTurnRouteReserveMultiplier +
               routeStepDistance *
               Mathf.Max(1, waypoints.Count + 4);
    }

    private float GetTurnReserveLength(
        IReadOnlyList<Vector3> waypoints,
        Vector2 startFacingDirection,
        float turnRadius)
    {
        if (waypoints == null ||
            waypoints.Count <= 1 ||
            turnRadius <= 0f)
        {
            return 0f;
        }

        Vector2 facingDirection =
            TurnRadiusRouteMath2A.NormalizeDirectionOrUp(startFacingDirection);

        float turnReserveLength =
            0f;

        for (int i = 1; i < waypoints.Count; i++)
        {
            Vector3 segment3 =
                waypoints[i] - waypoints[i - 1];

            Vector2 segment =
                new Vector2(
                    segment3.x,
                    segment3.y);

            if (segment.sqrMagnitude <= RouteSegmentEpsilon)
                continue;

            Vector2 segmentDirection =
                segment.normalized;

            turnReserveLength +=
                Vector2.Angle(
                    facingDirection,
                    segmentDirection) *
                Mathf.Deg2Rad *
                turnRadius;

            facingDirection =
                segmentDirection;
        }

        return turnReserveLength;
    }

    private float GetIntermediateWaypointArrivalDistanceThreshold(
        IReadOnlyList<Vector3> travelPath,
        float routeStepDistance)
    {
        if (!PathUsesSunAvoidance(travelPath))
            return ArrivalDistanceThreshold;

        return Mathf.Max(
            ArrivalDistanceThreshold,
            routeStepDistance * 2f);
    }

    private float GetAdjustedShipTurnRadius(
        float adjustmentFactor)
    {
        return GetCurrentShipTurnRadius() *
               Mathf.Clamp01(adjustmentFactor);
    }

    private float GetRouteTurnRadiusAdjustmentStepFactor()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        if (config == null)
            return 0.05f;

        return Mathf.Clamp(
            config.RouteTurnRadiusAdjustmentStepPercent * 0.01f,
            0.001f,
            0.5f);
    }

    private float GetRouteSpeedAdjustmentStepFactor()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        if (config == null)
            return 0.025f;

        return Mathf.Clamp(
            config.RouteSpeedAdjustmentStepPercent * 0.01f,
            0.001f,
            0.5f);
    }

    private float GetMinRouteTurnRadiusAdjustmentFactor()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        if (config == null)
            return 0.05f;

        return Mathf.Clamp(
            config.MinRouteTurnRadiusAdjustmentFactor,
            0.01f,
            1f);
    }

    private float GetRouteNearDistanceThreshold(
        float turnRadius)
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        float multiplier =
            config != null
                ? config.RouteNearDistanceTurnRadiusMultiplier
                : 0.75f;

        return Mathf.Max(
            ArrivalDistanceThreshold,
            Mathf.Max(0f, turnRadius) * multiplier);
    }

    private float GetRouteForwardSectorAngleDegrees()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        return config != null
            ? config.RouteForwardSectorAngleDegrees
            : 60f;
    }

    private float GetRouteBehindSectorAngleDegrees()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        float forwardAngle =
            GetRouteForwardSectorAngleDegrees();

        float behindAngle =
            config != null
                ? config.RouteBehindSectorAngleDegrees
                : 135f;

        return Mathf.Clamp(
            behindAngle,
            forwardAngle + 1f,
            179f);
    }

    private Vector2 GetCurrentShipFacingDirection()
    {
        if (_shipMovementService != null &&
            _shipMovementService.State != null)
        {
            return TurnRadiusRouteMath2A.NormalizeDirectionOrUp(
                _shipMovementService.State.FacingDirection);
        }

        return TurnRadiusRouteMath2A.NormalizeDirectionOrUp(
            _travelFacingDirection);
    }

    private Vector3 GetCurrentShipPosition()
    {
        if (_shipMovementService != null &&
            _shipMovementService.State != null)
        {
            Vector2 movementPosition =
                _shipMovementService.State.Position;

            return new Vector3(
                movementPosition.x,
                movementPosition.y,
                State.GetCurrentPosition().z);
        }

        return State.GetCurrentPosition();
    }

    private float GetCurrentShipTurnRadius()
    {
        AllyConfig activeShipConfig =
            _hangarService != null
                ? _hangarService.GetActiveShipData()
                : null;

        if (activeShipConfig == null)
            return 0f;

        if (_shipStatsService == null)
            return Mathf.Max(0f, activeShipConfig.TurnRadius);

        ShipRuntimeData activeShipState =
            _hangarService.GetActiveShipState();

        List<ModuleConfig> equippedModules =
            new List<ModuleConfig>();

        if (activeShipState != null &&
            activeShipState.EquippedModuleIds != null)
        {
            for (int i = 0;
                 i < activeShipState.EquippedModuleIds.Count;
                 i++)
            {
                string moduleId =
                    activeShipState.EquippedModuleIds[i];

                if (string.IsNullOrWhiteSpace(moduleId))
                    continue;

                ModuleConfig moduleConfig =
                    _configService.GetModuleConfigById(moduleId);

                if (moduleConfig != null)
                    equippedModules.Add(moduleConfig);
            }
        }

        ShipFinalStats activeShipStats =
            _shipStatsService.Calculate(
                activeShipConfig,
                equippedModules);

        if (activeShipStats != null)
            return Mathf.Max(0f, activeShipStats.TurnRadius);

        return 0f;
    }

    private void RefreshNpcDestinationAtTickStart(
        int quantTick,
        bool force = false)
    {
        if (State == null ||
            State.Destination == null ||
            State.Destination.Type != TravelDestinationType.Npc)
        {
            return;
        }

        if (State.Status == SystemTravelStatus.Flying)
            return;

        if (!force &&
            _lastNpcDestinationRefreshTick == quantTick)
            return;

        string runtimeNpcId = State.Destination.RuntimeNpcId;

        if (!TryGetNpcDestinationPosition(
                runtimeNpcId,
                out Vector3 npcPosition))
        {
            CancelTravel();
            return;
        }

        State.DestinationPosition = npcPosition;
        State.Destination.FixedMapPosition = npcPosition;

        _lastNpcDestinationRefreshTick = quantTick;
    }

    private bool IsNpcDestination()
    {
        return State != null &&
               State.Destination != null &&
               State.Destination.Type == TravelDestinationType.Npc;
    }

    private bool IsMapPointDestination()
    {
        return State != null &&
               State.Destination != null &&
               State.Destination.Type == TravelDestinationType.MapPoint;
    }

    private bool ShouldLogMapPointRouteBuildTrace()
    {
        return IsMapPointDestination() &&
               _isMapPointRouteBuildTraceEnabled;
    }

    private void PublishTravelProgress(float progress01)
    {
        State.TravelProgress01 = Mathf.Clamp01(progress01);

        if (_gameSessionService != null &&
            _gameSessionService.State != null &&
            _gameSessionService.State.Player != null)
        {
            _gameSessionService.State.Player.SystemMapShipPosition =
                State.GetCurrentPosition();
        }

        _eventBus.Publish(new SystemTravelProgressChangedEvent(
            State.GetCurrentPosition(),
            State.DestinationPosition,
            State.TravelProgress01
        ));
    }

    private bool TryGetNpcDestinationPosition(
        string runtimeNpcId,
        out Vector3 position)
    {
        position = Vector3.zero;

        if (string.IsNullOrWhiteSpace(runtimeNpcId))
            return false;

        if (!TryResolveNpcRuntimeService())
            return false;

        if (!_npcRuntimeService.TryGetNpc(
                runtimeNpcId,
                out SystemNpcRuntimeState npc))
        {
            return false;
        }

        if (npc == null ||
            !npc.IsAlive ||
            npc.IsOnPlanet)
        {
            return false;
        }

        position = npc.CurrentPosition;
        position.z = -2f;
        return true;
    }

    private bool TryResolveNpcRuntimeService()
    {
        if (_npcRuntimeService != null)
            return true;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return false;
        }

        return Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _npcRuntimeService);
    }

    private float GetPathLength(IReadOnlyList<Vector3> path)
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

            if (segmentDistance <= RouteSegmentEpsilon)
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

    private Vector2 GetDirectionOnPathAtDistance(
        IReadOnlyList<Vector3> path,
        float distance)
    {
        if (path == null || path.Count <= 1)
            return Vector2.zero;

        float remainingDistance =
            Mathf.Max(0f, distance);

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 from =
                path[i - 1];

            Vector3 to =
                path[i];

            Vector3 segment =
                to - from;

            float segmentDistance =
                segment.magnitude;

            if (segmentDistance <= RouteSegmentEpsilon)
                continue;

            if (remainingDistance <= segmentDistance)
            {
                return new Vector2(
                    segment.x,
                    segment.y).normalized;
            }

            remainingDistance -= segmentDistance;
        }

        Vector3 finalSegment =
            path[path.Count - 1] -
            path[path.Count - 2];

        return new Vector2(
            finalSegment.x,
            finalSegment.y).normalized;
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
