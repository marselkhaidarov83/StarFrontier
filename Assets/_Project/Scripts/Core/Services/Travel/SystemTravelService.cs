using System.Collections.Generic;
using UnityEngine;

public sealed class SystemTravelService : CustomService, ISystemTravelService
{
    private const int NpcFollowModeCount = 3;
    private const float NpcFollowWeaponRangeFactor = 0.9f;
    private const float NpcFollowDefaultMinDistance = 100f;
    private const float NpcFollowDefaultMaxDistance = 300f;

    private int _npcFollowModeIndex;

    public int NpcFollowModeIndex => Mathf.Clamp(
        _npcFollowModeIndex,
        0,
        NpcFollowModeCount - 1);

    public int NpcFollowModeNumber => NpcFollowModeIndex + 1;
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
            float speedFactor,
            float maxAllowedRouteLength = 0f,
            bool hasBuiltRoute = false)
        {
            TurnFactor = Mathf.Clamp01(turnFactor);
            SpeedFactor = Mathf.Clamp01(speedFactor);
            MaxAllowedRouteLength = Mathf.Max(0f, maxAllowedRouteLength);
            HasBuiltRoute = hasBuiltRoute;
        }

        public float TurnFactor { get; }
        public float SpeedFactor { get; }
        public float MaxAllowedRouteLength { get; }
        public bool HasBuiltRoute { get; }
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

        SunBlockedNearForward,
        SunBlockedNearSide,
        SunBlockedNearBehind,
        SunBlockedFarForward,
        SunBlockedFarSide,
        SunBlockedFarBehind,

        NearSunStartAwayNearForward,
        NearSunStartAwayFarForward,
        NearSunStartAwayNearSide,
        NearSunStartAwayFarSide,
        NearSunStartAwayNearBehind,
        NearSunStartAwayFarBehind,

        NearSunStartTangentNearForward,
        NearSunStartTangentFarForward,
        NearSunStartTangentNearSideAway,
        NearSunStartTangentFarSideAway,
        NearSunStartTangentNearBehind,
        NearSunStartTangentFarBehind,

        NearSunStartTowardNearForward,
        NearSunStartTowardFarForward,
        NearSunStartTowardNearSide,
        NearSunStartTowardFarSide,
        NearSunStartTowardNearBehind,
        NearSunStartTowardFarBehind,

        NearSunStartExit
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
    private readonly TravelRoutePlan _activeTravelRoutePlan = new();
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
    private readonly ISystemShipRouteService2A _shipRouteService;
    private readonly SystemShipRouteResult2A _playerShipRouteBuildResult = new();
    private readonly ITargetService2A _targetService;
    private ISystemNpcRuntimeService _npcRuntimeService;
    private ISystemEnemyService _enemyService;

    private Vector2 _travelFacingDirection = Vector2.up;
    private Vector2 _routePreviewStartFacingDirection = Vector2.up;
    private float _routeTurnAdjustmentFactor = 1f;
    private float _routeSpeedAdjustmentFactor = 1f;
    private bool _useRouteInitialSpeedLimit;
    private float _routeInitialSpeedLimitFactor = 1f;
    private int _movingDestinationRouteRefreshBlockStartQuantTick = -1;
    private float _routeInitialSpeedLimitDistance;
    private bool _consumeSunFacingStartTurnTick;
    private bool _useFallbackStartTurnInPlaceRoute;
    private bool _useSunFacingStartTurnInPlaceRoute;
    private Vector2 _sunFacingStartTurnInPlaceDirection = Vector2.up;
    private Vector2 _fallbackStartTurnInPlaceDirection = Vector2.up;
    private float _activeRouteDistanceTravelled;
    private int _lastNpcDestinationRefreshTick = -1;
    private int _activeRouteBuildQuantTick = -1;
    private int _lastMovingDestinationRouteRefreshKey = -1;
    private int _movingDestinationRouteRefreshBlockedSlotsRemaining;
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
        _shipRouteService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemShipRouteService2A>();

        if (Bootstrapper.Instance.ServiceRegistry.TryGet(
                out ITargetService2A targetService))
        {
            _targetService = targetService;
        }

        State = new SystemTravelState();
        _eventBus.Subscribe<TravelFinishedEvent>(OnTravelFinished);
        _eventBus.Subscribe<GameTickStartedEvent>(OnGameTickStarted);
        _eventBus.Subscribe<GameTimeQuantumAdvancedEvent>(OnGameTimeQuantumAdvanced);
        _eventBus.Subscribe<SystemNpcPositionChangedEvent>(OnSystemNpcPositionChangedEvent);
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

    private void OnSystemNpcPositionChangedEvent(
    SystemNpcPositionChangedEvent evt)
    {
        if (State == null ||
            State.Destination == null ||
            State.Destination.Type != TravelDestinationType.Npc)
        {
            return;
        }

        if (State.Status != SystemTravelStatus.DestinationSelected)
            return;

        if (!string.Equals(
                State.Destination.RuntimeNpcId,
                evt.RuntimeNpcId))
        {
            return;
        }

        if (!IsTargetInCurrentSystem(evt.SystemId))
            return;

        if (!TryGetLiveDestinationPosition(
                out Vector3 destinationPosition))
        {
            CancelTravel();
            return;
        }

        State.DestinationPosition = destinationPosition;
        State.Destination.FixedMapPosition = destinationPosition;

        float distanceToDestination =
            Vector3.Distance(
                State.GetCurrentPosition(),
                State.DestinationPosition);

        if (distanceToDestination <= ArrivalDistanceThreshold)
            return;

        LogCustom(
            "[PLAYER-NPC-FOLLOW] Start selected NPC travel after NPC moved. " +
            "Npc=" + evt.RuntimeNpcId +
            " | PlayerPosition=" + FormatVector3(State.GetCurrentPosition()) +
            " | DestinationPosition=" + FormatVector3(State.DestinationPosition) +
            " | Distance=" + distanceToDestination.ToString("0.###"));

        StartTravelAutomaticallyIfPossible();
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
        if (IsMapPointInsideSunForbiddenDestinationRadius(mapPosition))
        {
            RejectForbiddenMapPointDestination(
                mapPosition,
                "map point is inside sun forbidden destination radius");

            return;
        }

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

    private void RejectForbiddenMapPointDestination(
        Vector3 mapPosition,
        string reason)
    {
        LogCustom(
            "[RouteTrace.RejectMapPoint] " +
            "Reason = " +
            reason +
            " | TargetPosition = " +
            FormatVector3(mapPosition));

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

        _eventBus.Publish(new SystemTravelCancelledEvent());
    }

    private bool IsMapPointInsideSunForbiddenDestinationRadius(
        Vector3 destinationPosition)
    {
        SunAvoidanceObstacle obstacle =
            GetSunAvoidanceObstacle(
                destinationPosition.z,
                destinationPosition);

        if (!obstacle.HasObstacle ||
            obstacle.BlockingRadius <= 0f)
        {
            return false;
        }

        float targetDistanceFromSun =
            Vector2.Distance(
                new Vector2(
                    destinationPosition.x,
                    destinationPosition.y),
                new Vector2(
                    obstacle.Center.x,
                    obstacle.Center.y));

        return targetDistanceFromSun < obstacle.BlockingRadius;
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

        Vector3 travelDestinationPosition =
            GetNpcFollowDestinationPosition(npcPosition);

        ClearPreviousTravelSelectionForNpcDestination();

        State.Destination = SystemTravelDestination.Npc(
            runtimeNpcId,
            npcPosition);

        State.DestinationPosition = travelDestinationPosition;
        State.Status = SystemTravelStatus.DestinationSelected;
        State.TravelProgress01 = 0f;
        _lastNpcDestinationRefreshTick = -1;

        _eventBus.Publish(new DestinationSelectedEvent(
            TravelDestinationType.Npc,
            travelDestinationPosition,
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

        _travelFacingDirection =
            GetCurrentShipFacingDirection();

        _routePreviewStartFacingDirection =
            _travelFacingDirection;

        _activeTravelRoutePath.Clear();
        _activeRouteDistanceTravelled = 0f;

        _activeRouteBuildQuantTick =
            GetCurrentTravelQuantTick();

        _movingDestinationRouteRefreshBlockStartQuantTick =
            _activeRouteBuildQuantTick;

        _lastMovingDestinationRouteRefreshKey = -1;

        _routeTurnAdjustmentFactor = 1f;
        _routeSpeedAdjustmentFactor = 1f;

        ClearRouteInitialSpeedLimit();

        _mapPointRouteTraceFrameCount = 0;

        bool shouldTraceRouteBuild =
            IsMapPointDestination() ||
            IsNpcDestination();

        if (shouldTraceRouteBuild)
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

        if (shouldTraceRouteBuild)
        {
            LogMapPointRouteCaseTrace(routeClassification);

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

            _isMapPointRouteBuildTraceEnabled = false;

            ResetFailedTravelToDestinationSelected();
            return;
        }

        SystemShipRouteRequest2A routeRequest =
            new SystemShipRouteRequest2A
            {
                SystemId = GetCurrentPlayerSystemId(),
                StartPosition = State.StartPosition,
                DestinationPosition = State.DestinationPosition,
                StartFacingDirection = _routePreviewStartFacingDirection,
                TargetKind = GetCurrentShipRouteTargetKind2A(),
                Settings = CreatePlayerShipRouteSettings2A()
            };

        bool routeBuilt =
            _shipRouteService != null &&
            _shipRouteService.TryBuildRoute(
                routeRequest,
                _playerShipRouteBuildResult);

        if (routeBuilt)
        {
            CopyRoutePath(
                _playerShipRouteBuildResult.Path,
                _activeTravelRoutePath);

            _routeTurnAdjustmentFactor =
                Mathf.Clamp01(_playerShipRouteBuildResult.TurnRadiusFactor);

            _routeSpeedAdjustmentFactor =
                Mathf.Clamp01(_playerShipRouteBuildResult.SpeedFactor);
        }

        RebuildActiveTravelRoutePlan(
            routeClassification,
            State.StartPosition,
            State.DestinationPosition,
            _routePreviewStartFacingDirection);

        if (!IsMovingTravelDestination())
        {
            ApplyShortRouteSpeedAdjustment();
        }

        ConfigureRouteInitialSpeedLimit(
            routeClassification,
            State.StartPosition,
            _routePreviewStartFacingDirection);

        if (_activeTravelRoutePath.Count <= 1)
        {
            Debug.LogWarning("[SystemTravelService] Cannot start travel: route could not be built.");

            LogRouteDebugSnapshot(
                "Route build failed",
                State.StartPosition,
                State.DestinationPosition,
                _routePreviewStartFacingDirection);

            if (IsMapPointDestination())
            {
                RejectMapPointTravelDestination(
                    "route could not be built");
                return;
            }

            _isMapPointRouteBuildTraceEnabled = false;

            ResetFailedTravelToDestinationSelected();
            return;
        }

        State.DestinationPosition =
            _activeTravelRoutePath[_activeTravelRoutePath.Count - 1];

        State.TravelDistance =
            GetPathLength(_activeTravelRoutePath);

        if (shouldTraceRouteBuild)
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
            State.DestinationPosition));

        LogCustom("Travel started.");
        LogCustom("State = " + State);
    }

    private SystemShipRouteSettings2A CreatePlayerShipRouteSettings2A()
    {
        ShipMovementConfig movementConfig =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        return new SystemShipRouteSettings2A
        {
            Speed = Mathf.Max(0.01f, GetCurrentShipTravelSpeed()),
            TurnRadius = Mathf.Max(0f, GetCurrentShipTurnRadius()),
            ArrivalDistanceThreshold = ArrivalDistanceThreshold,
            SunAvoidanceSafetyMargin = SunAvoidanceSafetyMargin,
            SunAvoidanceArcSegments = SunAvoidanceArcSegments,
            AllowSunAvoidance = true,
            RouteSubstepsPerTick = movementConfig != null ? movementConfig.RouteSubstepsPerTick : 10,
            RouteStraightExitAngleDegrees = movementConfig != null ? movementConfig.RouteStraightExitAngleDegrees : 3f,
            TurnRadiusAdjustmentStepPercent = movementConfig != null ? movementConfig.RouteTurnRadiusAdjustmentStepPercent : 5f,
            SpeedAdjustmentStepPercent = movementConfig != null ? movementConfig.RouteSpeedAdjustmentStepPercent : 2.5f,
            MinTurnRadiusAdjustmentFactor = movementConfig != null ? movementConfig.MinRouteTurnRadiusAdjustmentFactor : 0.05f,
            MinTurnRadiusAbsolute = movementConfig != null ? movementConfig.MinRouteTurnRadiusAbsolute : 30f,
            BehindSmallTurnAngleToleranceDegrees = movementConfig != null ? movementConfig.RouteBehindSmallTurnAngleToleranceDegrees : 75f,
            MaxRoutePlanSteps = RoutePlanMaxSteps,
            SunAvoidanceTurnRouteReserveMultiplier = SunAvoidanceTurnRouteReserveMultiplier,
            DebugLog = null,
            DebugPrefix = string.Empty
        };
    }

    private SystemShipRouteTargetKind2A GetCurrentShipRouteTargetKind2A()
    {
        if (State == null ||
            State.Destination == null)
        {
            return SystemShipRouteTargetKind2A.MapPoint;
        }

        switch (State.Destination.Type)
        {
            case TravelDestinationType.Planet:
                return SystemShipRouteTargetKind2A.Planet;

            case TravelDestinationType.Station:
                return SystemShipRouteTargetKind2A.Station;

            case TravelDestinationType.SystemExit:
                return SystemShipRouteTargetKind2A.SystemExit;

            case TravelDestinationType.Npc:
                return SystemShipRouteTargetKind2A.Npc;

            case TravelDestinationType.MapPoint:
            default:
                return SystemShipRouteTargetKind2A.MapPoint;
        }
    }

    private void RebuildActiveTravelRoutePlan(
    RouteDestinationClassification classification,
    Vector3 startPosition,
    Vector3 destinationPosition,
    Vector2 startFacingDirection)
    {
        _activeTravelRoutePlan.Clear();

        if (ShouldUseStartTurnInPlace(classification))
        {
            Vector2 turnDirection =
                GetStartTurnInPlaceDirection(
                    classification,
                    startPosition,
                    destinationPosition,
                    startFacingDirection);

            _activeTravelRoutePlan.AddTurnInPlace(
                startPosition,
                startFacingDirection,
                turnDirection);
        }

        _activeTravelRoutePlan.SetMovePath(
            _activeTravelRoutePath);
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

    public void CycleNpcFollowMode()
    {
        _npcFollowModeIndex =
            (NpcFollowModeIndex + 1) %
            NpcFollowModeCount;

        float followDistance =
            GetNpcFollowModeDistance();

        _eventBus.Publish(
            new SystemNpcFollowModeChangedEvent2A(
                NpcFollowModeIndex,
                followDistance));

        if (State == null ||
            State.Destination == null ||
            State.Destination.Type != TravelDestinationType.Npc)
        {
            return;
        }

        if (!TryGetLiveDestinationPosition(
                out Vector3 destinationPosition))
        {
            return;
        }

        State.DestinationPosition =
            destinationPosition;

        State.Destination.FixedMapPosition =
            destinationPosition;

        if (State.Status != SystemTravelStatus.Flying)
            return;

        TryRebuildActiveRouteToMovingDestination(
            destinationPosition,
            GetCurrentTravelQuantTick(),
            false);
    }

    public float GetNpcFollowModeDistance()
    {
        ResolvePlayerWeaponFollowRange(
            out float minFollowDistance,
            out float maxFollowDistance);

        switch (NpcFollowModeIndex)
        {
            case 0:
                return minFollowDistance;

            case 1:
                return (minFollowDistance + maxFollowDistance) * 0.5f;

            case 2:
                return maxFollowDistance;

            default:
                return minFollowDistance;
        }
    }

    private void ResolvePlayerWeaponFollowRange(
        out float minFollowDistance,
        out float maxFollowDistance)
    {
        minFollowDistance =
            NpcFollowDefaultMinDistance;

        maxFollowDistance =
            NpcFollowDefaultMaxDistance;

        ShipRuntimeData activeShip =
            _hangarService != null
                ? _hangarService.GetActiveShipState()
                : null;

        if (activeShip == null ||
            activeShip.EquippedWeaponIds == null ||
            activeShip.EquippedWeaponIds.Count == 0 ||
            _configService == null)
        {
            return;
        }

        bool hasWeaponRange =
            false;

        float minWeaponRange =
            float.MaxValue;

        float maxWeaponRange =
            0f;

        for (int i = 0; i < activeShip.EquippedWeaponIds.Count; i++)
        {
            string weaponConfigId =
                activeShip.EquippedWeaponIds[i];

            if (string.IsNullOrWhiteSpace(weaponConfigId))
                continue;

            WeaponConfig weaponConfig =
                _configService.GetWeaponConfigById(
                    weaponConfigId);

            if (weaponConfig == null)
                continue;

            if (weaponConfig.RangeMin > 0f)
            {
                minWeaponRange =
                    Mathf.Min(
                        minWeaponRange,
                        weaponConfig.RangeMin);

                hasWeaponRange =
                    true;
            }

            if (weaponConfig.RangeMax > 0f)
            {
                maxWeaponRange =
                    Mathf.Max(
                        maxWeaponRange,
                        weaponConfig.RangeMax);

                hasWeaponRange =
                    true;
            }
        }

        if (!hasWeaponRange)
            return;

        if (minWeaponRange == float.MaxValue)
            minWeaponRange = maxWeaponRange;

        if (maxWeaponRange <= 0f)
            maxWeaponRange = minWeaponRange;

        if (maxWeaponRange < minWeaponRange)
            maxWeaponRange = minWeaponRange;

        minFollowDistance =
            Mathf.Max(
                0f,
                minWeaponRange *
                NpcFollowWeaponRangeFactor);

        maxFollowDistance =
            Mathf.Max(
                minFollowDistance,
                maxWeaponRange *
                NpcFollowWeaponRangeFactor);
    }

    private Vector3 GetNpcFollowDestinationPosition(
     Vector3 npcPosition)
    {
        float followDistance =
            Mathf.Max(
                ArrivalDistanceThreshold,
                GetNpcFollowModeDistance());

        Vector3 shipPosition =
            State != null
                ? State.GetCurrentPosition()
                : GetCurrentShipPosition();

        Vector2 directionFromTargetToShip =
            new Vector2(
                shipPosition.x - npcPosition.x,
                shipPosition.y - npcPosition.y);

        if (directionFromTargetToShip.sqrMagnitude <=
            RouteSegmentEpsilon)
        {
            Vector2 facingDirection =
                GetCurrentShipFacingDirection();

            directionFromTargetToShip =
                facingDirection.sqrMagnitude > RouteSegmentEpsilon
                    ? -facingDirection.normalized
                    : Vector2.down;
        }
        else
        {
            directionFromTargetToShip.Normalize();
        }

        Vector3 followPosition =
            npcPosition +
            new Vector3(
                directionFromTargetToShip.x,
                directionFromTargetToShip.y,
                0f) *
            followDistance;

        followPosition.z =
            shipPosition.z;

        return PushPositionOutsideSunBlockingRadius(
            followPosition,
            shipPosition);
    }

    private Vector3 PushPositionOutsideSunBlockingRadius(
    Vector3 position,
    Vector3 fallbackDirectionSource)
    {
        SunAvoidanceObstacle obstacle =
            GetSunAvoidanceObstacle(
                position.z,
                position);

        if (!obstacle.HasObstacle ||
            obstacle.BlockingRadius <= 0f)
        {
            return position;
        }

        Vector2 fromSunToPosition =
            new Vector2(
                position.x - obstacle.Center.x,
                position.y - obstacle.Center.y);

        float safeDistance =
            obstacle.BlockingRadius +
            Mathf.Max(ArrivalDistanceThreshold, RouteSegmentEpsilon);

        if (fromSunToPosition.magnitude >= safeDistance)
            return position;

        if (fromSunToPosition.sqrMagnitude <= RouteSegmentEpsilon)
        {
            fromSunToPosition =
                new Vector2(
                    fallbackDirectionSource.x - obstacle.Center.x,
                    fallbackDirectionSource.y - obstacle.Center.y);
        }

        if (fromSunToPosition.sqrMagnitude <= RouteSegmentEpsilon)
            fromSunToPosition = Vector2.up;

        Vector2 safePosition =
            new Vector2(
                obstacle.Center.x,
                obstacle.Center.y) +
            fromSunToPosition.normalized * safeDistance;

        return new Vector3(
            safePosition.x,
            safePosition.y,
            position.z);
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

        if (_consumeSunFacingStartTurnTick)
        {
            speedFactor =
                Mathf.Min(
                    speedFactor,
                    Mathf.Clamp01(_routeInitialSpeedLimitFactor));
        }

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
        if (targetInsideSunBody)
            return RouteDestinationCase.ForbiddenDestination;

        if (State != null &&
            State.Destination != null &&
            State.Destination.Type == TravelDestinationType.Npc)
        {
            return RouteDestinationCase.MovingTarget;
        }

        if (startInsideSunSafety)
        {
            return GetNearSunStartDestinationCase(
                shipPosition,
                targetPosition,
                shipFacingDirection,
                obstacle,
                bearingAngleDegrees,
                isNear);
        }

        return GetSunBlockedRouteCase(
            isNear,
            bearingAngleDegrees);
    }

    private RouteDestinationCase GetNearSunStartDestinationCase(
    Vector3 shipPosition,
    Vector3 targetPosition,
    Vector2 shipFacingDirection,
    SunAvoidanceObstacle obstacle,
    float targetBearingAngleDegrees,
    bool isNear)
    {
        if (!obstacle.HasObstacle)
        {
            return GetSunBlockedRouteCase(
                isNear,
                targetBearingAngleDegrees);
        }

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

        float tangentToleranceAngle =
            GetSunTangentToleranceAngleDegrees();

        if (sunFacingAngle < 90f - tangentToleranceAngle)
        {
            if (targetForward)
                return isNear
                    ? RouteDestinationCase.NearSunStartAwayNearForward
                    : RouteDestinationCase.NearSunStartAwayFarForward;

            if (targetBehind)
                return isNear
                    ? RouteDestinationCase.NearSunStartAwayNearBehind
                    : RouteDestinationCase.NearSunStartAwayFarBehind;

            return isNear
                ? RouteDestinationCase.NearSunStartAwayNearSide
                : RouteDestinationCase.NearSunStartAwayFarSide;
        }

        if (sunFacingAngle > 90f + tangentToleranceAngle)
        {
            if (targetForward)
                return isNear
                    ? RouteDestinationCase.NearSunStartTowardNearForward
                    : RouteDestinationCase.NearSunStartTowardFarForward;

            if (targetBehind)
                return isNear
                    ? RouteDestinationCase.NearSunStartTowardNearBehind
                    : RouteDestinationCase.NearSunStartTowardFarBehind;

            return isNear
                ? RouteDestinationCase.NearSunStartTowardNearSide
                : RouteDestinationCase.NearSunStartTowardFarSide;
        }

        if (targetForward)
            return isNear
                ? RouteDestinationCase.NearSunStartTangentNearForward
                : RouteDestinationCase.NearSunStartTangentFarForward;

        if (targetBehind)
            return isNear
                ? RouteDestinationCase.NearSunStartTangentNearBehind
                : RouteDestinationCase.NearSunStartTangentFarBehind;

        if (GetSunStartTargetSideIsAwayFromSun(
                shipPosition,
                targetPosition,
                radialAwayFromSun))
        {
            return isNear
                ? RouteDestinationCase.NearSunStartTangentNearSideAway
                : RouteDestinationCase.NearSunStartTangentFarSideAway;
        }

        return GetSunBlockedRouteCase(
            isNear,
            targetBearingAngleDegrees);
    }

    private RouteDestinationCase GetSunBlockedRouteCase(
    bool isNear,
    float bearingAngleDegrees)
    {
        float forwardAngle =
            GetRouteForwardSectorAngleDegrees();

        float behindAngle =
            GetRouteBehindSectorAngleDegrees();

        if (bearingAngleDegrees <= forwardAngle)
        {
            return isNear
                ? RouteDestinationCase.SunBlockedNearForward
                : RouteDestinationCase.SunBlockedFarForward;
        }

        if (bearingAngleDegrees >= behindAngle)
        {
            return isNear
                ? RouteDestinationCase.SunBlockedNearBehind
                : RouteDestinationCase.SunBlockedFarBehind;
        }

        return isNear
            ? RouteDestinationCase.SunBlockedNearSide
            : RouteDestinationCase.SunBlockedFarSide;
    }

    private static bool GetSunStartTargetSideIsAwayFromSun(
        Vector3 shipPosition,
        Vector3 targetPosition,
        Vector2 radialAwayFromSun)
    {
        Vector2 targetDirection =
            new Vector2(
                targetPosition.x - shipPosition.x,
                targetPosition.y - shipPosition.y);

        return targetDirection.sqrMagnitude <= RouteSegmentEpsilon ||
               Vector2.Dot(targetDirection.normalized, radialAwayFromSun) >= 0f;
    }

    private bool IsFacingNearSunTangent(
        Vector2 facingDirection,
        Vector2 radialAwayFromSun)
    {
        float sunFacingAngle =
            Vector2.Angle(
                TurnRadiusRouteMath2A.NormalizeDirectionOrUp(facingDirection),
                radialAwayFromSun.normalized);

        float tangentOffsetAngle =
            Mathf.Abs(90f - sunFacingAngle);

        return tangentOffsetAngle <=
               GetSunTangentToleranceAngleDegrees();
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
        if (!classification.Obstacle.HasObstacle)
            return false;

        if (IsNearSunStartTowardSunDestinationCase(
                classification.DestinationCase))
        {
            return true;
        }

        if (!IsStartCloseEnoughToSunForFacingEscape(classification))
            return false;

        return IsDirectionFacingSunFromPoint(
            shipPosition,
            shipFacingDirection,
            classification.Obstacle);
    }

    private bool IsStartCloseEnoughToSunForFacingEscape(
    RouteDestinationClassification classification)
    {
        if (!classification.Obstacle.HasObstacle)
            return false;

        float nearSunDistance =
            classification.Obstacle.BlockingRadius +
            Mathf.Max(
                GetSunAvoidanceRoutePaddingMax(),
                GetCurrentShipTurnRadius() * 0.1f,
                ArrivalDistanceThreshold * 4f);

        return classification.StartDistanceFromSun <= nearSunDistance;
    }

    private static bool IsNearSunStartTowardSunDestinationCase(
    RouteDestinationCase destinationCase)
    {
        return destinationCase == RouteDestinationCase.NearSunStartTowardNearForward ||
               destinationCase == RouteDestinationCase.NearSunStartTowardFarForward ||
               destinationCase == RouteDestinationCase.NearSunStartTowardNearSide ||
               destinationCase == RouteDestinationCase.NearSunStartTowardFarSide ||
               destinationCase == RouteDestinationCase.NearSunStartTowardNearBehind ||
               destinationCase == RouteDestinationCase.NearSunStartTowardFarBehind;
    }

    private static bool IsNearSunStartTangentEscapeDestinationCase(
    RouteDestinationCase destinationCase)
    {
        return IsNearSunStartTowardSunDestinationCase(destinationCase);
    }

    private static bool ShouldStartNearSunCaseWithMinimumTurnRadius(
    RouteDestinationCase destinationCase)
    {
        return IsNearSunStartTowardSunDestinationCase(destinationCase);
    }

    private void ClearRouteInitialSpeedLimit()
    {
        _useRouteInitialSpeedLimit = false;
        _routeInitialSpeedLimitFactor = 1f;
        _routeInitialSpeedLimitDistance = 0f;
        _consumeSunFacingStartTurnTick = false;
    }

    private void ConfigureRouteInitialSpeedLimit(
    RouteDestinationClassification classification,
    Vector3 shipPosition,
    Vector2 shipFacingDirection)
    {
        ClearRouteInitialSpeedLimit();

        if (ShouldUseBehindSmallTurn(classification))
        {
            float slowDistance =
                GetBehindSmallTurnArcLength(
                    classification);

            _useRouteInitialSpeedLimit = true;
            _routeInitialSpeedLimitFactor =
                Mathf.Max(
                    GetMinRouteTurnRadiusAdjustmentFactor(),
                    GetSpeedFactorForOneTickDistance(slowDistance));
            _routeInitialSpeedLimitDistance =
                slowDistance;

            LogMapPointInitialSpeedLimitTrace(
                "BehindSmallTurn",
                slowDistance,
                _routeInitialSpeedLimitFactor);

            return;
        }

        if (ShouldUseStartTurnInPlace(classification))
        {
            _routeInitialSpeedLimitFactor =
                GetMinRouteTurnRadiusAdjustmentFactor();

            _consumeSunFacingStartTurnTick = true;

            LogMapPointInitialSpeedLimitTrace(
                "StartTurnInPlace",
                0f,
                _routeInitialSpeedLimitFactor);

            return;
        }
    }

    private float GetRouteInitialTurnSlowDistance(
        IReadOnlyList<Vector3> routePath,
        SunAvoidanceObstacle obstacle,
        Vector2 startFacingDirection)
    {
        if (routePath == null ||
            routePath.Count <= 2 ||
            !obstacle.HasObstacle)
        {
            return 0f;
        }

        Vector2 previousDirection =
            TurnRadiusRouteMath2A.NormalizeDirectionOrUp(
                startFacingDirection);

        float slowDistance =
            0f;

        float straightDistanceAfterTurn =
            0f;

        bool hasDetectedTurn =
            false;

        const float turningAngleThresholdDegrees = 2f;
        const float straightAfterTurnDistance = 10f;

        for (int i = 1; i < routePath.Count; i++)
        {
            Vector3 from =
                routePath[i - 1];

            Vector3 to =
                routePath[i];

            Vector3 segment3 =
                to - from;

            Vector2 segmentDirection =
                new Vector2(
                    segment3.x,
                    segment3.y);

            float segmentDistance =
                segmentDirection.magnitude;

            if (segmentDistance <= RouteSegmentEpsilon)
                continue;

            segmentDirection =
                segmentDirection.normalized;

            float angleDelta =
                Vector2.Angle(
                    previousDirection,
                    segmentDirection);

            bool isStillTurning =
                angleDelta > turningAngleThresholdDegrees;

            bool isFacingSun =
                IsDirectionFacingSunFromPoint(
                    from,
                    segmentDirection,
                    obstacle);

            if (isStillTurning)
            {
                hasDetectedTurn = true;
                straightDistanceAfterTurn = 0f;
                slowDistance += segmentDistance;
            }
            else if (hasDetectedTurn)
            {
                straightDistanceAfterTurn += segmentDistance;

                if (straightDistanceAfterTurn >= straightAfterTurnDistance)
                    break;

                slowDistance += segmentDistance;
            }
            else if (isFacingSun)
            {
                slowDistance += segmentDistance;
            }
            else
            {
                break;
            }

            previousDirection =
                segmentDirection;
        }

        return slowDistance;
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
            GetCurrentTravelTickDestinationPosition();

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
            IsNpcDestination() &&
            distanceToDestination > ArrivalDistanceThreshold)
        {
            if (TryRefreshReachedMovingDestinationRoute(quantTick))
            {
                PublishTravelProgress(State.TravelProgress01);
                return;
            }
        }

        if (_consumeSunFacingStartTurnTick &&
            hasActiveTravelRoute)
        {
            Vector2 tangentDirection =
                GetDirectionOnPathAtDistance(
                    _activeTravelRoutePath,
                    0f);

            if (_activeTravelRoutePlan.HasTurnInPlaceAtStart &&
                _activeTravelRoutePlan.Steps.Count > 0)
            {
                tangentDirection =
                    _activeTravelRoutePlan.Steps[0].ToDirection;
            }

            if (tangentDirection.sqrMagnitude > RouteSegmentEpsilon)
            {
                _travelFacingDirection =
                    TurnRadiusRouteMath2A.NormalizeDirectionOrUp(
                        tangentDirection);
            }

            _consumeSunFacingStartTurnTick = false;

            SyncShipMovementStateWithTravel(
                State.GetCurrentPosition(),
                0f);

            PublishTravelProgress(State.TravelProgress01);

            TryRefreshActiveRouteForMovingDestination(quantTick);

            LogMapPointRouteTraceTick(
                "TurnInPlace",
                deltaTime,
                quantTick,
                State.GetCurrentPosition(),
                State.DestinationPosition,
                0f,
                0f,
                "TurnInPlace",
                false);

            return;
        }

        if (!hasActiveTravelRoute &&
            distanceToDestination <= ArrivalDistanceThreshold)
        {
            if (TryRefreshReachedMovingDestinationRoute(quantTick))
            {
                PublishTravelProgress(State.TravelProgress01);
                return;
            }

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

        Vector3 nextPosition = IsNpcDestination()
            ? CalculateNextTravelPositionByTickLockedNpcPath(
                movementDistance,
                out destinationReached)
            : CalculateNextTravelPositionOnActiveRoute(
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
            Vector3.Distance(positionBeforeMove, nextPosition),
            destinationReached ? "ReachedEndOfActiveRoute" : "None",
            destinationReached);

        if (destinationReached ||
            (!hasActiveTravelRoute &&
             Vector3.Distance(State.GetCurrentPosition(), State.DestinationPosition) <= ArrivalDistanceThreshold))
        {
            if (TryRefreshReachedMovingDestinationRoute(quantTick))
            {
                PublishTravelProgress(State.TravelProgress01);
                return;
            }

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
                Vector3.Distance(positionBeforeMove, State.GetCurrentPosition()),
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
                State.TravelDistance);
        }
        else
        {
            State.TravelProgress01 = 1f;
        }

        PublishTravelProgress(State.TravelProgress01);

        TryRefreshActiveRouteForMovingDestination(quantTick);
    }

    private Vector3 CalculateNextTravelPositionByTickLockedNpcPath(
        float movementDistance,
        out bool destinationReached)
    {
        return CalculateNextTravelPositionOnActiveRoute(
            movementDistance,
            out destinationReached);
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
        if (!TryGetLiveDestinationPosition(
                out Vector3 destinationPosition))
        {
            return State != null
                ? State.GetCurrentPosition()
                : Vector3.zero;
        }

        return destinationPosition;
    }

    private bool TryRefreshReachedMovingDestinationRoute(
    int quantTick)
    {
        if (!IsMovingTravelDestination())
            return false;

        if (!TryGetLiveDestinationPosition(
                out Vector3 liveDestinationPosition))
        {
            if (IsNpcDestination())
                CancelTravel();

            return false;
        }

        if (Vector3.Distance(
                State.GetCurrentPosition(),
                liveDestinationPosition) <= ArrivalDistanceThreshold)
        {
            State.DestinationPosition =
                liveDestinationPosition;

            return false;
        }

        return TryRebuildActiveRouteToMovingDestination(
            liveDestinationPosition,
            quantTick);
    }

    private void TryRefreshActiveRouteForMovingDestination(
    int quantTick)
    {
        if (!ShouldRefreshActiveRouteForMovingDestination())
            return;

        if (!ShouldRefreshMovingDestinationRouteNow(
                quantTick,
                out bool isMandatoryEndOfTickRefresh))
        {
            return;
        }

        Vector3 currentRouteEndPosition =
            GetActiveRouteEndPositionForRefresh();

        if (!TryGetLiveDestinationPosition(
                out Vector3 liveDestinationPosition))
        {
            if (IsNpcDestination())
                CancelTravel();

            return;
        }

        bool preserveLockedPrefix =
            ShouldPreserveMovingDestinationLockedPrefix(
                quantTick);

        bool alwaysRefresh =
            ShouldAlwaysRefreshMovingDestinationRoute();

        float refreshThreshold =
            isMandatoryEndOfTickRefresh
                ? 0.01f
                : Mathf.Max(0.5f, ArrivalDistanceThreshold);

        float destinationShift =
            Vector3.Distance(
                liveDestinationPosition,
                currentRouteEndPosition);

        if (!alwaysRefresh &&
            !preserveLockedPrefix &&
            destinationShift < refreshThreshold)
        {
            return;
        }

        TryRebuildActiveRouteToMovingDestination(
            liveDestinationPosition,
            quantTick,
            preserveLockedPrefix);
    }

    private bool IsMovingDestinationInitialRouteRefreshBlocked(
    int quantTick)
    {
        if (State == null ||
            State.Destination == null)
        {
            return false;
        }

        if (State.Destination.Type == TravelDestinationType.Npc)
            return false;

        int blockedTicks =
            GetMovingDestinationRouteRefreshBlockedInitialTicks();

        if (blockedTicks <= 0)
            return false;

        if (_movingDestinationRouteRefreshBlockStartQuantTick < 0)
            return false;

        int passedTicks =
            quantTick - _movingDestinationRouteRefreshBlockStartQuantTick;

        if (passedTicks < 0)
            return false;

        return passedTicks < blockedTicks;
    }

    private int GetMovingDestinationRouteRefreshBlockedInitialTicks()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        if (config == null)
            return 1;

        return config.MovingDestinationRouteRefreshBlockedInitialTicks;
    }

    private bool ShouldRefreshMovingDestinationRouteNow(
    int quantTick,
    out bool isMandatoryEndOfTickRefresh)
    {
        isMandatoryEndOfTickRefresh =
            IsCurrentTravelFrameAtEndOfTick();

        if (isMandatoryEndOfTickRefresh)
        {
            int endTickRefreshKey =
                quantTick * 1000 + 999;

            if (_lastMovingDestinationRouteRefreshKey == endTickRefreshKey)
                return false;

            _lastMovingDestinationRouteRefreshKey =
                endTickRefreshKey;

            return true;
        }

        int refreshesPerTick =
            GetMovingDestinationRouteRefreshesPerTick();

        if (refreshesPerTick <= 1)
            return false;

        float tickProgress =
            GetCurrentTickProgress01();

        int refreshSlot =
            Mathf.FloorToInt(
                tickProgress *
                refreshesPerTick);

        refreshSlot =
            Mathf.Clamp(
                refreshSlot,
                0,
                refreshesPerTick - 1);

        if (refreshSlot <= 0)
            return false;

        int refreshKey =
            quantTick * 1000 + refreshSlot;

        if (_lastMovingDestinationRouteRefreshKey == refreshKey)
            return false;

        _lastMovingDestinationRouteRefreshKey =
            refreshKey;

        return true;
    }

    private bool TryConsumeMovingDestinationRouteRefreshBlockedSlot()
    {
        if (_movingDestinationRouteRefreshBlockedSlotsRemaining <= 0)
            return false;

        _movingDestinationRouteRefreshBlockedSlotsRemaining--;

        return true;
    }

    private float GetCurrentTickProgress01()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null ||
            !Bootstrapper.Instance.ServiceRegistry.TryGet<IGameTimeService>(
                out IGameTimeService gameTimeService) ||
            gameTimeService == null ||
            gameTimeService.State == null)
        {
            return 0f;
        }

        float secondsPerTick =
            Mathf.Max(
                0.01f,
                GameTimeState.SecondsPerDay);

        return Mathf.Clamp01(
            gameTimeService.State.Accumulator /
            secondsPerTick);
    }

    private int GetMovingDestinationRouteRefreshesPerTick()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        if (config == null)
            return 10;

        return config.MovingDestinationRouteRefreshesPerTick;
    }

    private bool ShouldRefreshActiveRouteForMovingDestination()
    {
        if (State == null ||
            State.Destination == null ||
            State.Status != SystemTravelStatus.Flying)
        {
            return false;
        }

        switch (State.Destination.Type)
        {
            case TravelDestinationType.Planet:
            case TravelDestinationType.Npc:
            case TravelDestinationType.Station:
            case TravelDestinationType.SystemExit:
                return true;

            default:
                return false;
        }
    }

    private bool TryGetLiveDestinationPosition(
    out Vector3 destinationPosition)
    {
        destinationPosition =
            State != null
                ? State.GetCurrentPosition()
                : Vector3.zero;

        if (State == null ||
            State.Destination == null)
        {
            return false;
        }

        SystemTravelDestination destination =
            State.Destination;

        switch (destination.Type)
        {
            case TravelDestinationType.Planet:
                if (destination.PlanetData == null)
                    return false;

                destinationPosition =
                    _orbitalMotionService.GetPlanetCurrentPosition(
                        destination.PlanetData.PlanetOrbit);

                destinationPosition.z =
                    State.GetCurrentPosition().z;

                return true;

            case TravelDestinationType.Npc:
                if (!TryGetNpcDestinationPosition(
                        destination.RuntimeNpcId,
                        out Vector3 npcPosition))
                {
                    return false;
                }

                npcPosition.z =
                    State.GetCurrentPosition().z;

                destinationPosition =
                    GetNpcFollowDestinationPosition(npcPosition);

                return true;

            case TravelDestinationType.Station:
                if (destination.StationData != null)
                {
                    Vector3 stationPosition =
                        destination.StationData.LocalOffset;

                    stationPosition.z =
                        State.GetCurrentPosition().z;

                    destinationPosition =
                        stationPosition;

                    return true;
                }

                destinationPosition =
                    destination.FixedMapPosition;

                destinationPosition.z =
                    State.GetCurrentPosition().z;

                return true;

            case TravelDestinationType.SystemExit:
                if (destination.RouteConfig != null &&
                    !string.IsNullOrWhiteSpace(destination.FromSystemId))
                {
                    Vector3 exitPoint =
                        destination.RouteConfig.GetExitPoint(
                            destination.FromSystemId);

                    exitPoint.z =
                        State.GetCurrentPosition().z;

                    destinationPosition =
                        exitPoint;

                    return true;
                }

                if (destination.SystemLink != null)
                {
                    Vector3 linkExitPoint =
                        destination.SystemLink.ExitPoint;

                    linkExitPoint.z =
                        State.GetCurrentPosition().z;

                    destinationPosition =
                        linkExitPoint;

                    return true;
                }

                destinationPosition =
                    destination.FixedMapPosition;

                destinationPosition.z =
                    State.GetCurrentPosition().z;

                return true;

            case TravelDestinationType.MapPoint:
                destinationPosition =
                    destination.FixedMapPosition;

                destinationPosition.z =
                    State.GetCurrentPosition().z;

                return true;

            default:
                return false;
        }
    }

    private bool TryRebuildActiveRouteToMovingDestination(
    Vector3 liveDestinationPosition,
    int quantTick,
    bool preserveLockedPrefix = false)
    {
        Vector3 routeStartPosition =
            State.GetCurrentPosition();

        Vector2 routeStartFacingDirection =
            _travelFacingDirection.sqrMagnitude > RouteSegmentEpsilon
                ? _travelFacingDirection
                : GetCurrentShipFacingDirection();

        RouteDestinationClassification routeClassification =
            ClassifyRouteDestination(
                routeStartPosition,
                liveDestinationPosition,
                routeStartFacingDirection);

        if (routeClassification.DestinationCase ==
            RouteDestinationCase.ForbiddenDestination)
        {
            return false;
        }

        float previousTurnFactor = _routeTurnAdjustmentFactor;
        float previousSpeedFactor = _routeSpeedAdjustmentFactor;
        bool previousUseInitialSpeedLimit = _useRouteInitialSpeedLimit;
        float previousInitialSpeedLimitFactor = _routeInitialSpeedLimitFactor;
        float previousInitialSpeedLimitDistance = _routeInitialSpeedLimitDistance;
        bool previousConsumeSunFacingStartTurnTick = _consumeSunFacingStartTurnTick;
        bool previousUseFallbackStartTurnInPlaceRoute = _useFallbackStartTurnInPlaceRoute;
        bool previousUseSunFacingStartTurnInPlaceRoute = _useSunFacingStartTurnInPlaceRoute;
        Vector2 previousFallbackStartTurnInPlaceDirection = _fallbackStartTurnInPlaceDirection;
        Vector2 previousSunFacingStartTurnInPlaceDirection = _sunFacingStartTurnInPlaceDirection;
        RouteDestinationCase previousActiveRouteDestinationCase = _activeRouteDestinationCase;

        ClearRouteInitialSpeedLimit();

        Vector3 nextStateStartPosition =
            routeStartPosition;

        Vector2 nextRoutePreviewStartFacingDirection =
            routeStartFacingDirection;

        float nextRouteDistanceTravelled =
            0f;

        float nextTurnFactor =
            1f;

        float nextSpeedFactor =
            1f;

        bool usedPreservedLockedPrefix = false;

        if (preserveLockedPrefix &&
            TryBuildRouteWithPreservedMovingDestinationPrefix(
                liveDestinationPosition,
                out routeClassification,
                out nextStateStartPosition,
                out nextRoutePreviewStartFacingDirection,
                out nextRouteDistanceTravelled,
                out nextTurnFactor,
                out nextSpeedFactor))
        {
            usedPreservedLockedPrefix = true;
        }
        else
        {
            if (!TryBuildPlayerShipRoutePath2A(
                    routeStartPosition,
                    liveDestinationPosition,
                    routeStartFacingDirection,
                    _routeSegmentPathBuffer,
                    out nextTurnFactor,
                    out nextSpeedFactor))
            {
                _routeTurnAdjustmentFactor = previousTurnFactor;
                _routeSpeedAdjustmentFactor = previousSpeedFactor;
                _useRouteInitialSpeedLimit = previousUseInitialSpeedLimit;
                _routeInitialSpeedLimitFactor = previousInitialSpeedLimitFactor;
                _routeInitialSpeedLimitDistance = previousInitialSpeedLimitDistance;
                _consumeSunFacingStartTurnTick = previousConsumeSunFacingStartTurnTick;
                _useFallbackStartTurnInPlaceRoute = previousUseFallbackStartTurnInPlaceRoute;
                _useSunFacingStartTurnInPlaceRoute = previousUseSunFacingStartTurnInPlaceRoute;
                _fallbackStartTurnInPlaceDirection = previousFallbackStartTurnInPlaceDirection;
                _sunFacingStartTurnInPlaceDirection = previousSunFacingStartTurnInPlaceDirection;
                _activeRouteDestinationCase = previousActiveRouteDestinationCase;

                return false;
            }
        }

        if (_routeSegmentPathBuffer.Count <= 1)
        {
            _routeTurnAdjustmentFactor = previousTurnFactor;
            _routeSpeedAdjustmentFactor = previousSpeedFactor;
            _useRouteInitialSpeedLimit = previousUseInitialSpeedLimit;
            _routeInitialSpeedLimitFactor = previousInitialSpeedLimitFactor;
            _routeInitialSpeedLimitDistance = previousInitialSpeedLimitDistance;
            _consumeSunFacingStartTurnTick = previousConsumeSunFacingStartTurnTick;
            _useFallbackStartTurnInPlaceRoute = previousUseFallbackStartTurnInPlaceRoute;
            _useSunFacingStartTurnInPlaceRoute = previousUseSunFacingStartTurnInPlaceRoute;
            _fallbackStartTurnInPlaceDirection = previousFallbackStartTurnInPlaceDirection;
            _sunFacingStartTurnInPlaceDirection = previousSunFacingStartTurnInPlaceDirection;
            _activeRouteDestinationCase = previousActiveRouteDestinationCase;

            return false;
        }

        CopyRoutePath(
            _routeSegmentPathBuffer,
            _activeTravelRoutePath);

        State.StartPosition =
            nextStateStartPosition;

        State.DestinationPosition =
            _activeTravelRoutePath[_activeTravelRoutePath.Count - 1];

        if (State.Destination != null)
        {
            State.Destination.FixedMapPosition =
                State.DestinationPosition;
        }

        State.TravelDistance =
            GetPathLength(_activeTravelRoutePath);

        State.TravelProgress01 =
            State.TravelDistance > 0f
                ? Mathf.Clamp01(nextRouteDistanceTravelled / State.TravelDistance)
                : 0f;

        _activeRouteDistanceTravelled =
            Mathf.Clamp(
                nextRouteDistanceTravelled,
                0f,
                State.TravelDistance);

        _activeRouteBuildQuantTick =
            quantTick;

        _routePreviewStartFacingDirection =
            nextRoutePreviewStartFacingDirection;

        _routeTurnAdjustmentFactor =
            Mathf.Clamp01(nextTurnFactor);

        _routeSpeedAdjustmentFactor =
            Mathf.Clamp01(nextSpeedFactor);

        _activeRouteDestinationCase =
            routeClassification.DestinationCase;

        RebuildActiveTravelRoutePlan(
            routeClassification,
            State.StartPosition,
            State.DestinationPosition,
            _routePreviewStartFacingDirection);

        if (!IsMovingTravelDestination())
        {
            ApplyShortRouteSpeedAdjustment();
        }

        if (!usedPreservedLockedPrefix)
        {
            ConfigureRouteInitialSpeedLimit(
                routeClassification,
                routeStartPosition,
                routeStartFacingDirection);
        }

        return true;
    }

    private bool ShouldAlwaysRefreshMovingDestinationRoute()
    {
        return State != null &&
               State.Destination != null &&
               State.Destination.Type == TravelDestinationType.Npc;
    }

    private bool ShouldPreserveMovingDestinationLockedPrefix(
        int quantTick)
    {
        if (State == null ||
            State.Destination == null ||
            State.Status != SystemTravelStatus.Flying)
        {
            return false;
        }

        if (State.Destination.Type != TravelDestinationType.Planet)
            return false;

        return IsMovingDestinationInitialRouteRefreshBlocked(
            quantTick);
    }

    private bool TryBuildRouteWithPreservedMovingDestinationPrefix(
    Vector3 liveDestinationPosition,
    out RouteDestinationClassification routeClassification,
    out Vector3 nextStateStartPosition,
    out Vector2 nextRoutePreviewStartFacingDirection,
    out float nextRouteDistanceTravelled,
    out float nextTurnFactor,
    out float nextSpeedFactor)
    {
        routeClassification =
            default;

        nextStateStartPosition =
            State.StartPosition;

        nextRoutePreviewStartFacingDirection =
            _routePreviewStartFacingDirection;

        nextRouteDistanceTravelled =
            _activeRouteDistanceTravelled;

        nextTurnFactor =
            1f;

        nextSpeedFactor =
            1f;

        if (!TryGetMovingDestinationLockedPrefixState(
                out float lockedPrefixDistance,
                out Vector3 lockedPrefixEndPosition,
                out Vector2 lockedPrefixEndFacingDirection))
        {
            return false;
        }

        routeClassification =
            ClassifyRouteDestination(
                lockedPrefixEndPosition,
                liveDestinationPosition,
                lockedPrefixEndFacingDirection);

        if (routeClassification.DestinationCase ==
            RouteDestinationCase.ForbiddenDestination)
        {
            return false;
        }

        if (!TryBuildPlayerShipRoutePath2A(
                lockedPrefixEndPosition,
                liveDestinationPosition,
                lockedPrefixEndFacingDirection,
                _routeProbePathBuffer,
                out nextTurnFactor,
                out nextSpeedFactor))
        {
            return false;
        }

        BuildRoutePrefix(
            _activeTravelRoutePath,
            lockedPrefixDistance,
            _routeSegmentPathBuffer);

        AppendRouteTail(
            _routeProbePathBuffer,
            _routeSegmentPathBuffer);

        nextStateStartPosition =
            State.StartPosition;

        nextRoutePreviewStartFacingDirection =
            _routePreviewStartFacingDirection;

        nextRouteDistanceTravelled =
            Mathf.Min(
                _activeRouteDistanceTravelled,
                lockedPrefixDistance);

        return _routeSegmentPathBuffer.Count > 1;
    }

    private bool TryBuildPlayerShipRoutePath2A(
    Vector3 routeStartPosition,
    Vector3 destinationPosition,
    Vector2 routeStartFacingDirection,
    List<Vector3> routePath,
    out float turnFactor,
    out float speedFactor)
    {
        turnFactor = 1f;
        speedFactor = 1f;

        if (routePath == null)
            return false;

        routePath.Clear();

        if (_shipRouteService == null)
            return false;

        SystemShipRouteRequest2A routeRequest =
            new SystemShipRouteRequest2A
            {
                SystemId = GetCurrentPlayerSystemId(),
                StartPosition = routeStartPosition,
                DestinationPosition = destinationPosition,
                StartFacingDirection = routeStartFacingDirection,
                TargetKind = GetCurrentShipRouteTargetKind2A(),
                Settings = CreatePlayerShipRouteSettings2A()
            };

        bool routeBuilt =
            _shipRouteService.TryBuildRoute(
                routeRequest,
                _playerShipRouteBuildResult);

        if (!routeBuilt ||
            _playerShipRouteBuildResult.Path == null ||
            _playerShipRouteBuildResult.Path.Count <= 1)
        {
            return false;
        }

        CopyRoutePath(
            _playerShipRouteBuildResult.Path,
            routePath);

        turnFactor =
            Mathf.Clamp01(
                _playerShipRouteBuildResult.TurnRadiusFactor);

        speedFactor =
            Mathf.Clamp01(
                _playerShipRouteBuildResult.SpeedFactor);

        return routePath.Count > 1;
    }

    private bool TryGetMovingDestinationLockedPrefixState(
        out float lockedPrefixDistance,
        out Vector3 lockedPrefixEndPosition,
        out Vector2 lockedPrefixEndFacingDirection)
    {
        lockedPrefixDistance = 0f;
        lockedPrefixEndPosition = Vector3.zero;
        lockedPrefixEndFacingDirection = Vector2.up;

        int blockedTicks =
            GetMovingDestinationRouteRefreshBlockedInitialTicks();

        if (blockedTicks <= 0)
            return false;

        if (_activeTravelRoutePath == null ||
            _activeTravelRoutePath.Count <= 1)
        {
            return false;
        }

        float totalPathLength =
            GetPathLength(_activeTravelRoutePath);

        if (totalPathLength <= ArrivalDistanceThreshold)
            return false;

        float baseDistancePerTick =
            Mathf.Max(0.01f, GetCurrentShipTravelSpeed()) *
            Mathf.Max(0.01f, GameTimeState.SecondsPerDay);

        lockedPrefixDistance =
            GetPreviewRouteDistanceAtTick(
                blockedTicks,
                baseDistancePerTick,
                totalPathLength,
                Mathf.Clamp01(_routeSpeedAdjustmentFactor),
                _activeTravelRoutePlan.HasTurnInPlaceAtStart,
                _useRouteInitialSpeedLimit,
                _routeInitialSpeedLimitFactor,
                _routeInitialSpeedLimitDistance);

        lockedPrefixDistance =
            Mathf.Clamp(
                lockedPrefixDistance,
                0f,
                totalPathLength);

        if (lockedPrefixDistance <= ArrivalDistanceThreshold)
            return false;

        if (_activeRouteDistanceTravelled >=
            lockedPrefixDistance - RouteSegmentEpsilon)
        {
            return false;
        }

        lockedPrefixEndPosition =
            GetPointOnPathAtDistance(
                _activeTravelRoutePath,
                lockedPrefixDistance);

        lockedPrefixEndFacingDirection =
            GetDirectionOnPathAtDistance(
                _activeTravelRoutePath,
                lockedPrefixDistance);

        if (lockedPrefixEndFacingDirection.sqrMagnitude <= RouteSegmentEpsilon)
            lockedPrefixEndFacingDirection = _travelFacingDirection;

        if (lockedPrefixEndFacingDirection.sqrMagnitude <= RouteSegmentEpsilon)
            lockedPrefixEndFacingDirection = GetCurrentShipFacingDirection();

        lockedPrefixEndFacingDirection =
            TurnRadiusRouteMath2A.NormalizeDirectionOrUp(
                lockedPrefixEndFacingDirection);

        return true;
    }

    private void BuildRoutePrefix(
        IReadOnlyList<Vector3> sourcePath,
        float prefixDistance,
        List<Vector3> destinationPath)
    {
        if (destinationPath == null)
            return;

        destinationPath.Clear();

        if (sourcePath == null ||
            sourcePath.Count == 0)
        {
            return;
        }

        destinationPath.Add(sourcePath[0]);

        if (sourcePath.Count == 1)
            return;

        float remainingDistance =
            Mathf.Max(0f, prefixDistance);

        for (int i = 1; i < sourcePath.Count; i++)
        {
            Vector3 from =
                sourcePath[i - 1];

            Vector3 to =
                sourcePath[i];

            float segmentDistance =
                Vector3.Distance(from, to);

            if (segmentDistance <= RouteSegmentEpsilon)
                continue;

            if (remainingDistance >= segmentDistance)
            {
                AddRoutePointIfDifferent(
                    destinationPath,
                    to);

                remainingDistance -= segmentDistance;
                continue;
            }

            float t =
                Mathf.Clamp01(
                    remainingDistance / segmentDistance);

            Vector3 prefixEnd =
                Vector3.Lerp(
                    from,
                    to,
                    t);

            AddRoutePointIfDifferent(
                destinationPath,
                prefixEnd);

            return;
        }
    }

    private void AppendRouteTail(
        IReadOnlyList<Vector3> tailPath,
        List<Vector3> destinationPath)
    {
        if (tailPath == null ||
            destinationPath == null ||
            tailPath.Count == 0)
        {
            return;
        }

        int startIndex =
            destinationPath.Count > 0
                ? 1
                : 0;

        for (int i = startIndex; i < tailPath.Count; i++)
        {
            AddRoutePointIfDifferent(
                destinationPath,
                tailPath[i]);
        }
    }

    private void AddRoutePointIfDifferent(
        List<Vector3> path,
        Vector3 point)
    {
        if (path == null)
            return;

        if (path.Count > 0 &&
            Vector3.Distance(
                path[path.Count - 1],
                point) <= RouteSegmentEpsilon)
        {
            return;
        }

        path.Add(point);
    }

    private bool IsActiveRouteRefreshBlockedByInitialTicks(
    int quantTick)
    {
        int blockedTicks =
            GetMovingDestinationRouteRefreshBlockedInitialTicks();

        if (blockedTicks <= 0)
            return false;

        if (_activeRouteBuildQuantTick < 0)
            return false;

        int passedTicks =
            quantTick - _activeRouteBuildQuantTick;

        if (passedTicks < 0)
            return false;

        int completedTicks =
            passedTicks;

        if (IsCurrentTravelFrameAtEndOfTick())
        {
            completedTicks += 1;
        }

        return completedTicks < blockedTicks;
    }


    private bool IsCurrentTravelFrameAtEndOfTick()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null ||
            !Bootstrapper.Instance.ServiceRegistry.TryGet<IGameTimeService>(
                out IGameTimeService gameTimeService) ||
            gameTimeService == null ||
            gameTimeService.State == null)
        {
            return false;
        }

        float secondsPerTick =
            Mathf.Max(
                0.01f,
                GameTimeState.SecondsPerDay);

        return gameTimeService.State.Accumulator >=
               secondsPerTick - 0.0001f;
    }



    private int GetMovingDestinationRouteRefreshBlockedInitialSlots()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        if (config == null)
            return 1;

        return config.MovingDestinationRouteRefreshBlockedInitialSlots;
    }

    private int GetCurrentTravelQuantTick()
    {
        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null &&
            Bootstrapper.Instance.ServiceRegistry.TryGet<IGameTimeService>(
                out IGameTimeService gameTimeService) &&
            gameTimeService != null)
        {
            return gameTimeService.CurrentQuantTick;
        }

        return -1;
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

        if (State.Status != SystemTravelStatus.DestinationSelected &&
            State.Status != SystemTravelStatus.Flying)
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

        float baseSpeed =
            Mathf.Max(
                0.01f,
                GetCurrentShipTravelSpeed());

        float baseDistancePerTick =
            baseSpeed *
            safeSecondsPerTick;

        if (State.Destination != null &&
            (State.Destination.Type == TravelDestinationType.Planet ||
             State.Destination.Type == TravelDestinationType.Npc))
        {
            BuildLivePlanetRoutePreview2A(
                preview,
                safeSmallDotSpacing,
                safeMaxBigDots,
                safeMaxSmallDots,
                baseDistancePerTick);
        }
        else
        {
            BuildStaticRoutePreview2A(
                preview,
                safeSmallDotSpacing,
                safeMaxBigDots,
                safeMaxSmallDots,
                baseDistancePerTick);
        }

        return preview;
    }

    private void BuildStaticRoutePreview2A(
    TravelRoutePreview2A preview,
    float smallDotSpacing,
    int maxBigDots,
    int maxSmallDots,
    float baseDistancePerTick)
    {
        BuildRoutePreview2A(
            preview,
            smallDotSpacing,
            maxBigDots,
            maxSmallDots,
            baseDistancePerTick);
    }

    private void BuildLivePlanetRoutePreview2A(
    TravelRoutePreview2A preview,
    float smallDotSpacing,
    int maxBigDots,
    int maxSmallDots,
    float baseDistancePerTick)
    {
        BuildRoutePreview2A(
            preview,
            smallDotSpacing,
            maxBigDots,
            maxSmallDots,
            baseDistancePerTick);
    }

    private void BuildRoutePreview2A(
TravelRoutePreview2A preview,
float smallDotSpacing,
int maxBigDots,
int maxSmallDots,
float baseDistancePerTick)
    {
        Vector3 currentPosition =
            GetCurrentShipPosition();

        Vector3 destinationPosition =
            GetCurrentDestinationPosition();

        float distanceToDestination =
            Vector3.Distance(
                currentPosition,
                destinationPosition);

        if (distanceToDestination <= ArrivalDistanceThreshold)
            return;

        bool isFlying =
            State.Status == SystemTravelStatus.Flying;

        Vector3 routeStartPosition =
            isFlying
                ? State.StartPosition
                : currentPosition;

        Vector2 routeStartFacingDirection =
            isFlying
                ? _routePreviewStartFacingDirection
                : GetCurrentShipFacingDirection();

        RouteDestinationClassification routeClassification =
            ClassifyRouteDestination(
                routeStartPosition,
                destinationPosition,
                routeStartFacingDirection);

        RouteAdjustment routeAdjustment =
            isFlying
                ? new RouteAdjustment(
                    _routeTurnAdjustmentFactor,
                    _routeSpeedAdjustmentFactor)
                : ResolveRouteAdjustment(
                    routeStartPosition,
                    destinationPosition,
                    routeStartFacingDirection);

        if (isFlying &&
            _activeTravelRoutePath.Count > 1)
        {
            _routePreviewPathBuffer.Clear();
            _routePreviewPathBuffer.AddRange(_activeTravelRoutePath);
        }
        else if (routeAdjustment.HasBuiltRoute &&
                 _routeProbePathBuffer.Count > 1)
        {
            _routePreviewPathBuffer.Clear();
            _routePreviewPathBuffer.AddRange(_routeProbePathBuffer);
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
                routeAdjustment.SpeedFactor,
                routeAdjustment.MaxAllowedRouteLength);
        }

        float totalPathLength =
            GetPathLength(
                _routePreviewPathBuffer);

        if (totalPathLength <= ArrivalDistanceThreshold)
            return;

        float passedDistance =
            isFlying
                ? _activeRouteDistanceTravelled
                : GetClosestDistanceOnPath(
                    _routePreviewPathBuffer,
                    currentPosition);

        passedDistance =
            Mathf.Clamp(
                passedDistance,
                0f,
                totalPathLength);

        ResolveRoutePreviewSpeedSchedule(
            routeClassification,
            routeStartPosition,
            routeStartFacingDirection,
            isFlying,
            out bool hasStartTurnTick,
            out bool useInitialSpeedLimit,
            out float initialSpeedLimitFactor,
            out float initialSpeedLimitDistance);

        float routeSpeedFactor =
            Mathf.Clamp01(
                routeAdjustment.SpeedFactor);

        float currentTickRemainingFactor =
            isFlying
                ? GetCurrentTickRemainingFactor()
                : 1f;

        for (int tickIndex = 1; tickIndex <= maxBigDots; tickIndex++)
        {
            float intervalStartDistance =
                isFlying
                    ? GetFlyingPreviewRouteDistanceAtTick(
                        tickIndex - 1,
                        passedDistance,
                        baseDistancePerTick,
                        totalPathLength,
                        routeSpeedFactor,
                        hasStartTurnTick,
                        useInitialSpeedLimit,
                        initialSpeedLimitFactor,
                        initialSpeedLimitDistance,
                        currentTickRemainingFactor)
                    : GetPreviewRouteDistanceAtTick(
                        tickIndex - 1,
                        baseDistancePerTick,
                        totalPathLength,
                        routeSpeedFactor,
                        hasStartTurnTick,
                        useInitialSpeedLimit,
                        initialSpeedLimitFactor,
                        initialSpeedLimitDistance);

            float distanceAtTick =
                isFlying
                    ? GetFlyingPreviewRouteDistanceAtTick(
                        tickIndex,
                        passedDistance,
                        baseDistancePerTick,
                        totalPathLength,
                        routeSpeedFactor,
                        hasStartTurnTick,
                        useInitialSpeedLimit,
                        initialSpeedLimitFactor,
                        initialSpeedLimitDistance,
                        currentTickRemainingFactor)
                    : GetPreviewRouteDistanceAtTick(
                        tickIndex,
                        baseDistancePerTick,
                        totalPathLength,
                        routeSpeedFactor,
                        hasStartTurnTick,
                        useInitialSpeedLimit,
                        initialSpeedLimitFactor,
                        initialSpeedLimitDistance);

            if (isFlying &&
                distanceAtTick <= passedDistance + 0.001f)
            {
                continue;
            }

            AddSmallRoutePreviewDots2A(
                preview,
                _routePreviewPathBuffer,
                isFlying
                    ? Mathf.Max(intervalStartDistance, passedDistance)
                    : intervalStartDistance,
                distanceAtTick,
                passedDistance,
                tickIndex,
                smallDotSpacing,
                maxSmallDots);

            Vector3 tickPosition =
                GetPointOnPathAtDistance(
                    _routePreviewPathBuffer,
                    distanceAtTick);

            preview.AddBigDot(
                tickPosition,
                tickIndex);

            if (distanceAtTick >= totalPathLength)
                break;
        }
    }


    private float GetCurrentTickRemainingFactor()
    {
        float secondsPerTick =
            Mathf.Max(
                0.01f,
                GameTimeState.SecondsPerDay);

        float accumulator = 0f;

        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null &&
            Bootstrapper.Instance.ServiceRegistry.TryGet<IGameTimeService>(
                out IGameTimeService gameTimeService) &&
            gameTimeService != null &&
            gameTimeService.State != null)
        {
            accumulator =
                gameTimeService.State.Accumulator;
        }

        float elapsedFactor =
            Mathf.Clamp01(
                accumulator /
                secondsPerTick);

        return Mathf.Clamp01(
            1f - elapsedFactor);
    }

    private float GetFlyingPreviewRouteDistanceAtTick(
        int tickIndex,
        float passedDistance,
        float baseDistancePerTick,
        float totalPathLength,
        float routeSpeedFactor,
        bool hasStartTurnTick,
        bool useInitialSpeedLimit,
        float initialSpeedLimitFactor,
        float initialSpeedLimitDistance,
        float currentTickRemainingFactor)
    {
        float distance =
            Mathf.Clamp(
                passedDistance,
                0f,
                totalPathLength);

        if (tickIndex <= 0)
            return distance;

        for (int currentTick = 1; currentTick <= tickIndex; currentTick++)
        {
            if (hasStartTurnTick &&
                currentTick == 1)
            {
                continue;
            }

            float tickDistance =
                currentTick == 1
                    ? baseDistancePerTick *
                      Mathf.Clamp01(currentTickRemainingFactor)
                    : baseDistancePerTick;

            distance =
                AdvancePreviewRouteDistanceForOneTick(
                    distance,
                    tickDistance,
                    totalPathLength,
                    routeSpeedFactor,
                    useInitialSpeedLimit,
                    initialSpeedLimitFactor,
                    initialSpeedLimitDistance);

            if (distance >= totalPathLength)
                return totalPathLength;
        }

        return Mathf.Clamp(
            distance,
            0f,
            totalPathLength);
    }

    private void ResolveRoutePreviewSpeedSchedule(
    RouteDestinationClassification routeClassification,
    Vector3 routeStartPosition,
    Vector2 routeStartFacingDirection,
    bool isFlying,
    out bool hasStartTurnTick,
    out bool useInitialSpeedLimit,
    out float initialSpeedLimitFactor,
    out float initialSpeedLimitDistance)
    {
        if (isFlying)
        {
            hasStartTurnTick = _consumeSunFacingStartTurnTick;
            useInitialSpeedLimit = _useRouteInitialSpeedLimit;
            initialSpeedLimitFactor = _routeInitialSpeedLimitFactor;
            initialSpeedLimitDistance = _routeInitialSpeedLimitDistance;
            return;
        }

        hasStartTurnTick = false;
        useInitialSpeedLimit = false;
        initialSpeedLimitFactor = 1f;
        initialSpeedLimitDistance = 0f;

        if (ShouldUseBehindSmallTurn(routeClassification))
        {
            float slowDistance =
                GetBehindSmallTurnArcLength(
                    routeClassification);

            useInitialSpeedLimit = true;
            initialSpeedLimitFactor =
                Mathf.Max(
                    GetMinRouteTurnRadiusAdjustmentFactor(),
                    GetSpeedFactorForOneTickDistance(slowDistance));

            initialSpeedLimitDistance =
                slowDistance;

            return;
        }

        if (IsNearSunStartFacingSun(
                routeClassification,
                routeStartPosition,
                routeStartFacingDirection))
        {
            hasStartTurnTick = true;
            initialSpeedLimitFactor =
                GetMinRouteTurnRadiusAdjustmentFactor();
        }
    }

    private float GetPreviewRouteDistanceAtTick(
    int tickIndex,
    float baseDistancePerTick,
    float totalPathLength,
    float routeSpeedFactor,
    bool hasStartTurnTick,
    bool useInitialSpeedLimit,
    float initialSpeedLimitFactor,
    float initialSpeedLimitDistance)
    {
        if (tickIndex <= 0)
            return 0f;

        float distance =
            0f;

        for (int currentTick = 1; currentTick <= tickIndex; currentTick++)
        {
            if (hasStartTurnTick &&
                currentTick == 1)
            {
                continue;
            }

            distance =
                AdvancePreviewRouteDistanceForOneTick(
                    distance,
                    baseDistancePerTick,
                    totalPathLength,
                    routeSpeedFactor,
                    useInitialSpeedLimit,
                    initialSpeedLimitFactor,
                    initialSpeedLimitDistance);

            if (distance >= totalPathLength)
                return totalPathLength;
        }

        return Mathf.Clamp(
            distance,
            0f,
            totalPathLength);
    }

    private float AdvancePreviewRouteDistanceForOneTick(
    float currentDistance,
    float baseDistancePerTick,
    float totalPathLength,
    float routeSpeedFactor,
    bool useInitialSpeedLimit,
    float initialSpeedLimitFactor,
    float initialSpeedLimitDistance)
    {
        float safeBaseDistancePerTick =
            Mathf.Max(
                0.01f,
                baseDistancePerTick);

        float normalSpeedFactor =
            Mathf.Max(
                0.0001f,
                Mathf.Clamp01(routeSpeedFactor));

        if (!useInitialSpeedLimit ||
            initialSpeedLimitDistance <= RouteSegmentEpsilon ||
            currentDistance >= initialSpeedLimitDistance - RouteSegmentEpsilon)
        {
            return Mathf.Min(
                totalPathLength,
                currentDistance +
                safeBaseDistancePerTick *
                normalSpeedFactor);
        }

        float limitedSpeedFactor =
            Mathf.Min(
                normalSpeedFactor,
                Mathf.Max(
                    0.0001f,
                    Mathf.Clamp01(initialSpeedLimitFactor)));

        float distanceToInitialLimit =
            Mathf.Max(
                0f,
                initialSpeedLimitDistance - currentDistance);

        float limitedDistancePerTick =
            safeBaseDistancePerTick *
            limitedSpeedFactor;

        if (distanceToInitialLimit >= limitedDistancePerTick)
        {
            return Mathf.Min(
                totalPathLength,
                currentDistance +
                limitedDistancePerTick);
        }

        float usedTickPart =
            limitedDistancePerTick > RouteSegmentEpsilon
                ? distanceToInitialLimit / limitedDistancePerTick
                : 1f;

        float remainingTickPart =
            Mathf.Clamp01(
                1f - usedTickPart);

        return Mathf.Min(
            totalPathLength,
            initialSpeedLimitDistance +
            safeBaseDistancePerTick *
            normalSpeedFactor *
            remainingTickPart);
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

        float firstDotDistance =
            Mathf.Floor(safeStartDistance / safeSpacing) *
            safeSpacing +
            safeSpacing;

        for (
            float dotDistance = firstDotDistance;
            dotDistance < safeEndDistance - 0.001f;
            dotDistance += safeSpacing)
        {
            if (preview.SmallDotCount >= maxSmallDots)
                return;

            if (dotDistance <= passedDistance + 0.001f)
                continue;

            Vector3 position =
                GetPointOnPathAtDistance(
                    path,
                    dotDistance);

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
        BuildDirectTravelPath(
            from,
            to,
            _directTravelPathBuffer);

        SunAvoidanceObstacle obstacle =
            GetSunAvoidanceObstacle(
                from.z,
                to);

        BuildSunDangerAwareTravelPathFromRawRoute(
            _directTravelPathBuffer,
            path,
            GetCurrentShipFacingDirection(),
            GetCurrentShipTurnRadius(),
            obstacle);
    }

    private void BuildSunDangerAwareTravelPathFromRawRoute(
    IReadOnlyList<Vector3> rawRoute,
    List<Vector3> path,
    Vector2 startFacingDirection,
    float turnRadius,
    SunAvoidanceObstacle obstacle)
    {
        if (path == null)
            return;

        if (rawRoute == null ||
            rawRoute.Count == 0)
        {
            path.Clear();
            return;
        }

        if (!obstacle.HasObstacle ||
            obstacle.Radius <= 0f)
        {
            path.Clear();

            for (int i = 0; i < rawRoute.Count; i++)
                path.Add(rawRoute[i]);

            return;
        }

        SystemTravelSunAvoidancePath2A.TryBuildPathAroundDangerZoneFromRawRoute(
            path,
            rawRoute,
            obstacle.Center,
            obstacle.Radius,
            SunAvoidanceArcSegments,
            startFacingDirection,
            turnRadius);
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
        Vector2 startFacingDirection,
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
                startFacingDirection,
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
        Vector2 startFacingDirection,
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
                startFacingDirection,
                obstacle);

        SunStartTangentEscapeOption optionB =
            GetSunStartTangentEscapeOption(
                from,
                to,
                center,
                tangentB,
                tangentDistance,
                startFacingDirection,
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
        Vector2 startFacingDirection,
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
                startFacingDirection,
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
                    startFacingDirection,
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
        Vector2 startFacingDirection,
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

        Vector2 facingDirection =
    TurnRadiusRouteMath2A.NormalizeDirectionOrUp(
        startFacingDirection);

        float tangentFacingAngle =
            Vector2.Angle(
                facingDirection,
                tangentDirection);

        float score =
            Vector2.Distance(tangentExit, to2) +
            tangentFacingAngle * obstacle.Radius;

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
        BuildDirectTravelPath(
            from,
            to,
            _directTravelPathBuffer);

        BuildSunDangerAwareTravelPathFromRawRoute(
            _directTravelPathBuffer,
            path,
            startFacingDirection,
            turnRadius,
            obstacle);
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
    float turnRadius)
    {
        if (path == null)
            return;

        path.Clear();

        StarSystemConfig currentSystem =
            _configService.GetCurrentSystemConfig();

        if (currentSystem == null || currentSystem.Sun == null)
        {
            path.Add(from);
            path.Add(to);
            return;
        }

        SunConfig sun =
            currentSystem.Sun;

        Vector3 sunCenter =
            new Vector3(
                sun.LocalOffset.x,
                sun.LocalOffset.y,
                from.z);

        float sunRadius =
            Mathf.Max(
                0f,
                GetSunWorldSize(sun) * 0.5f);

        float unifiedSafeRadius =
            GetUnifiedSunSafeRadius(sunRadius);

        SystemTravelSunAvoidancePath2A.BuildPath(
            path,
            from,
            to,
            sunCenter,
            unifiedSafeRadius,
            SunAvoidanceArcSegments,
            forceAvoidance,
            startFacingDirection,
            turnRadius);
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

    private SunAvoidanceObstacle GetSunAvoidanceObstacle(float z, Vector3? destinationPosition)
    {
        StarSystemConfig currentSystem =
            _configService.GetCurrentSystemConfig();

        if (currentSystem == null || currentSystem.Sun == null)
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

        float unifiedSafeRadius =
            GetUnifiedSunSafeRadius(sunRadius);

        return new SunAvoidanceObstacle(
            true,
            sunCenter,
            unifiedSafeRadius,
            unifiedSafeRadius);
    }

    private static float GetRoutePlanningAvoidanceRadius(
    Vector3 destinationPosition,
    Vector3 sunCenter,
    float sunRadius,
    float minimumSafeRadius)
    {
        return Mathf.Max(
            sunRadius + SunCollisionSafetyMargin,
            minimumSafeRadius);
    }

    private float GetUnifiedSunSafeRadius(float sunRadius)
    {
        return Mathf.Max(
            sunRadius + SunCollisionSafetyMargin,
            sunRadius * GetSunDestinationForbiddenRadiusMultiplier());
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
        if (IsNearSunStartDestinationCase(destinationCase))
        {
            return RoutePathAvoidsSunBody(
                routePath,
                obstacle);
        }

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
        return IsNearSunStartDestinationCase(destinationCase);
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
    float speed,
    float minimumMaxAllowedRouteLength = 0f)
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

        Vector2 effectiveRouteStartFacingDirection =
            routeStartFacingDirection;

        if (IsNearSunStartTowardSunDestinationCase(
                routeClassification.DestinationCase))
        {
            effectiveRouteStartFacingDirection =
                GetSunStartLocalTangentDirection(
                    routeStartPosition,
                    destinationPosition,
                    routeStartFacingDirection,
                    obstacle);
        }

        bool directTravelPathNeedsSunAvoidance =
            routeClassification.RequiresSunAvoidance ||
            RouteCaseUsesSunAvoidance(
                routeClassification.DestinationCase);

        bool useNearSunTangentBehindAwayPath =
            ShouldUseNearSunTangentBehindAwayPath(
                routeClassification,
                routeStartPosition,
                effectiveRouteStartFacingDirection);

        if (TryRebuildRoutePathFromWaypoints(
                routePath,
                _directTravelPathBuffer,
                effectiveRouteStartFacingDirection,
                turnRadius,
                speed,
                routeStepDistance,
                "RebuildTravelRoutePath.Direct",
                obstacle,
                minimumMaxAllowedRouteLength) &&
            RoutePathAvoidsSunForWaypoints(
                routePath,
                _directTravelPathBuffer,
                obstacle,
                routeClassification.DestinationCase))
        {
            return;
        }

        if (useNearSunTangentBehindAwayPath)
        {
            BuildNearSunTangentBehindAwayTravelPath(
                routeStartPosition,
                destinationPosition,
                _travelPathBuffer,
                obstacle,
                turnRadius);

            if (TryRebuildRoutePathFromWaypoints(
                    routePath,
                    _travelPathBuffer,
                    effectiveRouteStartFacingDirection,
                    turnRadius,
                    speed,
                    routeStepDistance,
                    "RebuildTravelRoutePath.NearSunTangentBehindAway",
                    obstacle,
                    minimumMaxAllowedRouteLength) &&
                RoutePathAvoidsSunBody(
                    routePath,
                    obstacle))
            {
                return;
            }
        }

        if (!directTravelPathNeedsSunAvoidance)
            return;

        BuildSunAdjustmentTravelPath(
            routeStartPosition,
            destinationPosition,
            _travelPathBuffer,
            effectiveRouteStartFacingDirection,
            turnRadius,
            obstacle,
            routeClassification.DestinationCase);

        TryRebuildRoutePathFromWaypoints(
            routePath,
            _travelPathBuffer,
            effectiveRouteStartFacingDirection,
            turnRadius,
            speed,
            routeStepDistance,
            "RebuildTravelRoutePath.SunAvoidance",
            obstacle,
            minimumMaxAllowedRouteLength);
    }

    private bool TryRebuildRoutePathFromWaypoints(
    List<Vector3> routePath,
    IReadOnlyList<Vector3> travelPath,
    Vector2 routeStartFacingDirection,
    float turnRadius,
    float speed,
    float routeStepDistance,
    string logPhase,
    SunAvoidanceObstacle obstacle,
    float minimumMaxAllowedRouteLength = 0f)
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
                intermediateWaypointArrivalDistanceThreshold,
                GetRouteStraightExitAngleDegrees());

        float routeLength =
            GetPathLength(routePath);

        float maxAllowedRouteLength =
            GetMaxAllowedRouteLength(
                travelPath,
                travelPathLength,
                turnRadius,
                routeStepDistance,
                routeStartFacingDirection);

        if (minimumMaxAllowedRouteLength > 0f)
        {
            maxAllowedRouteLength =
                Mathf.Max(
                    maxAllowedRouteLength,
                    minimumMaxAllowedRouteLength);
        }

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

        Vector2 tangentRouteDirection =
            GetDirectionFromPoints(
                routeStart,
                tangentPoint,
                routeStartFacingDirection);

        Vector2 firstSegmentFacingDirection =
            IsNearSunStartTowardSunDestinationCase(_activeRouteDestinationCase)
                ? tangentRouteDirection
                : routeStartFacingDirection;

        bool firstBuilt =
            TryRebuildRoutePathFromWaypoints(
                routePath,
                _routeSegmentWaypointsBuffer,
                firstSegmentFacingDirection,
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
                tangentRouteDirection);

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
            speed > 0f
                ? speed
                : GetCurrentShipTravelSpeed();

        int substepsPerTick =
            GetRouteSubstepsPerTick();

        float distancePerSubstep =
            planningSpeed /
            Mathf.Max(1, substepsPerTick);

        return Mathf.Max(
            ArrivalDistanceThreshold,
            distancePerSubstep);
    }

    private bool TryBuildSunSafeRouteFromBuiltRoute(
    List<Vector3> routePath,
    IReadOnlyList<Vector3> builtRoutePath,
    Vector2 routeStartFacingDirection,
    float turnRadius,
    float speed,
    string logPhase,
    SunAvoidanceObstacle obstacle,
    float minimumMaxAllowedRouteLength = 0f)
    {
        if (routePath == null)
            return false;

        routePath.Clear();

        if (builtRoutePath == null ||
            builtRoutePath.Count <= 1)
        {
            return false;
        }

        float paddingStep =
            GetSunAvoidanceRoutePaddingStep();

        float paddingMax =
            GetSunAvoidanceRoutePaddingMax();

        int attemptCount =
            paddingStep > RouteSegmentEpsilon
                ? Mathf.CeilToInt(paddingMax / paddingStep)
                : 0;

        for (int attempt = 0; attempt <= attemptCount; attempt++)
        {
            float padding =
                attempt == attemptCount
                    ? paddingMax
                    : paddingStep * attempt;

            SunAvoidanceObstacle planningObstacle =
                CreateSunRoutePlanningObstacle(
                    obstacle,
                    padding);

            string attemptLogPhase =
                padding > RouteSegmentEpsilon
                    ? logPhase + ".Padding" + padding.ToString("0.###")
                    : logPhase;

            if (TryBuildSunSafeRouteFromBuiltRouteOnce(
                    routePath,
                    builtRoutePath,
                    routeStartFacingDirection,
                    turnRadius,
                    speed,
                    attemptLogPhase,
                    planningObstacle,
                    obstacle,
                    minimumMaxAllowedRouteLength))
            {
                return true;
            }
        }

        routePath.Clear();
        routePath.Add(builtRoutePath[0]);
        return false;
    }

    private bool TryBuildSunSafeRouteFromBuiltRouteOnce(
    List<Vector3> routePath,
    IReadOnlyList<Vector3> builtRoutePath,
    Vector2 routeStartFacingDirection,
    float turnRadius,
    float speed,
    string logPhase,
    SunAvoidanceObstacle planningObstacle,
    SunAvoidanceObstacle validationObstacle,
    float minimumMaxAllowedRouteLength = 0f)
    {
        routePath.Clear();

        Vector3 routeStart =
            builtRoutePath[0];

        Vector3 routeDestination =
            builtRoutePath[builtRoutePath.Count - 1];

        float routeStepDistance =
            GetRoutePlanStepDistance(speed);

        List<Vector3> bestRoute =
            null;

        float bestRouteLength =
            float.MaxValue;

        List<Vector3> simpleTravelPath =
            new List<Vector3>(32);

        BuildSunAdjustmentTravelPath(
            routeStart,
            routeDestination,
            simpleTravelPath,
            routeStartFacingDirection,
            turnRadius,
            planningObstacle,
            _activeRouteDestinationCase);

        List<Vector3> simpleSmoothedRoute =
            new List<Vector3>(128);

        bool simpleSmoothedBuilt =
            TryRebuildRoutePathFromWaypoints(
                simpleSmoothedRoute,
                simpleTravelPath,
                routeStartFacingDirection,
                turnRadius,
                speed,
                routeStepDistance,
                logPhase + ".SimpleSmoothed",
                validationObstacle,
                minimumMaxAllowedRouteLength);

        if (simpleSmoothedBuilt &&
            RoutePathAvoidsSunBody(
                simpleSmoothedRoute,
                validationObstacle))
        {
            bestRoute =
                simpleSmoothedRoute;

            bestRouteLength =
                GetPathLength(
                    simpleSmoothedRoute);
        }

        BuildSunDangerAwareTravelPathFromRawRoute(
            builtRoutePath,
            _travelPathBuffer,
            routeStartFacingDirection,
            turnRadius,
            planningObstacle);

        if (_travelPathBuffer.Count > 1)
        {
            List<Vector3> rawSmoothedRoute =
                new List<Vector3>(128);

            bool rawSmoothedBuilt =
                TryRebuildRoutePathFromWaypoints(
                    rawSmoothedRoute,
                    _travelPathBuffer,
                    routeStartFacingDirection,
                    turnRadius,
                    speed,
                    routeStepDistance,
                    logPhase + ".Smoothed",
                    validationObstacle,
                    minimumMaxAllowedRouteLength);

            if (rawSmoothedBuilt &&
                RoutePathAvoidsSunBody(
                    rawSmoothedRoute,
                    validationObstacle))
            {
                float rawSmoothedRouteLength =
                    GetPathLength(
                        rawSmoothedRoute);

                if (rawSmoothedRouteLength < bestRouteLength)
                {
                    bestRoute =
                        rawSmoothedRoute;

                    bestRouteLength =
                        rawSmoothedRouteLength;
                }
            }
        }

        if (bestRoute != null &&
            bestRoute.Count > 1)
        {
            routePath.Clear();

            for (int i = 0; i < bestRoute.Count; i++)
                routePath.Add(bestRoute[i]);

            return true;
        }

        routePath.Clear();

        if (_travelPathBuffer.Count > 0)
        {
            for (int i = 0; i < _travelPathBuffer.Count; i++)
                routePath.Add(_travelPathBuffer[i]);
        }

        float rawRouteLength =
            GetPathLength(routePath);

        float maxAllowedRouteLength =
            GetMaxAllowedRouteLength(
                routePath,
                rawRouteLength,
                turnRadius,
                routeStepDistance,
                routeStartFacingDirection);

        if (minimumMaxAllowedRouteLength > 0f)
        {
            maxAllowedRouteLength =
                Mathf.Max(
                    maxAllowedRouteLength,
                    minimumMaxAllowedRouteLength);
        }

        bool rawRouteAvoidsSun =
            RoutePathAvoidsSunBody(
                routePath,
                validationObstacle);

        LogMapPointRouteBuildTrace(
            logPhase + ".RawFallbackRejected",
            routeStart,
            routeDestination,
            routeStartFacingDirection,
            turnRadius,
            speed,
            routeStepDistance,
            0,
            0f,
            maxAllowedRouteLength,
            rawRouteAvoidsSun,
            false,
            _travelPathBuffer,
            routePath);

        routePath.Clear();
        routePath.Add(routeStart);

        return false;
    }

    // private bool ShouldUseStartTurnInPlace(
    // RouteDestinationClassification classification)
    // {
    //     if (IsNearSunStartTowardSunDestinationCase(
    //             classification.DestinationCase))
    //     {
    //         return true;
    //     }

    //     if (classification.DestinationCase != RouteDestinationCase.SunBlockedNearSide)
    //         return false;

    //     if (!classification.IsNear)
    //         return false;

    //     if (classification.RequiresSunAvoidance)
    //         return false;

    //     if (classification.StartInsideSunSafety ||
    //         classification.TargetInsideSunSafety ||
    //         classification.TargetInsideSunBody)
    //     {
    //         return false;
    //     }

    //     return true;
    // }

    private bool ShouldUseStartTurnInPlace(
    RouteDestinationClassification classification)
    {
        return IsNearSunStartTowardSunDestinationCase(
                   classification.DestinationCase) ||
               _useSunFacingStartTurnInPlaceRoute ||
               _useFallbackStartTurnInPlaceRoute;
    }

    private Vector2 GetStartTurnInPlaceDirection(
    RouteDestinationClassification classification,
    Vector3 routeStartPosition,
    Vector3 destinationPosition,
    Vector2 routeStartFacingDirection)
    {
        if (_useSunFacingStartTurnInPlaceRoute &&
            _sunFacingStartTurnInPlaceDirection.sqrMagnitude > RouteSegmentEpsilon)
        {
            return TurnRadiusRouteMath2A.NormalizeDirectionOrUp(
                _sunFacingStartTurnInPlaceDirection);
        }

        if (_useFallbackStartTurnInPlaceRoute &&
            _fallbackStartTurnInPlaceDirection.sqrMagnitude > RouteSegmentEpsilon)
        {
            return TurnRadiusRouteMath2A.NormalizeDirectionOrUp(
                _fallbackStartTurnInPlaceDirection);
        }

        if (IsNearSunStartTowardSunDestinationCase(
                classification.DestinationCase))
        {
            return GetSunStartLocalTangentDirection(
                routeStartPosition,
                destinationPosition,
                routeStartFacingDirection,
                classification.Obstacle);
        }

        return GetDirectionFromPoints(
            routeStartPosition,
            destinationPosition,
            routeStartFacingDirection);
    }

    private int GetRouteSubstepsPerTick()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        if (config == null)
            return 10;

        return config.RouteSubstepsPerTick;
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
        return IsSunBlockedRouteCase(destinationCase) ||
               IsNearSunStartDestinationCase(destinationCase);
    }

    private static bool IsSunBlockedRouteCase(
    RouteDestinationCase destinationCase)
    {
        return destinationCase == RouteDestinationCase.SunBlockedNearForward ||
               destinationCase == RouteDestinationCase.SunBlockedNearSide ||
               destinationCase == RouteDestinationCase.SunBlockedNearBehind ||
               destinationCase == RouteDestinationCase.SunBlockedFarForward ||
               destinationCase == RouteDestinationCase.SunBlockedFarSide ||
               destinationCase == RouteDestinationCase.SunBlockedFarBehind;
    }

    private static bool IsNearSunStartDestinationCase(
     RouteDestinationCase destinationCase)
    {
        return destinationCase == RouteDestinationCase.NearSunStartAwayNearForward ||
               destinationCase == RouteDestinationCase.NearSunStartAwayFarForward ||
               destinationCase == RouteDestinationCase.NearSunStartAwayNearSide ||
               destinationCase == RouteDestinationCase.NearSunStartAwayFarSide ||
               destinationCase == RouteDestinationCase.NearSunStartAwayNearBehind ||
               destinationCase == RouteDestinationCase.NearSunStartAwayFarBehind ||
               destinationCase == RouteDestinationCase.NearSunStartTangentNearForward ||
               destinationCase == RouteDestinationCase.NearSunStartTangentFarForward ||
               destinationCase == RouteDestinationCase.NearSunStartTangentNearSideAway ||
               destinationCase == RouteDestinationCase.NearSunStartTangentFarSideAway ||
               destinationCase == RouteDestinationCase.NearSunStartTangentNearBehind ||
               destinationCase == RouteDestinationCase.NearSunStartTangentFarBehind ||
               destinationCase == RouteDestinationCase.NearSunStartTowardNearForward ||
               destinationCase == RouteDestinationCase.NearSunStartTowardFarForward ||
               destinationCase == RouteDestinationCase.NearSunStartTowardNearSide ||
               destinationCase == RouteDestinationCase.NearSunStartTowardFarSide ||
               destinationCase == RouteDestinationCase.NearSunStartTowardNearBehind ||
               destinationCase == RouteDestinationCase.NearSunStartTowardFarBehind ||
               destinationCase == RouteDestinationCase.NearSunStartExit;
    }

    private RouteAdjustment ResolveRouteAdjustment(
    Vector3 routeStartPosition,
    Vector3 destinationPosition,
    Vector2 routeStartFacingDirection)
    {
        _useFallbackStartTurnInPlaceRoute = false;
        _fallbackStartTurnInPlaceDirection = Vector2.up;
        _useSunFacingStartTurnInPlaceRoute = false;
        _sunFacingStartTurnInPlaceDirection = Vector2.up;

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

        bool useBehindSmallTurn =
            ShouldUseBehindSmallTurn(routeClassification);

        Vector2 effectiveRouteStartFacingDirection =
            routeStartFacingDirection;

        bool useSunFacingStartTurn =
            IsNearSunStartFacingSun(
                routeClassification,
                routeStartPosition,
                routeStartFacingDirection);

        if (useSunFacingStartTurn)
        {
            _useSunFacingStartTurnInPlaceRoute = true;

            _sunFacingStartTurnInPlaceDirection =
                GetSunStartLocalTangentDirection(
                    routeStartPosition,
                    destinationPosition,
                    routeStartFacingDirection,
                    obstacle);

            effectiveRouteStartFacingDirection =
                _sunFacingStartTurnInPlaceDirection;
        }

        float distanceToDestination =
            GetPathLength(_routePreviewPathBuffer);

        bool requiresSunAvoidance =
            routeClassification.RequiresSunAvoidance;

        bool useNearSideFallback =
            ShouldUseNearSideStartTurnInPlaceFallback(routeClassification);

        bool useNearSunTangentBehindAwayPath =
            ShouldUseNearSunTangentBehindAwayPath(
                routeClassification,
                routeStartPosition,
                effectiveRouteStartFacingDirection);

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

        if (useBehindSmallTurn)
        {
            turnAdjustmentFactor =
                minFactor;

            LogMapPointAdjustmentTrace(
                "BehindSmallTurnMinimumTurnRadius",
                turnAdjustmentFactor,
                speedAdjustmentFactor,
                false,
                distanceToDestination,
                GetAdjustedShipTurnRadius(turnAdjustmentFactor),
                0f,
                0f,
                true);
        }

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
                GetAdjustedShipTurnRadius(turnAdjustmentFactor),
                0f,
                0f,
                false);
        }

        if (useSunFacingStartTurn)
        {
            turnAdjustmentFactor =
                minFactor;

            LogMapPointAdjustmentTrace(
                "NearSunStartFacingSunEscape",
                turnAdjustmentFactor,
                speedAdjustmentFactor,
                false,
                distanceToDestination,
                GetAdjustedShipTurnRadius(turnAdjustmentFactor),
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

            float adjustedTurnRadius =
                GetAdjustedShipTurnRadius(turnAdjustmentFactor);

            float probeRouteLength;
            float maxAllowedRouteLength;

            bool directRouteBuilt =
                CanReachRouteWithTurnRadius(
                    _directTravelPathBuffer,
                    effectiveRouteStartFacingDirection,
                    adjustedTurnRadius,
                    speedAdjustmentFactor,
                    out probeRouteLength,
                    out maxAllowedRouteLength);

            bool directRouteAvoidsSun =
                directRouteBuilt &&
                (
                    requiresSunAvoidance
                        ? RoutePathAvoidsSunForWaypoints(
                            _routeProbePathBuffer,
                            _directTravelPathBuffer,
                            obstacle,
                            routeClassification.DestinationCase)
                        : RoutePathAvoidsSunBody(
                            _routeProbePathBuffer,
                            obstacle)
                );

            LogMapPointAdjustmentTrace(
                "ProbeDirect",
                turnAdjustmentFactor,
                speedAdjustmentFactor,
                directRouteBuilt && directRouteAvoidsSun,
                GetPathLength(_directTravelPathBuffer),
                adjustedTurnRadius,
                probeRouteLength,
                maxAllowedRouteLength,
                directRouteAvoidsSun);

            if (directRouteBuilt && directRouteAvoidsSun)
            {
                return new RouteAdjustment(
                    turnAdjustmentFactor,
                    speedAdjustmentFactor,
                    maxAllowedRouteLength,
                    true);
            }

            if (directRouteBuilt &&
                requiresSunAvoidance)
            {
                CopyRoutePath(
                    _routeProbePathBuffer,
                    _routeSegmentPathBuffer);

                bool sunSafeRouteBuilt =
                    TryBuildSunSafeRouteFromBuiltRoute(
                        _routeProbePathBuffer,
                        _routeSegmentPathBuffer,
                        effectiveRouteStartFacingDirection,
                        adjustedTurnRadius,
                        GetCurrentShipTravelSpeed() *
                        Mathf.Clamp01(speedAdjustmentFactor),
                        "ProbeSunPostProcessFromBaseRoute",
                        obstacle,
                        maxAllowedRouteLength);

                float sunSafeRouteLength =
                    GetPathLength(_routeProbePathBuffer);

                LogMapPointAdjustmentTrace(
                    "ProbeSunPostProcess",
                    turnAdjustmentFactor,
                    speedAdjustmentFactor,
                    sunSafeRouteBuilt,
                    sunSafeRouteLength,
                    adjustedTurnRadius,
                    sunSafeRouteLength,
                    maxAllowedRouteLength,
                    sunSafeRouteBuilt);

                if (sunSafeRouteBuilt)
                {
                    return new RouteAdjustment(
                        turnAdjustmentFactor,
                        speedAdjustmentFactor,
                        maxAllowedRouteLength,
                        true);
                }
            }

            if (useNearSunTangentBehindAwayPath)
            {
                BuildNearSunTangentBehindAwayTravelPath(
                    routeStartPosition,
                    destinationPosition,
                    _routePreviewPathBuffer,
                    obstacle,
                    adjustedTurnRadius);

                distanceToDestination =
                    GetPathLength(_routePreviewPathBuffer);

                bool tangentRouteBuilt =
                    CanReachRouteWithTurnRadius(
                        _routePreviewPathBuffer,
                        effectiveRouteStartFacingDirection,
                        adjustedTurnRadius,
                        speedAdjustmentFactor,
                        out probeRouteLength,
                        out maxAllowedRouteLength);

                bool tangentRouteAvoidsSun =
                    tangentRouteBuilt &&
                    RoutePathAvoidsSunBody(
                        _routeProbePathBuffer,
                        obstacle);

                LogMapPointAdjustmentTrace(
                    "ProbeNearSunTangentBehindAway",
                    turnAdjustmentFactor,
                    speedAdjustmentFactor,
                    tangentRouteBuilt && tangentRouteAvoidsSun,
                    distanceToDestination,
                    adjustedTurnRadius,
                    probeRouteLength,
                    maxAllowedRouteLength,
                    tangentRouteAvoidsSun);

                if (tangentRouteBuilt && tangentRouteAvoidsSun)
                {
                    return new RouteAdjustment(
                        turnAdjustmentFactor,
                        speedAdjustmentFactor,
                        maxAllowedRouteLength,
                        true);
                }
            }

            if (!requiresSunAvoidance)
            {
                LogMapPointAdjustmentTrace(
                    "SkipSunAvoidanceNotRequired",
                    turnAdjustmentFactor,
                    speedAdjustmentFactor,
                    false,
                    GetPathLength(_directTravelPathBuffer),
                    adjustedTurnRadius,
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

            bool sunAvoidanceRouteBuilt = false;
            bool sunAvoidanceRouteAvoidsSun = false;

            float sunAvoidancePathLength = 0f;
            float sunAvoidanceProbeRouteLength = 0f;
            float sunAvoidanceMaxAllowedRouteLength = 0f;

            float paddingStep =
                GetSunAvoidanceRoutePaddingStep();

            float paddingMax =
                GetSunAvoidanceRoutePaddingMax();

            int paddingAttemptCount =
                paddingStep > RouteSegmentEpsilon
                    ? Mathf.CeilToInt(paddingMax / paddingStep)
                    : 0;

            for (int paddingAttempt = 0; paddingAttempt <= paddingAttemptCount; paddingAttempt++)
            {
                float padding =
                    paddingAttempt == paddingAttemptCount
                        ? paddingMax
                        : paddingStep * paddingAttempt;

                SunAvoidanceObstacle planningObstacle =
                    CreateSunRoutePlanningObstacle(
                        obstacle,
                        padding);

                BuildSunAdjustmentTravelPath(
                    routeStartPosition,
                    destinationPosition,
                    _routePreviewPathBuffer,
                    effectiveRouteStartFacingDirection,
                    adjustedTurnRadius,
                    planningObstacle,
                    routeClassification.DestinationCase);

                sunAvoidancePathLength =
                    GetPathLength(_routePreviewPathBuffer);

                sunAvoidanceRouteBuilt =
                    CanReachRouteWithTurnRadius(
                        _routePreviewPathBuffer,
                        effectiveRouteStartFacingDirection,
                        adjustedTurnRadius,
                        speedAdjustmentFactor,
                        out sunAvoidanceProbeRouteLength,
                        out sunAvoidanceMaxAllowedRouteLength);

                sunAvoidanceRouteAvoidsSun =
                    sunAvoidanceRouteBuilt &&
                    RoutePathAvoidsSunBody(
                        _routeProbePathBuffer,
                        obstacle);

                LogMapPointAdjustmentTrace(
                    padding > RouteSegmentEpsilon
                        ? "ProbeSunAvoidance.Padding" + padding.ToString("0.###")
                        : "ProbeSunAvoidance",
                    turnAdjustmentFactor,
                    speedAdjustmentFactor,
                    sunAvoidanceRouteBuilt && sunAvoidanceRouteAvoidsSun,
                    sunAvoidancePathLength,
                    adjustedTurnRadius,
                    sunAvoidanceProbeRouteLength,
                    sunAvoidanceMaxAllowedRouteLength,
                    sunAvoidanceRouteAvoidsSun);

                if (sunAvoidanceRouteBuilt && sunAvoidanceRouteAvoidsSun)
                    break;
            }

            if (sunAvoidanceRouteBuilt && sunAvoidanceRouteAvoidsSun)
            {
                return new RouteAdjustment(
                    turnAdjustmentFactor,
                    speedAdjustmentFactor,
                    sunAvoidanceMaxAllowedRouteLength,
                    true);
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

        if (!useSunFacingStartTurn &&
            useNearSideFallback)
        {
            BuildDirectTravelPath(
                routeStartPosition,
                destinationPosition,
                _routeProbePathBuffer);

            _useFallbackStartTurnInPlaceRoute = true;
            _fallbackStartTurnInPlaceDirection =
                GetDirectionFromPoints(
                    routeStartPosition,
                    destinationPosition,
                    routeStartFacingDirection);

            float directRouteLength =
                GetPathLength(_routeProbePathBuffer);

            LogMapPointAdjustmentTrace(
                "NearSideStartTurnInPlaceFallback",
                1f,
                1f,
                true,
                directRouteLength,
                0f,
                directRouteLength,
                directRouteLength,
                true);

            return new RouteAdjustment(
                1f,
                1f,
                directRouteLength,
                true);
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
                    routeStepDistance),
                GetRouteStraightExitAngleDegrees());

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
                   turnReserveLength * 1.25f +
                   routeStepDistance * 4f;
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
        float baseTurnRadius =
            GetCurrentShipTurnRadius();

        float factor =
            Mathf.Clamp01(adjustmentFactor);

        float adjustedTurnRadius =
            baseTurnRadius * factor;

        if (factor < 1f &&
            baseTurnRadius > RouteSegmentEpsilon)
        {
            adjustedTurnRadius =
                Mathf.Max(
                    adjustedTurnRadius,
                    GetMinRouteTurnRadiusAbsolute());
        }

        return adjustedTurnRadius;
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

    private float GetSunTangentToleranceAngleDegrees()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        float tolerancePercent =
            config != null
                ? config.SunTangentTolerancePercent
                : 5f;

        return Mathf.Clamp(
            tolerancePercent,
            0f,
            25f) *
            0.01f *
            90f;
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
        {
            return;
        }

        string runtimeNpcId = State.Destination.RuntimeNpcId;

        if (!TryGetNpcDestinationPosition(
                runtimeNpcId,
                out Vector3 npcPosition))
        {
            CancelTravel();
            return;
        }

        Vector3 destinationPosition =
            GetNpcFollowDestinationPosition(npcPosition);

        State.DestinationPosition = destinationPosition;
        State.Destination.FixedMapPosition = destinationPosition;

        _lastNpcDestinationRefreshTick = quantTick;

        if (State.Status != SystemTravelStatus.DestinationSelected)
            return;

        float distanceToDestination =
            Vector3.Distance(
                State.GetCurrentPosition(),
                State.DestinationPosition);

        if (distanceToDestination <= ArrivalDistanceThreshold)
            return;

        LogCustom(
            "[PLAYER-NPC-FOLLOW] Restart selected NPC travel after target moved. " +
            "Npc=" + runtimeNpcId +
            " | QuantTick=" + quantTick +
            " | PlayerPosition=" + FormatVector3(State.GetCurrentPosition()) +
            " | DestinationPosition=" + FormatVector3(State.DestinationPosition) +
            " | Distance=" + distanceToDestination.ToString("0.###"));

        StartTravelAutomaticallyIfPossible();
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
        return _isMapPointRouteBuildTraceEnabled &&
               (IsMapPointDestination() || IsNpcDestination());
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

        if (TryResolveNpcRuntimeService() &&
            _npcRuntimeService.TryGetNpc(
                runtimeNpcId,
                out SystemNpcRuntimeState npc) &&
            npc != null)
        {
            if (!npc.IsAlive ||
                npc.IsOnPlanet ||
                !IsTargetInCurrentSystem(npc.CurrentSystemId))
            {
                return false;
            }

            position = npc.CurrentPosition;
            position.z = -2f;
            return true;
        }

        if (TryResolveEnemyService() &&
            _enemyService.TryGetEnemy(
                runtimeNpcId,
                out SystemEnemyRuntimeState enemy) &&
            enemy != null)
        {
            if (!enemy.IsAlive ||
                !IsTargetInCurrentSystem(enemy.SystemId))
            {
                return false;
            }

            position = enemy.Position;
            position.z = -2f;
            return true;
        }

        return false;
    }

    private bool IsTargetInCurrentSystem(string targetSystemId)
    {
        string currentSystemId =
            GetCurrentPlayerSystemId();

        if (string.IsNullOrWhiteSpace(currentSystemId))
            return true;

        if (string.IsNullOrWhiteSpace(targetSystemId))
            return false;

        return string.Equals(
            targetSystemId,
            currentSystemId);
    }

    private string GetCurrentPlayerSystemId()
    {
        if (State != null &&
            !string.IsNullOrWhiteSpace(State.CurrentSystemId))
        {
            return State.CurrentSystemId;
        }

        if (_gameSessionService != null &&
            _gameSessionService.State != null &&
            _gameSessionService.State.Player != null)
        {
            return _gameSessionService.State.Player.CurrentSystemId;
        }

        return string.Empty;
    }

    private bool TryResolveEnemyService()
    {
        if (_enemyService != null)
            return true;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return false;
        }

        return Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _enemyService);
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

    private float GetSunDestinationForbiddenRadiusMultiplier()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        if (config == null)
            return 1.2f;

        return config.SunDestinationForbiddenRadiusMultiplier;
    }

    private void BuildNearSunTangentBehindAwayTravelPath(
    Vector3 from,
    Vector3 to,
    List<Vector3> path,
    SunAvoidanceObstacle obstacle,
    float turnRadius)
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

        Vector2 radialAway =
            from2 - center;

        if (radialAway.sqrMagnitude <= RouteSegmentEpsilon)
            radialAway = Vector2.right;

        radialAway =
            radialAway.normalized;

        float awayDistance =
            Mathf.Max(
                ArrivalDistanceThreshold * 4f,
                turnRadius * 0.5f);

        Vector2 awayPoint =
            from2 +
            radialAway * awayDistance;

        path.Add(
            new Vector3(
                awayPoint.x,
                awayPoint.y,
                from.z));

        path.Add(to);
    }

    private bool ShouldUseNearSunTangentBehindAwayPath(
    RouteDestinationClassification classification,
    Vector3 shipPosition,
    Vector2 shipFacingDirection)
    {
        bool isSupportedCase =
            classification.DestinationCase == RouteDestinationCase.NearSunStartTangentNearBehind ||
            classification.DestinationCase == RouteDestinationCase.NearSunStartTangentFarBehind ||
            classification.DestinationCase == RouteDestinationCase.SunBlockedNearBehind ||
            classification.DestinationCase == RouteDestinationCase.SunBlockedFarBehind;

        if (!isSupportedCase ||
            !classification.Obstacle.HasObstacle)
        {
            return false;
        }

        Vector2 radialAwayFromSun =
            new Vector2(
                shipPosition.x - classification.Obstacle.Center.x,
                shipPosition.y - classification.Obstacle.Center.y);

        if (radialAwayFromSun.sqrMagnitude <= RouteSegmentEpsilon)
            return false;

        radialAwayFromSun =
            radialAwayFromSun.normalized;

        if (!IsFacingNearSunTangent(
                shipFacingDirection,
                radialAwayFromSun))
        {
            return false;
        }

        float nearSunDistance =
            classification.Obstacle.Radius +
            GetCurrentShipTurnRadius() * 0.1f;

        return classification.StartDistanceFromSun <= nearSunDistance;
    }

    private Vector2 GetSunStartLocalTangentDirection(
    Vector3 shipPosition,
    Vector3 targetPosition,
    Vector2 fallbackDirection,
    SunAvoidanceObstacle obstacle)
    {
        if (!obstacle.HasObstacle)
        {
            return TurnRadiusRouteMath2A.NormalizeDirectionOrUp(
                fallbackDirection);
        }

        Vector2 center =
            new Vector2(
                obstacle.Center.x,
                obstacle.Center.y);

        Vector2 ship =
            new Vector2(
                shipPosition.x,
                shipPosition.y);

        Vector2 target =
            new Vector2(
                targetPosition.x,
                targetPosition.y);

        Vector2 radialAway =
            ship - center;

        if (radialAway.sqrMagnitude <= RouteSegmentEpsilon)
            radialAway = target - center;

        if (radialAway.sqrMagnitude <= RouteSegmentEpsilon)
            return TurnRadiusRouteMath2A.NormalizeDirectionOrUp(
                fallbackDirection);

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

        Vector2 targetDirection =
            target - ship;

        if (targetDirection.sqrMagnitude <= RouteSegmentEpsilon)
        {
            return Vector2.Dot(
                       tangentA,
                       TurnRadiusRouteMath2A.NormalizeDirectionOrUp(fallbackDirection)) >=
                   Vector2.Dot(
                       tangentB,
                       TurnRadiusRouteMath2A.NormalizeDirectionOrUp(fallbackDirection))
                ? tangentA
                : tangentB;
        }

        targetDirection =
            targetDirection.normalized;

        return Vector2.Dot(tangentA, targetDirection) >=
               Vector2.Dot(tangentB, targetDirection)
            ? tangentA
            : tangentB;
    }

    private float GetRouteStraightExitAngleDegrees()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        if (config == null)
            return 3f;

        return config.RouteStraightExitAngleDegrees;
    }

    private float GetRouteBehindSmallTurnAngleToleranceDegrees()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        if (config == null)
            return 15f;

        return config.RouteBehindSmallTurnAngleToleranceDegrees;
    }

    private bool ShouldUseBehindSmallTurn(
    RouteDestinationClassification classification)
    {
        float tolerance =
            GetRouteBehindSmallTurnAngleToleranceDegrees();

        bool isBehindEnough =
            classification.BearingAngleDegrees >= 180f - tolerance;

        if (classification.DestinationCase == RouteDestinationCase.MovingTarget)
            return isBehindEnough;

        if (classification.DestinationCase == RouteDestinationCase.NearSunStartTangentNearBehind ||
            classification.DestinationCase == RouteDestinationCase.NearSunStartTangentFarBehind ||
            classification.DestinationCase == RouteDestinationCase.NearSunStartAwayNearBehind ||
            classification.DestinationCase == RouteDestinationCase.NearSunStartAwayFarBehind)
        {
            return true;
        }

        if (classification.DestinationCase != RouteDestinationCase.SunBlockedNearBehind &&
            classification.DestinationCase != RouteDestinationCase.SunBlockedFarBehind)
        {
            return false;
        }

        return isBehindEnough;
    }

    private float GetBehindSmallTurnArcLength(
    RouteDestinationClassification classification)
    {
        float angleRadians =
            classification.BearingAngleDegrees *
            Mathf.Deg2Rad;

        float turnRadius =
            GetAdjustedShipTurnRadius(
                _routeTurnAdjustmentFactor);

        return Mathf.Max(
            ArrivalDistanceThreshold,
            angleRadians * turnRadius);
    }

    private float GetSpeedFactorForOneTickDistance(
        float distance)
    {
        float baseSpeed =
            GetCurrentShipTravelSpeed();

        if (baseSpeed <= RouteSegmentEpsilon)
            return 1f;

        return Mathf.Clamp01(
            distance / baseSpeed);
    }

    private float GetMinRouteTurnRadiusAbsolute()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        if (config == null)
            return 30f;

        return config.MinRouteTurnRadiusAbsolute;
    }

    private static void CopyRoutePath(
    IReadOnlyList<Vector3> source,
    List<Vector3> destination)
    {
        if (destination == null)
            return;

        destination.Clear();

        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            destination.Add(source[i]);
        }
    }

    public SystemRouteSectorDebugInfo2A GetRouteSectorDebugInfo2A(
    Vector3 targetPosition)
    {
        Vector3 shipPosition =
            GetCurrentShipPosition();

        Vector2 shipFacingDirection =
            GetCurrentShipFacingDirection();

        RouteDestinationClassification classification =
            ClassifyRouteDestination(
                shipPosition,
                targetPosition,
                shipFacingDirection);

        return new SystemRouteSectorDebugInfo2A(
            classification.DestinationCase.ToString(),
            shipPosition,
            targetPosition,
            shipFacingDirection,
            classification.DirectDistance,
            classification.BearingAngleDegrees,
            classification.NearDistanceThreshold,
            classification.IsNear,
            classification.RequiresSunAvoidance,
            classification.StartInsideSunSafety,
            classification.TargetInsideSunSafety,
            classification.TargetInsideSunBody,
            classification.StartDistanceFromSun,
            classification.TargetDistanceFromSun,
            classification.Obstacle.HasObstacle,
            classification.Obstacle.Center,
            classification.Obstacle.Radius,
            classification.Obstacle.BlockingRadius,
            GetRouteForwardSectorAngleDegrees(),
            GetRouteBehindSectorAngleDegrees(),
            GetSunTangentToleranceAngleDegrees());
    }

    private float GetSunAvoidanceRoutePaddingStep()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        if (config == null)
            return 5f;

        return config.SunAvoidanceRoutePaddingStep;
    }

    private float GetSunAvoidanceRoutePaddingMax()
    {
        ShipMovementConfig config =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        if (config == null)
            return 40f;

        return config.SunAvoidanceRoutePaddingMax;
    }

    private static SunAvoidanceObstacle CreateSunRoutePlanningObstacle(
        SunAvoidanceObstacle obstacle,
        float routePadding)
    {
        if (!obstacle.HasObstacle)
            return obstacle;

        float planningRadius =
            Mathf.Max(
                obstacle.BlockingRadius,
                obstacle.Radius + Mathf.Max(0f, routePadding));

        return new SunAvoidanceObstacle(
            true,
            obstacle.Center,
            planningRadius,
            obstacle.BlockingRadius);
    }

    private bool ShouldUseNearSideStartTurnInPlaceFallback(
        RouteDestinationClassification classification)
    {
        return classification.DestinationCase == RouteDestinationCase.SunBlockedNearSide &&
               classification.IsNear &&
               !classification.RequiresSunAvoidance &&
               !classification.StartInsideSunSafety &&
               !classification.TargetInsideSunSafety &&
               !classification.TargetInsideSunBody;
    }

    private bool IsMovingTravelDestination()
    {
        if (State == null ||
            State.Destination == null)
        {
            return false;
        }

        switch (State.Destination.Type)
        {
            case TravelDestinationType.Planet:
            case TravelDestinationType.Npc:
            case TravelDestinationType.Station:
            case TravelDestinationType.SystemExit:
                return true;

            default:
                return false;
        }
    }

    private Vector3 GetCurrentTravelTickDestinationPosition()
    {
        if (IsNpcDestination())
            return GetCurrentDestinationPosition();

        return GetCurrentActiveRouteDestinationPosition();
    }

    private Vector3 GetActiveRouteEndPositionForRefresh()
    {
        if (_activeTravelRoutePath != null &&
            _activeTravelRoutePath.Count > 0)
        {
            return _activeTravelRoutePath[_activeTravelRoutePath.Count - 1];
        }

        return State != null
            ? State.GetCurrentPosition()
            : GetCurrentDestinationPosition();
    }
}
