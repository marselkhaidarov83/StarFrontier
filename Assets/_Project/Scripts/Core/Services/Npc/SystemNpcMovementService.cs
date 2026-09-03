using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SystemNpcMovementService : CustomService, ISystemNpcMovementService
{
    private const float ArrivalDistanceThreshold = 3f;
    private const float SunAvoidanceSafetyMargin = 80f;
    private const int SunAvoidanceArcSegments = 18;
    private const float SunAvoidanceDestinationRefreshThreshold = 40f;
    private const float DirectionThresholdSqrMagnitude = 0.0001f;

    private readonly ISystemNpcRuntimeService _runtimeService;
    private readonly ISystemNpcBehaviorService _behaviorService;
    private readonly ISystemNpcMovementRouteService _routeService;
    private readonly IConfigService _configService;
    private readonly SimpleEventBus _eventBus;

    private readonly List<Vector3> _sunAvoidancePath = new();
    private readonly Dictionary<string, SunAvoidanceRouteState> _sunAvoidanceRoutes = new();

    private const int NpcRoutePlanMaxSteps = 8192;
    private readonly Dictionary<string, NpcMovementRouteState> _npcMovementRoutes = new();
    private readonly List<Vector3> _npcRouteWaypointsBuffer = new();
    private readonly List<Vector3> _npcRoutePreviewPathBuffer = new();

    public SystemNpcMovementService()
    {
        // _debugEnabled = true;
        _debugStop = true;

        _runtimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _behaviorService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcBehaviorService>();
        _routeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcMovementRouteService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        LogCustom(
            "[NpcRouteDebug] Service created | " +
            "RuntimeService = " + (_runtimeService != null) +
            " | BehaviorService = " + (_behaviorService != null) +
            " | RouteService = " + (_routeService != null) +
            " | ConfigService = " + (_configService != null) +
            " | EventBus = " + (_eventBus != null));
    }

    public void Tick(StarSystemConfig starSystem, float deltaTime, int currentTick)
    {
        if (starSystem == null || string.IsNullOrWhiteSpace(starSystem.Id))
            return;

        if (deltaTime <= 0f)
            return;

        var npcs = _runtimeService.GetAliveNpcsInSystem(starSystem.Id);

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];

            if (!CanMove(npc))
                continue;

            TickNpcMovement(npc, deltaTime, currentTick);
        }
    }

    private bool CanMove(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return false;

        if (!npc.IsAlive)
            return false;

        if (npc.IsOnPlanet)
            return false;

        if (npc.TravelState == SystemNpcTravelState.Idle)
            return false;

        if (npc.TravelState == SystemNpcTravelState.OnPlanet)
            return false;

        return true;
    }

    private void TickNpcMovement(
    SystemNpcRuntimeState npc,
    float deltaTime,
    int currentTick)
    {
        if (npc.TargetPosition == Vector3.zero)
        {
            npc.StartPosition = npc.CurrentPosition;
            npc.TravelProgress01 = 0f;
        }

        EnsureTickMovementDirection(npc, currentTick);

        if (npc.TickMovementArrived)
            return;

        if (!_npcMovementRoutes.TryGetValue(
                npc.RuntimeNpcId,
                out NpcMovementRouteState routeState) ||
            routeState.Path.Count <= 1)
        {
            return;
        }

        float totalRouteLength =
            GetNpcPathLength(routeState.Path);

        if (totalRouteLength <= ArrivalDistanceThreshold)
        {
            CompleteMovement(npc, currentTick);
            return;
        }

        routeState.DistanceTravelled =
            Mathf.Clamp(
                routeState.DistanceTravelled,
                0f,
                totalRouteLength);

        float movementDistance =
            Mathf.Max(0f, npc.Speed) *
            deltaTime;

        float nextDistance =
            Mathf.Clamp(
                routeState.DistanceTravelled + movementDistance,
                0f,
                totalRouteLength);

        Vector3 newPosition =
            GetNpcPointOnPathAtDistance(
                routeState.Path,
                nextDistance);

        Vector2 routeDirection =
            GetNpcDirectionOnPathAtDistance(
                routeState.Path,
                nextDistance);

        if (routeDirection.sqrMagnitude > DirectionThresholdSqrMagnitude)
        {
            npc.FacingDirection =
                new Vector3(
                    routeDirection.x,
                    routeDirection.y,
                    0f);

            npc.TickMovementDirection =
                npc.FacingDirection;
        }

        npc.CurrentPosition = newPosition;
        routeState.DistanceTravelled = nextDistance;

        npc.TravelProgress01 =
            Mathf.Clamp01(
                routeState.DistanceTravelled /
                totalRouteLength);

        _eventBus.Publish(new SystemNpcPositionChangedEvent(
            npc.RuntimeNpcId,
            npc.CurrentSystemId,
            npc.CurrentPosition));

        if (totalRouteLength - nextDistance <= ArrivalDistanceThreshold)
            CompleteMovement(npc, currentTick);
    }

    private void EnsureTickMovementDirection(
    SystemNpcRuntimeState npc,
    int currentTick)
    {
        if (npc.TickMovementDirectionTick == currentTick &&
            npc.TickMovementTargetPosition != Vector3.zero)
        {
            return;
        }

        npc.TickMovementArrived = false;

        Vector3 finalTargetPosition =
            _routeService.GetNextTargetPosition(npc);

        npc.TargetPosition =
            finalTargetPosition;

        if (!TryBuildNpcMovementRoutePath(
                npc,
                finalTargetPosition,
                _npcRoutePreviewPathBuffer))
        {
            npc.CurrentMovementTargetPosition = finalTargetPosition;
            npc.TickMovementTargetPosition = finalTargetPosition;
            npc.TickMovementDirectionTick = currentTick;
            return;
        }

        NpcMovementRouteState routeState =
            GetOrCreateNpcMovementRouteState(npc.RuntimeNpcId);

        routeState.Path.Clear();
        routeState.Path.AddRange(_npcRoutePreviewPathBuffer);
        routeState.Destination = finalTargetPosition;
        routeState.DistanceTravelled = 0f;
        routeState.Tick = currentTick;

        float distancePerTick =
            Mathf.Max(0f, npc.Speed) *
            Mathf.Max(0.01f, GameTimeState.SecondsPerDay);

        Vector3 movementTargetPosition =
            GetNpcPointOnPathAtDistance(
                routeState.Path,
                distancePerTick);

        npc.CurrentMovementTargetPosition = movementTargetPosition;
        npc.TickMovementTargetPosition = movementTargetPosition;
        npc.TickMovementDirectionTick = currentTick;

        Vector2 direction =
            GetNpcDirectionOnPathAtDistance(
                routeState.Path,
                0f);

        if (direction.sqrMagnitude > DirectionThresholdSqrMagnitude)
        {
            npc.TickMovementDirection =
                new Vector3(
                    direction.x,
                    direction.y,
                    0f);
        }

        if (npc.FacingDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            npc.FacingDirection = npc.TickMovementDirection;
    }

    public bool TryBuildRoutePreview2A(
    string runtimeNpcId,
    TravelRoutePreview2A preview,
    float smallDotSpacing,
    int maxBigDots,
    int maxSmallDots,
    float secondsPerTick)
    {
        if (preview == null)
        {
            LogCustom("[NpcRouteDebug] BuildPreview failed: preview is null.");
            return false;
        }

        preview.Clear();

        if (string.IsNullOrWhiteSpace(runtimeNpcId))
            return false;

        if (_runtimeService == null)
            return false;

        if (!_runtimeService.TryGetNpc(
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

        if (!TryGetNpcPreviewDestination(
                npc,
                out Vector3 destinationPosition))
        {
            return false;
        }

        bool routeBuilt;
        float passedDistance = 0f;

        if (TryGetActiveNpcPreviewRoute(
                npc,
                destinationPosition,
                out NpcMovementRouteState activeRouteState))
        {
            _npcRoutePreviewPathBuffer.Clear();
            _npcRoutePreviewPathBuffer.AddRange(activeRouteState.Path);
            passedDistance = Mathf.Max(0f, activeRouteState.DistanceTravelled);
            routeBuilt = _npcRoutePreviewPathBuffer.Count > 1;
        }
        else
        {
            routeBuilt =
                TryBuildNpcMovementRoutePath(
                    npc,
                    destinationPosition,
                    _npcRoutePreviewPathBuffer);

            passedDistance = 0f;
        }

        if (!routeBuilt)
            return false;

        float totalPathLength =
            GetNpcPathLength(_npcRoutePreviewPathBuffer);

        if (totalPathLength <= ArrivalDistanceThreshold)
            return false;

        passedDistance =
            Mathf.Clamp(
                passedDistance,
                0f,
                totalPathLength);

        float safeSmallDotSpacing =
            Mathf.Max(0.01f, smallDotSpacing);

        int safeMaxBigDots =
            Mathf.Max(1, maxBigDots);

        int safeMaxSmallDots =
            Mathf.Max(0, maxSmallDots);

        float distancePerTick =
            Mathf.Max(0.01f, npc.Speed) *
            Mathf.Max(0.01f, secondsPerTick);

        float firstBigDotDistance =
            Mathf.Floor(passedDistance / distancePerTick) *
            distancePerTick +
            distancePerTick;

        for (int visibleTickIndex = 1; visibleTickIndex <= safeMaxBigDots; visibleTickIndex++)
        {
            float intervalStartDistance =
                Mathf.Clamp(
                    firstBigDotDistance -
                    distancePerTick +
                    (visibleTickIndex - 1) * distancePerTick,
                    0f,
                    totalPathLength);

            float distanceAtTick =
                Mathf.Clamp(
                    firstBigDotDistance +
                    (visibleTickIndex - 1) * distancePerTick,
                    0f,
                    totalPathLength);

            if (distanceAtTick <= passedDistance + 0.001f)
                continue;

            AddNpcSmallRoutePreviewDots2A(
                preview,
                _npcRoutePreviewPathBuffer,
                intervalStartDistance,
                distanceAtTick,
                passedDistance,
                visibleTickIndex,
                safeSmallDotSpacing,
                safeMaxSmallDots);

            preview.AddBigDot(
                GetNpcPointOnPathAtDistance(
                    _npcRoutePreviewPathBuffer,
                    distanceAtTick),
                visibleTickIndex);

            if (distanceAtTick >= totalPathLength)
                break;
        }

        return preview.HasDots;
    }

    private bool TryGetNpcPreviewDestination(
    SystemNpcRuntimeState npc,
    out Vector3 destinationPosition)
    {
        destinationPosition = Vector3.zero;

        if (npc == null)
            return false;

        Vector3 liveTargetPosition =
            _routeService != null
                ? _routeService.GetNextTargetPosition(npc)
                : Vector3.zero;

        if (TryUseNpcPreviewDestination(
                npc,
                liveTargetPosition,
                out destinationPosition))
        {
            return true;
        }

        if (TryUseNpcPreviewDestination(
                npc,
                npc.TargetPosition,
                out destinationPosition))
        {
            return true;
        }

        if (TryUseNpcPreviewDestination(
                npc,
                npc.CurrentMovementTargetPosition,
                out destinationPosition))
        {
            return true;
        }

        if (TryUseNpcPreviewDestination(
                npc,
                npc.TickMovementTargetPosition,
                out destinationPosition))
        {
            return true;
        }

        return false;
    }

    private bool TryUseNpcPreviewDestination(
    SystemNpcRuntimeState npc,
    Vector3 candidatePosition,
    out Vector3 destinationPosition)
    {
        destinationPosition = Vector3.zero;

        if (npc == null)
            return false;

        if (!IsFinite(candidatePosition))
            return false;

        if (candidatePosition == Vector3.zero)
            return false;

        candidatePosition.z = npc.CurrentPosition.z;

        if (Vector3.Distance(
                npc.CurrentPosition,
                candidatePosition) <= ArrivalDistanceThreshold)
        {
            return false;
        }

        destinationPosition = candidatePosition;
        return true;
    }

    private static bool IsFinite(
    Vector3 value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z);
    }

    private static bool IsFinite(
        float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }

    private bool TryBuildNpcMovementRoutePath(
    SystemNpcRuntimeState npc,
    Vector3 destinationPosition,
    List<Vector3> routePath)
    {
        if (npc == null ||
            routePath == null)
        {
            return false;
        }

        routePath.Clear();

        if (Vector3.Distance(
                npc.CurrentPosition,
                destinationPosition) <= ArrivalDistanceThreshold)
        {
            return false;
        }

        BuildNpcTravelWaypoints(
            npc,
            destinationPosition,
            _npcRouteWaypointsBuffer);

        Vector2 facingDirection =
            GetNpcSafeFacingDirection(npc);

        float speed =
            Mathf.Max(0.01f, npc.Speed);

        float turnRadius =
            Mathf.Max(0f, npc.TurnRadius);

        float routeStepDistance =
            GetNpcRoutePlanStepDistance(speed);

        float waypointPathLength =
            GetNpcPathLength(_npcRouteWaypointsBuffer);

        int maxSteps =
            GetNpcRoutePlanMaxSteps(
                waypointPathLength,
                turnRadius,
                routeStepDistance);

        bool routeBuilt =
            TurnRadiusRouteMath2A.TryBuildWaypointPreviewPath(
                routePath,
                _npcRouteWaypointsBuffer,
                facingDirection,
                routeStepDistance,
                turnRadius,
                ArrivalDistanceThreshold,
                maxSteps,
                GetNpcIntermediateWaypointArrivalDistanceThreshold(
                    _npcRouteWaypointsBuffer,
                    routeStepDistance),
                GetNpcRouteStraightExitAngleDegrees());

        return routeBuilt &&
               routePath.Count > 1;
    }

    private void BuildNpcTravelWaypoints(
    SystemNpcRuntimeState npc,
    Vector3 destinationPosition,
    List<Vector3> waypoints)
    {
        waypoints.Clear();

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        if (starSystem == null ||
            starSystem.Sun == null)
        {
            waypoints.Add(npc.CurrentPosition);
            waypoints.Add(destinationPosition);
            return;
        }

        SunConfig sun = starSystem.Sun;

        Vector3 sunCenter =
            new Vector3(
                sun.LocalOffset.x,
                sun.LocalOffset.y,
                npc.CurrentPosition.z);

        float sunRadius =
            Mathf.Max(0f, GetSunWorldSize(sun) * 0.5f);

        float avoidanceRadius =
            sunRadius + SunAvoidanceSafetyMargin;

        SystemTravelSunAvoidancePath2A.BuildPath(
            waypoints,
            npc.CurrentPosition,
            destinationPosition,
            sunCenter,
            avoidanceRadius,
            SunAvoidanceArcSegments,
            false,
            GetNpcSafeFacingDirection(npc),
            Mathf.Max(0f, npc.TurnRadius));
    }

    private void AddNpcSmallRoutePreviewDots2A(
    TravelRoutePreview2A preview,
    List<Vector3> path,
    float intervalStartDistance,
    float intervalEndDistance,
    float passedDistance,
    int tickIndex,
    float smallDotSpacing,
    int maxSmallDots)
    {
        if (preview == null ||
            path == null ||
            path.Count <= 1)
        {
            return;
        }

        if (preview.SmallDotCount >= maxSmallDots)
            return;

        float safeSpacing =
            Mathf.Max(0.01f, smallDotSpacing);

        float firstDotDistance =
            Mathf.Ceil(
                (Mathf.Max(intervalStartDistance, passedDistance) + 0.001f) /
                safeSpacing) *
            safeSpacing;

        for (float dotDistance = firstDotDistance;
             dotDistance < intervalEndDistance - 0.001f;
             dotDistance += safeSpacing)
        {
            if (preview.SmallDotCount >= maxSmallDots)
                return;

            preview.AddSmallDot(
                GetNpcPointOnPathAtDistance(path, dotDistance),
                tickIndex);
        }
    }

    private bool TryGetActiveNpcPreviewRoute(
    SystemNpcRuntimeState npc,
    Vector3 destinationPosition,
    out NpcMovementRouteState routeState)
    {
        routeState = null;

        if (npc == null ||
            string.IsNullOrWhiteSpace(npc.RuntimeNpcId))
        {
            return false;
        }

        if (!_npcMovementRoutes.TryGetValue(
                npc.RuntimeNpcId,
                out routeState))
        {
            return false;
        }

        if (routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1)
        {
            return false;
        }

        float totalPathLength =
            GetNpcPathLength(routeState.Path);

        if (routeState.DistanceTravelled >= totalPathLength - ArrivalDistanceThreshold)
            return false;

        return true;
    }

    private NpcMovementRouteState GetOrCreateNpcMovementRouteState(
    string runtimeNpcId)
    {
        if (!_npcMovementRoutes.TryGetValue(
                runtimeNpcId,
                out NpcMovementRouteState routeState))
        {
            routeState = new NpcMovementRouteState();
            _npcMovementRoutes[runtimeNpcId] = routeState;
        }

        return routeState;
    }

    private Vector2 GetNpcSafeFacingDirection(
    SystemNpcRuntimeState npc)
    {
        Vector3 facing = npc.FacingDirection;
        facing.z = 0f;

        if (facing.sqrMagnitude > DirectionThresholdSqrMagnitude)
            return new Vector2(facing.x, facing.y).normalized;

        Vector3 tickDirection = npc.TickMovementDirection;
        tickDirection.z = 0f;

        if (tickDirection.sqrMagnitude > DirectionThresholdSqrMagnitude)
            return new Vector2(tickDirection.x, tickDirection.y).normalized;

        return Vector2.up;
    }

    private float GetNpcRoutePlanStepDistance(
    float speed)
    {
        int substepsPerTick =
            _configService != null &&
            _configService.ShipMovementConfig != null
                ? _configService.ShipMovementConfig.RouteSubstepsPerTick
                : 10;

        return Mathf.Max(
            ArrivalDistanceThreshold,
            speed / Mathf.Max(1, substepsPerTick));
    }

    private int GetNpcRoutePlanMaxSteps(
        float waypointPathLength,
        float turnRadius,
        float routeStepDistance)
    {
        float expectedLength =
            Mathf.Max(0f, waypointPathLength) +
            Mathf.Max(0f, turnRadius) *
            Mathf.PI *
            2f;

        int steps =
            Mathf.CeilToInt(
                expectedLength /
                Mathf.Max(ArrivalDistanceThreshold, routeStepDistance)) + 64;

        return Mathf.Clamp(
            steps,
            32,
            NpcRoutePlanMaxSteps);
    }

    private float GetNpcIntermediateWaypointArrivalDistanceThreshold(
    IReadOnlyList<Vector3> waypoints,
    float routeStepDistance)
    {
        if (waypoints == null ||
            waypoints.Count <= 2)
        {
            return ArrivalDistanceThreshold;
        }

        return Mathf.Max(
            ArrivalDistanceThreshold,
            routeStepDistance * 2f);
    }

    private float GetNpcRouteStraightExitAngleDegrees()
    {
        if (_configService == null ||
            _configService.ShipMovementConfig == null)
        {
            return 3f;
        }

        return _configService.ShipMovementConfig.RouteStraightExitAngleDegrees;
    }

    private float GetNpcPathLength(
        IReadOnlyList<Vector3> path)
    {
        if (path == null ||
            path.Count <= 1)
        {
            return 0f;
        }

        float length = 0f;

        for (int i = 1; i < path.Count; i++)
            length += Vector3.Distance(path[i - 1], path[i]);

        return length;
    }

    private Vector3 GetNpcPointOnPathAtDistance(
        IReadOnlyList<Vector3> path,
        float distance)
    {
        if (path == null ||
            path.Count == 0)
        {
            return Vector3.zero;
        }

        if (path.Count == 1)
            return path[0];

        float remainingDistance =
            Mathf.Max(0f, distance);

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 from = path[i - 1];
            Vector3 to = path[i];

            float segmentDistance =
                Vector3.Distance(from, to);

            if (segmentDistance <= DirectionThresholdSqrMagnitude)
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

    private Vector2 GetNpcDirectionOnPathAtDistance(
        IReadOnlyList<Vector3> path,
        float distance)
    {
        if (path == null ||
            path.Count <= 1)
        {
            return Vector2.up;
        }

        float remainingDistance =
            Mathf.Max(0f, distance);

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 from = path[i - 1];
            Vector3 to = path[i];

            float segmentDistance =
                Vector3.Distance(from, to);

            if (segmentDistance <= DirectionThresholdSqrMagnitude)
                continue;

            if (remainingDistance <= segmentDistance)
            {
                Vector2 direction =
                    new Vector2(
                        to.x - from.x,
                        to.y - from.y);

                return direction.sqrMagnitude > DirectionThresholdSqrMagnitude
                    ? direction.normalized
                    : Vector2.up;
            }

            remainingDistance -= segmentDistance;
        }

        Vector3 previous = path[path.Count - 2];
        Vector3 last = path[path.Count - 1];

        Vector2 fallback =
            new Vector2(
                last.x - previous.x,
                last.y - previous.y);

        return fallback.sqrMagnitude > DirectionThresholdSqrMagnitude
            ? fallback.normalized
            : Vector2.up;
    }



    private float CalculateProgress01(
        Vector3 startPosition,
        Vector3 destinationPosition,
        Vector3 currentPosition)
    {
        float totalDistance =
            Vector3.Distance(
                startPosition,
                destinationPosition);

        if (totalDistance <= ArrivalDistanceThreshold)
            return 1f;

        float remainingDistance =
            Vector3.Distance(
                currentPosition,
                destinationPosition);

        return Mathf.Clamp01(
            1f - remainingDistance / totalDistance);
    }

    private Vector3 GetSunSafeNextTargetPosition(
        SystemNpcRuntimeState npc,
        Vector3 finalTargetPosition)
    {
        if (npc == null)
            return finalTargetPosition;

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        if (starSystem == null || starSystem.Sun == null)
        {
            ClearSunAvoidanceRoute(npc.RuntimeNpcId);
            return finalTargetPosition;
        }

        SunConfig sun = starSystem.Sun;

        Vector3 sunCenter = new Vector3(
            sun.LocalOffset.x,
            sun.LocalOffset.y,
            npc.CurrentPosition.z);

        float sunRadius = Mathf.Max(0f, GetSunWorldSize(sun) * 0.5f);
        float avoidanceRadius = sunRadius + SunAvoidanceSafetyMargin;

        SunAvoidanceRouteState routeState = GetOrCreateSunAvoidanceRoute(
            npc,
            finalTargetPosition,
            sunCenter,
            avoidanceRadius);

        if (routeState == null)
            return finalTargetPosition;

        while (routeState.WaypointIndex < routeState.Waypoints.Count - 1 &&
               Vector3.Distance(
                   npc.CurrentPosition,
                   routeState.Waypoints[routeState.WaypointIndex]) <= ArrivalDistanceThreshold)
        {
            routeState.WaypointIndex++;
        }

        if (routeState.WaypointIndex >= routeState.Waypoints.Count - 1)
            return finalTargetPosition;

        return routeState.Waypoints[routeState.WaypointIndex];
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

    private SunAvoidanceRouteState GetOrCreateSunAvoidanceRoute(
        SystemNpcRuntimeState npc,
        Vector3 finalTargetPosition,
        Vector3 sunCenter,
        float avoidanceRadius)
    {
        if (string.IsNullOrWhiteSpace(npc.RuntimeNpcId))
            return null;

        bool shouldBuildRoute =
            !_sunAvoidanceRoutes.TryGetValue(npc.RuntimeNpcId, out SunAvoidanceRouteState routeState) ||
            routeState.Waypoints.Count < 2 ||
            routeState.WaypointIndex >= routeState.Waypoints.Count ||
            HasSunAvoidanceRouteTargetChanged(routeState, npc) ||
            ShouldRefreshSunAvoidanceFinalLeg(routeState, finalTargetPosition);

        if (!shouldBuildRoute)
            return routeState;

        SystemTravelSunAvoidancePath2A.BuildPath(
            _sunAvoidancePath,
            npc.CurrentPosition,
            finalTargetPosition,
            sunCenter,
            avoidanceRadius,
            SunAvoidanceArcSegments);

        if (_sunAvoidancePath.Count <= 2)
        {
            ClearSunAvoidanceRoute(npc.RuntimeNpcId);
            return null;
        }

        if (routeState == null)
            routeState = new SunAvoidanceRouteState();

        routeState.Waypoints.Clear();
        routeState.Waypoints.AddRange(_sunAvoidancePath);
        routeState.Destination = finalTargetPosition;
        routeState.WaypointIndex = 1;
        routeState.TravelState = npc.TravelState;
        routeState.TargetSystemId = npc.TargetSystemId;
        routeState.TargetPlanetId = npc.TargetPlanetId;
        routeState.CurrentTargetRuntimeNpcId = npc.CurrentTargetRuntimeNpcId;

        _sunAvoidanceRoutes[npc.RuntimeNpcId] = routeState;

        return routeState;
    }

    private bool HasSunAvoidanceRouteTargetChanged(
        SunAvoidanceRouteState routeState,
        SystemNpcRuntimeState npc)
    {
        if (routeState == null || npc == null)
            return true;

        return routeState.TravelState != npc.TravelState ||
               routeState.TargetSystemId != npc.TargetSystemId ||
               routeState.TargetPlanetId != npc.TargetPlanetId ||
               routeState.CurrentTargetRuntimeNpcId != npc.CurrentTargetRuntimeNpcId;
    }

    private bool ShouldRefreshSunAvoidanceFinalLeg(
        SunAvoidanceRouteState routeState,
        Vector3 finalTargetPosition)
    {
        if (routeState == null)
            return true;

        if (routeState.WaypointIndex < routeState.Waypoints.Count - 1)
            return false;

        return Vector3.Distance(routeState.Destination, finalTargetPosition) >
               SunAvoidanceDestinationRefreshThreshold;
    }

    private void AdvanceSunAvoidanceRoute(SystemNpcRuntimeState npc)
    {
        if (npc == null || string.IsNullOrWhiteSpace(npc.RuntimeNpcId))
            return;

        if (!_sunAvoidanceRoutes.TryGetValue(npc.RuntimeNpcId, out SunAvoidanceRouteState routeState))
            return;

        if (routeState.WaypointIndex < routeState.Waypoints.Count - 1)
            routeState.WaypointIndex++;
    }

    private void ClearSunAvoidanceRoute(string runtimeNpcId)
    {
        if (string.IsNullOrWhiteSpace(runtimeNpcId))
            return;

        _sunAvoidanceRoutes.Remove(runtimeNpcId);
    }

    private void CompleteMovement(SystemNpcRuntimeState npc, int currentTick)
    {
        npc.CurrentPosition = npc.TargetPosition;
        npc.TravelProgress01 = 1f;

        bool completedSystemTravel =
            npc.TravelState == SystemNpcTravelState.TravelingToAnotherSystem;

        switch (npc.TravelState)
        {
            case SystemNpcTravelState.TravelingInsideSystem:
                npc.IsOnPlanet = true;
                npc.TravelState = SystemNpcTravelState.OnPlanet;
                npc.CurrentPlanetId = npc.TargetPlanetId;
                break;

            case SystemNpcTravelState.TravelingToAnotherSystem:
                CompleteSystemTravel(npc);
                break;

            case SystemNpcTravelState.Patrolling:
                npc.TravelState = SystemNpcTravelState.Idle;
                break;

            case SystemNpcTravelState.EngagingEnemy:
                npc.TravelState = SystemNpcTravelState.Idle;
                break;
        }

        npc.StartPosition = npc.CurrentPosition;

        if (!completedSystemTravel)
            npc.TargetPosition = Vector3.zero;

        _eventBus.Publish(new SystemNpcTravelStateChangedEvent(
            npc.RuntimeNpcId,
            npc,
            npc.TravelState,
            npc.CurrentSystemId));

        _behaviorService.CompleteBehavior(npc, currentTick);

        LogCustom(
            "Movement complete. " +
            "NPC: " + npc.RuntimeNpcId + ", " +
            "State: " + npc.TravelState + ", " +
            "Behavior: " + npc.CurrentBehavior);
    }

    private void CompleteSystemTravel(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return;

        LogCustom("started");

        if (!string.IsNullOrWhiteSpace(npc.TargetSystemId))
        {
            string arrivedSystemId =
                npc.TargetSystemId;

            npc.CurrentSystemId = arrivedSystemId;
            npc.CurrentPosition = npc.TargetSystemEntryPoint;

            npc.TargetSystemId = null;
            npc.TargetSystemExitPoint = Vector3.zero;
            npc.TargetSystemEntryPoint = Vector3.zero;

            ApplyInitialFacingToSun(npc, arrivedSystemId);
        }

        npc.IsOnPlanet = false;
        npc.TravelState = SystemNpcTravelState.Idle;
    }

    private void ApplyInitialFacingToSun(
    SystemNpcRuntimeState npc,
    string systemId)
    {
        if (npc == null)
            return;

        Vector3 directionToSun =
            ResolveDirectionToSun(
                systemId,
                npc.CurrentPosition);

        npc.FacingDirection = directionToSun;
        npc.TickMovementDirection = directionToSun;

        npc.StartPosition = npc.CurrentPosition;
        npc.TargetPosition = npc.CurrentPosition + directionToSun;
        npc.CurrentMovementTargetPosition = npc.TargetPosition;
        npc.TickMovementTargetPosition = npc.TargetPosition;

        npc.TickMovementDirectionTick = -1;
        npc.TickMovementArrived = false;
        npc.TravelProgress01 = 0f;
    }

    private Vector3 ResolveDirectionToSun(
    string systemId,
    Vector3 currentPosition)
    {
        if (_configService == null)
            return Vector3.up;

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(systemId);

        if (starSystem == null || starSystem.Sun == null)
            return Vector3.up;

        Vector2 sunOffset =
            starSystem.Sun.LocalOffset;

        Vector3 sunPosition =
            new Vector3(
                sunOffset.x,
                sunOffset.y,
                currentPosition.z);

        Vector3 directionToSun =
            sunPosition - currentPosition;

        directionToSun.z = 0f;

        if (directionToSun.sqrMagnitude < 0.0001f)
            return Vector3.up;

        return directionToSun.normalized;
    }

    private sealed class SunAvoidanceRouteState
    {
        public readonly List<Vector3> Waypoints = new();
        public Vector3 Destination;
        public int WaypointIndex;
        public SystemNpcTravelState TravelState;
        public string TargetSystemId;
        public string TargetPlanetId;
        public string CurrentTargetRuntimeNpcId;
    }

    private sealed class NpcMovementRouteState
    {
        public readonly List<Vector3> Path = new();
        public Vector3 Destination;
        public float DistanceTravelled;
        public int Tick = -1;
    }
}
