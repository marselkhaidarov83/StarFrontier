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
    private IGameTimeService _gameTimeService;
    private readonly SimpleEventBus _eventBus;
    private readonly ISystemShipRouteService2A _shipRouteService;
    private readonly SystemShipRouteResult2A _npcRouteBuildResult =
        new SystemShipRouteResult2A();

    private readonly List<Vector3> _sunAvoidancePath = new();
    private readonly Dictionary<string, SunAvoidanceRouteState> _sunAvoidanceRoutes = new();

    private const int NpcRoutePlanMaxSteps = 8192;
    private readonly Dictionary<string, NpcMovementRouteState> _npcMovementRoutes = new();
    private readonly List<Vector3> _npcRouteWaypointsBuffer = new();
    private readonly List<Vector3> _npcRoutePreviewPathBuffer = new();

    public SystemNpcMovementService()
    {
        _debugEnabled = false;
        _debugStop = true;

        _runtimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _behaviorService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcBehaviorService>();
        _routeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcMovementRouteService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _shipRouteService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemShipRouteService2A>();

        LogCustom(
            "[NpcRouteDebug] Service created | " +
            "RuntimeService = " + (_runtimeService != null) +
            " | BehaviorService = " + (_behaviorService != null) +
            " | RouteService = " + (_routeService != null) +
            " | ConfigService = " + (_configService != null) +
            " | ShipRouteService = " + (_shipRouteService != null) +
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
            {
                if (IsMilitaryDebugNpc(npc) &&
                    npc.CurrentBehavior == SystemNpcBehaviorType.PlanetToPlanetTravel)
                {
                    LogCustom(
                        "[NPC-MILITARY-MOVEMENT] Tick blocked for PlanetToPlanetTravel. " +
                        "Npc=" + npc.RuntimeNpcId +
                        ", Reason=" + GetMovementBlockReason(npc) +
                        ", System=" + starSystem.Id +
                        ", Behavior=" + npc.CurrentBehavior +
                        ", TravelState=" + npc.TravelState +
                        ", IsOnPlanet=" + npc.IsOnPlanet +
                        ", CurrentPlanet=" + npc.CurrentPlanetId +
                        ", TargetPlanet=" + npc.TargetPlanetId +
                        ", Position=" + npc.CurrentPosition +
                        ", TargetPosition=" + npc.TargetPosition +
                        ", Tick=" + currentTick);
                }

                continue;
            }

            TickNpcMovement(npc, deltaTime, currentTick);
        }
    }

    private string GetMovementBlockReason(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return "NpcNull";

        if (!npc.IsAlive)
            return "NotAlive";

        if (npc.IsOnPlanet)
            return "IsOnPlanet";

        if (npc.TravelState == SystemNpcTravelState.Idle)
            return "TravelStateIdle";

        if (npc.TravelState == SystemNpcTravelState.OnPlanet)
            return "TravelStateOnPlanet";

        return "Unknown";
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
        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-MOVEMENT] Tick start. " +
                "Npc=" + npc.RuntimeNpcId +
                ", Behavior=" + npc.CurrentBehavior +
                ", TravelState=" + npc.TravelState +
                ", IsOnPlanet=" + npc.IsOnPlanet +
                ", CurrentPlanet=" + npc.CurrentPlanetId +
                ", TargetPlanet=" + npc.TargetPlanetId +
                ", Position=" + npc.CurrentPosition +
                ", TargetPosition=" + npc.TargetPosition +
                ", CurrentMovementTargetPosition=" + npc.CurrentMovementTargetPosition +
                ", TickMovementTargetPosition=" + npc.TickMovementTargetPosition +
                ", TickMovementDirectionTick=" + npc.TickMovementDirectionTick +
                ", TickMovementArrived=" + npc.TickMovementArrived +
                ", Speed=" + npc.Speed +
                ", DeltaTime=" + deltaTime +
                ", Tick=" + currentTick);
        }

        if (npc.TargetPosition == Vector3.zero)
        {
            npc.StartPosition = npc.CurrentPosition;
            npc.TravelProgress01 = 0f;

            if (IsMilitaryDebugNpc(npc))
            {
                LogCustom(
                    "[NPC-MILITARY-MOVEMENT] TargetPosition was zero, reset start/progress. " +
                    "Npc=" + npc.RuntimeNpcId +
                    ", StartPosition=" + npc.StartPosition);
            }
        }

        EnsureTickMovementDirection(npc, currentTick);

        if (npc.TickMovementArrived)
        {
            if (IsMilitaryDebugNpc(npc))
            {
                LogCustom(
                    "[NPC-MILITARY-MOVEMENT] Tick skipped: TickMovementArrived. " +
                    "Npc=" + npc.RuntimeNpcId +
                    ", Behavior=" + npc.CurrentBehavior +
                    ", TravelState=" + npc.TravelState +
                    ", Position=" + npc.CurrentPosition +
                    ", TargetPosition=" + npc.TargetPosition);
            }

            return;
        }

        if (!_npcMovementRoutes.TryGetValue(
                npc.RuntimeNpcId,
                out NpcMovementRouteState routeState) ||
            routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1 ||
            !IsSameNpcMovementRouteContext(routeState, npc))
        {
            if (IsMilitaryDebugNpc(npc))
            {
                LogCustom(
                    "[NPC-MILITARY-MOVEMENT] Tick skipped: no valid route for current behavior. " +
                    "Npc=" + npc.RuntimeNpcId +
                    ", Behavior=" + npc.CurrentBehavior +
                    ", TravelState=" + npc.TravelState +
                    ", HasRoute=" + (routeState != null) +
                    ", HasPath=" + (routeState != null && routeState.Path != null) +
                    ", PathCount=" + (routeState != null && routeState.Path != null ? routeState.Path.Count : 0) +
                    ", RouteBehavior=" + (routeState != null ? routeState.BehaviorType : SystemNpcBehaviorType.None) +
                    ", RouteTravelState=" + (routeState != null ? routeState.TravelState : SystemNpcTravelState.Idle) +
                    ", Position=" + npc.CurrentPosition +
                    ", TargetPosition=" + npc.TargetPosition +
                    ", TargetPlanet=" + npc.TargetPlanetId);
            }

            return;
        }

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                npc.TargetPosition);

        float totalRouteLength =
            GetNpcPathLength(routeState.Path);

        float distanceToTarget =
            Vector3.Distance(
                npc.CurrentPosition,
                npc.TargetPosition);

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-MOVEMENT] Route state. " +
                "Npc=" + npc.RuntimeNpcId +
                ", Behavior=" + npc.CurrentBehavior +
                ", TravelState=" + npc.TravelState +
                ", RouteDestination=" + routeState.Destination +
                ", PathCount=" + routeState.Path.Count +
                ", TotalLength=" + totalRouteLength +
                ", DistanceTravelled=" + routeState.DistanceTravelled +
                ", CurrentPosition=" + npc.CurrentPosition +
                ", TargetPosition=" + npc.TargetPosition +
                ", DistanceToTarget=" + distanceToTarget +
                ", ArrivalThresholdUsed=" + arrivalThreshold);
        }

        if (totalRouteLength <= arrivalThreshold)
        {
            if (distanceToTarget > arrivalThreshold)
            {
                if (IsMilitaryDebugNpc(npc))
                {
                    LogCustom(
                        "[NPC-MILITARY-MOVEMENT] Short route ignored: target is still far. " +
                        "Npc=" + npc.RuntimeNpcId +
                        ", Behavior=" + npc.CurrentBehavior +
                        ", TravelState=" + npc.TravelState +
                        ", TotalLength=" + totalRouteLength +
                        ", DistanceToTarget=" + distanceToTarget +
                        ", ArrivalThresholdUsed=" + arrivalThreshold +
                        ", CurrentPosition=" + npc.CurrentPosition +
                        ", TargetPosition=" + npc.TargetPosition +
                        ", TargetPlanet=" + npc.TargetPlanetId);
                }

                ClearNpcMovementRoute(npc.RuntimeNpcId);
                return;
            }

            if (IsMilitaryDebugNpc(npc))
            {
                LogCustom(
                    "[NPC-MILITARY-MOVEMENT] Completing immediately: route shorter than threshold. " +
                    "Npc=" + npc.RuntimeNpcId +
                    ", Behavior=" + npc.CurrentBehavior +
                    ", TravelState=" + npc.TravelState +
                    ", TotalLength=" + totalRouteLength +
                    ", Threshold=" + arrivalThreshold +
                    ", CurrentPosition=" + npc.CurrentPosition +
                    ", TargetPosition=" + npc.TargetPosition +
                    ", TargetPlanet=" + npc.TargetPlanetId);
            }

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

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-MOVEMENT] Move step. " +
                "Npc=" + npc.RuntimeNpcId +
                ", Behavior=" + npc.CurrentBehavior +
                ", TravelState=" + npc.TravelState +
                ", MovementDistance=" + movementDistance +
                ", PreviousDistance=" + routeState.DistanceTravelled +
                ", NextDistance=" + nextDistance +
                ", Remaining=" + (totalRouteLength - nextDistance) +
                ", OldPosition=" + npc.CurrentPosition +
                ", NewPosition=" + newPosition +
                ", TargetPosition=" + npc.TargetPosition +
                ", TargetPlanet=" + npc.TargetPlanetId +
                ", RouteDirection=" + routeDirection);
        }

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

        if (totalRouteLength - nextDistance <= arrivalThreshold)
        {
            float distanceToRealTarget =
                Vector3.Distance(
                    npc.CurrentPosition,
                    npc.TargetPosition);

            if (distanceToRealTarget > arrivalThreshold)
            {
                if (IsMilitaryDebugNpc(npc))
                {
                    LogCustom(
                        "[NPC-MILITARY-MOVEMENT] Complete blocked after move: real target is still far. " +
                        "Npc=" + npc.RuntimeNpcId +
                        ", Behavior=" + npc.CurrentBehavior +
                        ", TravelState=" + npc.TravelState +
                        ", RouteRemaining=" + (totalRouteLength - nextDistance) +
                        ", DistanceToRealTarget=" + distanceToRealTarget +
                        ", ArrivalThresholdUsed=" + arrivalThreshold +
                        ", CurrentPosition=" + npc.CurrentPosition +
                        ", TargetPosition=" + npc.TargetPosition +
                        ", TargetPlanet=" + npc.TargetPlanetId);
                }

                ClearNpcMovementRoute(npc.RuntimeNpcId);
                return;
            }

            if (IsMilitaryDebugNpc(npc))
            {
                LogCustom(
                    "[NPC-MILITARY-MOVEMENT] Completing: remaining under threshold. " +
                    "Npc=" + npc.RuntimeNpcId +
                    ", Behavior=" + npc.CurrentBehavior +
                    ", TravelState=" + npc.TravelState +
                    ", Remaining=" + (totalRouteLength - nextDistance) +
                    ", Threshold=" + arrivalThreshold +
                    ", CurrentPosition=" + npc.CurrentPosition +
                    ", TargetPosition=" + npc.TargetPosition +
                    ", TargetPlanet=" + npc.TargetPlanetId +
                    ", TravelProgress01=" + npc.TravelProgress01);
            }

            CompleteMovement(npc, currentTick);
        }
    }

    private void EnsureTickMovementDirection(SystemNpcRuntimeState npc, int currentTick)
    {
        Vector3 finalTargetPosition =
            _routeService.GetNextTargetPosition(npc);

        finalTargetPosition.z = -2f;
        npc.TargetPosition = finalTargetPosition;

        float distanceToFinalTarget =
            Vector3.Distance(
                npc.CurrentPosition,
                finalTargetPosition);

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                finalTargetPosition);

        if (npc.TravelState == SystemNpcTravelState.Patrolling &&
            distanceToFinalTarget <= arrivalThreshold + 0.5f)
        {
            if (IsMilitaryDebugNpc(npc))
            {
                LogCustom(
                    "[NPC-MILITARY-ROUTE] Completing patrol before route build: already near destination. " +
                    "Npc=" + npc.RuntimeNpcId +
                    ", CurrentPosition=" + npc.CurrentPosition +
                    ", FinalTarget=" + finalTargetPosition +
                    ", Distance=" + distanceToFinalTarget +
                    ", ArrivalThresholdUsed=" + arrivalThreshold);
            }

            CompleteMovement(npc, currentTick);
            return;
        }

        float routeReuseDistanceThreshold = GetNpcRouteReuseDistanceThreshold(npc);

        bool hasReusableRoute =
            _npcMovementRoutes.TryGetValue(npc.RuntimeNpcId, out NpcMovementRouteState routeState) &&
            routeState != null &&
            routeState.Path != null &&
            routeState.Path.Count > 1 &&
            IsSameNpcMovementRouteContext(routeState, npc) &&
            Vector3.Distance(routeState.Destination, finalTargetPosition) <= routeReuseDistanceThreshold;


        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-ROUTE] Ensure route. " +
                "Npc=" + npc.RuntimeNpcId +
                ", Behavior=" + npc.CurrentBehavior +
                ", TravelState=" + npc.TravelState +
                ", CurrentPosition=" + npc.CurrentPosition +
                ", FinalTarget=" + finalTargetPosition +
                ", TargetSystem=" + npc.TargetSystemId +
                ", TargetPlanet=" + npc.TargetPlanetId +
                ", HasReusableRoute=" + hasReusableRoute +
                ", Tick=" + currentTick);
        }

        if (hasReusableRoute &&
            ShouldKeepNpcRouteUntilArrival(npc))
        {
            routeState.Tick = currentTick;

            float distancePerTick =
                Mathf.Max(0f, npc.Speed) *
                Mathf.Max(0.01f, GameTimeState.SecondsPerDay);

            float totalRouteLength =
                GetNpcPathLength(routeState.Path);

            float nextPreviewDistance =
                Mathf.Clamp(
                    routeState.DistanceTravelled + distancePerTick,
                    0f,
                    totalRouteLength);

            Vector3 movementTargetPosition =
                GetNpcPointOnPathAtDistance(
                    routeState.Path,
                    nextPreviewDistance);

            npc.CurrentMovementTargetPosition = movementTargetPosition;
            npc.TickMovementTargetPosition = movementTargetPosition;
            npc.TickMovementDirectionTick = currentTick;
            npc.TickMovementArrived = false;

            Vector2 direction =
                GetNpcDirectionOnPathAtDistance(
                    routeState.Path,
                    routeState.DistanceTravelled);

            if (direction.sqrMagnitude > DirectionThresholdSqrMagnitude)
            {
                npc.TickMovementDirection =
                    new Vector3(direction.x, direction.y, 0f);

                if (npc.FacingDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
                    npc.FacingDirection = npc.TickMovementDirection;
            }

            if (IsMilitaryDebugNpc(npc))
            {
                LogCustom(
                    "[NPC-MILITARY-ROUTE] Reusing active patrol route. " +
                    "Npc=" + npc.RuntimeNpcId +
                    ", Behavior=" + npc.CurrentBehavior +
                    ", TravelState=" + npc.TravelState +
                    ", PathCount=" + routeState.Path.Count +
                    ", PathLength=" + totalRouteLength +
                    ", DistanceTravelled=" + routeState.DistanceTravelled +
                    ", Destination=" + routeState.Destination +
                    ", CurrentPosition=" + npc.CurrentPosition +
                    ", MovementTarget=" + movementTargetPosition +
                    ", Tick=" + currentTick);
            }

            return;
        }

        if (npc.TickMovementDirectionTick == currentTick &&
            npc.TickMovementTargetPosition != Vector3.zero &&
            hasReusableRoute)
        {
            return;
        }

        npc.TickMovementArrived = false;

        if (!TryBuildNpcMovementRoutePath(npc, finalTargetPosition, _npcRoutePreviewPathBuffer))
        {
            ClearNpcMovementRoute(npc.RuntimeNpcId);

            npc.CurrentMovementTargetPosition = finalTargetPosition;
            npc.TickMovementTargetPosition = finalTargetPosition;
            npc.TickMovementDirectionTick = currentTick;

            if (IsMilitaryDebugNpc(npc))
            {
                LogCustom(
                    "[NPC-MILITARY-ROUTE] Build failed. Active route cleared. " +
                    "Npc=" + npc.RuntimeNpcId +
                    ", Behavior=" + npc.CurrentBehavior +
                    ", TravelState=" + npc.TravelState +
                    ", CurrentPosition=" + npc.CurrentPosition +
                    ", FinalTarget=" + finalTargetPosition +
                    ", Distance=" + Vector3.Distance(npc.CurrentPosition, finalTargetPosition) +
                    ", TargetSystem=" + npc.TargetSystemId +
                    ", TargetPlanet=" + npc.TargetPlanetId);
            }

            return;
        }

        routeState = GetOrCreateNpcMovementRouteState(npc.RuntimeNpcId);

        routeState.Path.Clear();
        routeState.Path.AddRange(_npcRoutePreviewPathBuffer);
        routeState.Destination = finalTargetPosition;
        routeState.DistanceTravelled = 0f;
        routeState.Tick = currentTick;

        SaveNpcMovementRouteContext(routeState, npc);

        float builtDistancePerTick =
            Mathf.Max(0f, npc.Speed) *
            Mathf.Max(0.01f, GameTimeState.SecondsPerDay);

        Vector3 builtMovementTargetPosition =
            GetNpcPointOnPathAtDistance(routeState.Path, builtDistancePerTick);

        npc.CurrentMovementTargetPosition = builtMovementTargetPosition;
        npc.TickMovementTargetPosition = builtMovementTargetPosition;
        npc.TickMovementDirectionTick = currentTick;

        Vector2 builtDirection =
            GetNpcDirectionOnPathAtDistance(routeState.Path, 0f);

        if (builtDirection.sqrMagnitude > DirectionThresholdSqrMagnitude)
        {
            npc.TickMovementDirection =
                new Vector3(builtDirection.x, builtDirection.y, 0f);
        }

        if (npc.FacingDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            npc.FacingDirection = npc.TickMovementDirection;

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-ROUTE] Build success. " +
                "Npc=" + npc.RuntimeNpcId +
                ", Behavior=" + npc.CurrentBehavior +
                ", TravelState=" + npc.TravelState +
                ", PathCount=" + routeState.Path.Count +
                ", PathLength=" + GetNpcPathLength(routeState.Path) +
                ", Destination=" + routeState.Destination +
                ", FirstPoint=" + routeState.Path[0] +
                ", LastPoint=" + routeState.Path[routeState.Path.Count - 1]);
        }
    }

    private bool ShouldKeepNpcRouteUntilArrival(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return false;

        if (npc.TravelState == SystemNpcTravelState.Patrolling)
            return true;

        if (npc.TravelState == SystemNpcTravelState.TravelingInsideSystem &&
            npc.CurrentBehavior == SystemNpcBehaviorType.PlanetToPlanetTravel)
        {
            return true;
        }

        if (npc.TravelState == SystemNpcTravelState.TravelingToAnotherSystem)
            return true;

        if (npc.TravelState == SystemNpcTravelState.EngagingEnemy)
            return true;

        return false;
    }

    private float GetNpcRouteReuseDistanceThreshold(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return ArrivalDistanceThreshold;

        if (npc.TravelState == SystemNpcTravelState.EngagingEnemy)
            return 40f;

        return ArrivalDistanceThreshold;
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

        if (_shipRouteService == null)
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

        IReadOnlyList<Vector3> path;
        float passedDistance;

        if (TryGetActiveNpcPreviewRoute(
                npc,
                destinationPosition,
                out NpcMovementRouteState activeRouteState))
        {
            path = activeRouteState.Path;

            passedDistance =
                Mathf.Clamp(
                    activeRouteState.DistanceTravelled,
                    0f,
                    GetNpcPathLength(activeRouteState.Path));
        }
        else
        {
            if (!TryBuildNpcMovementRoutePath(
                    npc,
                    destinationPosition,
                    _npcRoutePreviewPathBuffer))
            {
                return false;
            }

            path = _npcRoutePreviewPathBuffer;

            passedDistance =
                GetClosestDistanceOnNpcPath(
                    path,
                    npc.CurrentPosition);
        }

        return _shipRouteService.FillPreviewFromPath(
            path,
            Mathf.Max(0.01f, npc.Speed),
            preview,
            smallDotSpacing,
            maxBigDots,
            maxSmallDots,
            secondsPerTick,
            passedDistance,
            GetCurrentTickRemainingFactor());
    }

    private float GetClosestDistanceOnNpcPath(
    IReadOnlyList<Vector3> path,
    Vector3 position)
    {
        if (path == null ||
            path.Count <= 1)
        {
            return 0f;
        }

        float bestDistanceOnPath = 0f;
        float bestSqrDistance = float.MaxValue;
        float travelledDistance = 0f;

        Vector2 position2 =
            new Vector2(
                position.x,
                position.y);

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 from = path[i - 1];
            Vector3 to = path[i];

            Vector2 from2 =
                new Vector2(
                    from.x,
                    from.y);

            Vector2 to2 =
                new Vector2(
                    to.x,
                    to.y);

            Vector2 segment =
                to2 - from2;

            float segmentLength =
                segment.magnitude;

            if (segmentLength <= DirectionThresholdSqrMagnitude)
                continue;

            float t =
                Vector2.Dot(
                    position2 - from2,
                    segment) /
                Mathf.Max(
                    DirectionThresholdSqrMagnitude,
                    segment.sqrMagnitude);

            t = Mathf.Clamp01(t);

            Vector2 closestPoint =
                from2 + segment * t;

            float sqrDistance =
                (position2 - closestPoint).sqrMagnitude;

            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                bestDistanceOnPath =
                    travelledDistance +
                    segmentLength * t;
            }

            travelledDistance += segmentLength;
        }

        return Mathf.Max(0f, bestDistanceOnPath);
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

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                destinationPosition);

        float initialDistance =
            Vector3.Distance(
                npc.CurrentPosition,
                destinationPosition);

        bool debugMilitary =
            IsMilitaryDebugNpc(npc);

        if (debugMilitary)
        {
            LogCustom(
                "[NPC-MILITARY-ROUTE-BUILD] Start. " +
                "Npc=" + npc.RuntimeNpcId +
                ", Behavior=" + npc.CurrentBehavior +
                ", TravelState=" + npc.TravelState +
                ", CurrentSystem=" + npc.CurrentSystemId +
                ", CurrentPlanet=" + npc.CurrentPlanetId +
                ", TargetPlanet=" + npc.TargetPlanetId +
                ", CurrentPosition=" + npc.CurrentPosition +
                ", DestinationPosition=" + destinationPosition +
                ", InitialDistance=" + initialDistance +
                ", ArrivalThresholdUsed=" + arrivalThreshold +
                ", FacingDirection=" + npc.FacingDirection +
                ", TickMovementDirection=" + npc.TickMovementDirection +
                ", Speed=" + npc.Speed +
                ", TurnRadius=" + npc.TurnRadius);
        }

        if (initialDistance <= arrivalThreshold)
        {
            if (debugMilitary)
            {
                LogCustom(
                    "[NPC-MILITARY-ROUTE-BUILD] Rejected: already near destination. " +
                    "Npc=" + npc.RuntimeNpcId +
                    ", Distance=" + initialDistance +
                    ", ArrivalThresholdUsed=" + arrivalThreshold);
            }

            return false;
        }

        if (_shipRouteService == null)
        {
            if (debugMilitary)
            {
                LogCustom(
                    "[NPC-MILITARY-ROUTE-BUILD] Rejected: ship route service is null. " +
                    "Npc=" + npc.RuntimeNpcId);
            }

            return false;
        }

        if (debugMilitary)
        {
            BuildNpcTravelWaypoints(
                npc,
                destinationPosition,
                _npcRouteWaypointsBuffer);
        }

        SystemShipRouteRequest2A request =
            new SystemShipRouteRequest2A
            {
                SystemId = npc.CurrentSystemId,
                StartPosition = npc.CurrentPosition,
                DestinationPosition = destinationPosition,
                StartFacingDirection = GetNpcSafeFacingDirection(npc),
                TargetKind = npc.IsEnemy
                    ? SystemShipRouteTargetKind2A.Enemy
                    : SystemShipRouteTargetKind2A.Npc,
                Settings = CreateNpcRouteSettings(
                    npc,
                    arrivalThreshold,
                    debugMilitary)
            };

        bool routeBuilt =
            _shipRouteService.TryBuildRoute(
                request,
                _npcRouteBuildResult);

        if (routeBuilt)
        {
            routePath.AddRange(_npcRouteBuildResult.Path);
        }

        if (debugMilitary)
        {
            LogCustom(
                "[NPC-MILITARY-ROUTE-BUILD] Turn-radius result. " +
                "Npc=" + npc.RuntimeNpcId +
                ", RouteBuilt=" + routeBuilt +
                ", WaypointCount=" + _npcRouteWaypointsBuffer.Count +
                ", WaypointPathLength=" + TurnRadiusRouteMath2A.GetPathLength(_npcRouteWaypointsBuffer) +
                ", Waypoints=" + FormatNpcRoutePathForDebug(_npcRouteWaypointsBuffer) +
                ", FacingDirectionUsed=" + request.StartFacingDirection +
                ", SpeedUsed=" + _npcRouteBuildResult.EffectiveSpeed +
                ", TurnRadiusUsed=" + _npcRouteBuildResult.EffectiveTurnRadius +
                ", RouteStepDistance=" + GetNpcRoutePlanStepDistance(Mathf.Max(0.01f, npc.Speed)) +
                ", ArrivalThresholdUsed=" + arrivalThreshold +
                ", IntermediateWaypointArrivalDistanceThreshold=handled by SystemShipRouteService2A" +
                ", StraightExitAngleDegrees=" + GetNpcRouteStraightExitAngleDegrees() +
                ", MaxSteps=" + NpcRoutePlanMaxSteps +
                ", ResultPathCount=" + routePath.Count +
                ", ResultPathLength=" + _npcRouteBuildResult.PathLength +
                ", MaxAllowedRouteLength=handled by SystemShipRouteService2A" +
                ", ResultPath=" + FormatNpcRoutePathForDebug(routePath) +
                ", LastPointToDestinationDistance=" + GetNpcRouteLastPointDistanceToDestination(routePath, destinationPosition));
        }

        return routeBuilt &&
               routePath.Count > 1;
    }

    private SystemShipRouteSettings2A CreateNpcRouteSettings(
        SystemNpcRuntimeState npc,
        float arrivalThreshold,
        bool debugMilitary)
    {
        ShipMovementConfig movementConfig =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        return new SystemShipRouteSettings2A
        {
            Speed = Mathf.Max(0.01f, npc != null ? npc.Speed : 0f),
            TurnRadius = Mathf.Max(0f, npc != null ? npc.TurnRadius : 0f),
            ArrivalDistanceThreshold = arrivalThreshold,
            SunAvoidanceSafetyMargin = SunAvoidanceSafetyMargin,
            SunAvoidanceArcSegments = SunAvoidanceArcSegments,
            AllowSunAvoidance = true,
            RouteSubstepsPerTick = movementConfig != null ? movementConfig.RouteSubstepsPerTick : 10,
            RouteStraightExitAngleDegrees = movementConfig != null ? movementConfig.RouteStraightExitAngleDegrees : 3f,
            TurnRadiusAdjustmentStepPercent = movementConfig != null ? movementConfig.RouteTurnRadiusAdjustmentStepPercent : 5f,
            SpeedAdjustmentStepPercent = movementConfig != null ? movementConfig.RouteSpeedAdjustmentStepPercent : 2.5f,
            MinTurnRadiusAdjustmentFactor = movementConfig != null ? movementConfig.MinRouteTurnRadiusAdjustmentFactor : 0.05f,
            MinTurnRadiusAbsolute = movementConfig != null ? movementConfig.MinRouteTurnRadiusAbsolute : 30f,
            MaxRoutePlanSteps = NpcRoutePlanMaxSteps,
            SunAvoidanceTurnRouteReserveMultiplier = 1.5f,
            // DebugLog = debugMilitary ? message => LogCustom(message) : null,
            // DebugPrefix = debugMilitary && npc != null
            //     ? "[NPC-MILITARY-TURN-RADIUS] Npc=" + npc.RuntimeNpcId + ", "
            //     : string.Empty
            DebugLog = null,
            DebugPrefix = string.Empty
        };
    }

    private float GetNpcRouteArrivalDistanceThreshold(
    SystemNpcRuntimeState npc,
    Vector3 destinationPosition)
    {
        if (npc == null)
            return ArrivalDistanceThreshold;

        if (npc.CurrentBehavior != SystemNpcBehaviorType.PlanetToPlanetTravel &&
            npc.TravelState != SystemNpcTravelState.TravelingInsideSystem)
        {
            return ArrivalDistanceThreshold;
        }

        if (string.IsNullOrWhiteSpace(npc.TargetPlanetId))
            return ArrivalDistanceThreshold;

        PlanetConfig planet =
            _configService != null
                ? _configService.GetPlanetConfigById(npc.TargetPlanetId)
                : null;

        float planetRadius =
            GetPlanetWorldSize(planet) * 0.5f;

        return Mathf.Max(
            ArrivalDistanceThreshold,
            planetRadius);
    }

    private float GetPlanetWorldSize(PlanetConfig planet)
    {
        if (_configService != null &&
            _configService.SystemVisualConfig != null)
        {
            return _configService
                .SystemVisualConfig
                .GetPlanetWorldSize(planet);
        }

        return planet != null
            ? planet.VisualSize
            : 0f;
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

            if (IsMilitaryDebugNpc(npc))
            {
                LogCustom(
                    "[NPC-MILITARY-ROUTE-BUILD] Waypoints without sun avoidance. " +
                    "Npc=" + npc.RuntimeNpcId +
                    ", HasStarSystem=" + (starSystem != null) +
                    ", HasSun=" + (starSystem != null && starSystem.Sun != null) +
                    ", Waypoints=" + FormatNpcRoutePathForDebug(waypoints));
            }

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

        Vector2 safeFacingDirection =
            GetNpcSafeFacingDirection(npc);

        float safeTurnRadius =
            Mathf.Max(0f, npc.TurnRadius);

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-ROUTE-BUILD] Sun avoidance input. " +
                "Npc=" + npc.RuntimeNpcId +
                ", StarSystem=" + starSystem.Id +
                ", From=" + npc.CurrentPosition +
                ", To=" + destinationPosition +
                ", SunCenter=" + sunCenter +
                ", SunWorldRadius=" + sunRadius +
                ", SunAvoidanceSafetyMargin=" + SunAvoidanceSafetyMargin +
                ", AvoidanceRadius=" + avoidanceRadius +
                ", ArcSegments=" + SunAvoidanceArcSegments +
                ", FacingDirection=" + safeFacingDirection +
                ", TurnRadius=" + safeTurnRadius);

            LogCustom(
                "[NPC-MILITARY-ROUTE-BUILD] Sun avoidance distances. " +
                "Npc=" + npc.RuntimeNpcId +
                ", FromToSunDistance=" + Vector3.Distance(npc.CurrentPosition, sunCenter) +
                ", DestinationToSunDistance=" + Vector3.Distance(destinationPosition, sunCenter) +
                ", FromToDestinationDistance=" + Vector3.Distance(npc.CurrentPosition, destinationPosition));
        }

        SystemTravelSunAvoidancePath2A.BuildPath(
            waypoints,
            npc.CurrentPosition,
            destinationPosition,
            sunCenter,
            avoidanceRadius,
            SunAvoidanceArcSegments,
            false,
            safeFacingDirection,
            safeTurnRadius);

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-ROUTE-BUILD] Sun avoidance result. " +
                "Npc=" + npc.RuntimeNpcId +
                ", WaypointCount=" + waypoints.Count +
                ", WaypointPathLength=" + GetNpcPathLength(waypoints) +
                ", Waypoints=" + FormatNpcRoutePathForDebug(waypoints));
        }
    }

    private string FormatNpcRoutePathForDebug(
    IReadOnlyList<Vector3> path)
    {
        if (path == null)
            return "null";

        if (path.Count == 0)
            return "empty";

        const int maxPoints = 16;

        List<string> points =
            new List<string>();

        int visibleCount =
            Mathf.Min(path.Count, maxPoints);

        for (int i = 0; i < visibleCount; i++)
        {
            points.Add(
                i + ":" + path[i]);
        }

        if (path.Count > maxPoints)
        {
            points.Add("...");
            points.Add((path.Count - 1) + ":" + path[path.Count - 1]);
        }

        return string.Join(" | ", points);
    }

    private float GetNpcRouteLastPointDistanceToDestination(
    IReadOnlyList<Vector3> path,
    Vector3 destinationPosition)
    {
        if (path == null ||
            path.Count == 0)
        {
            return -1f;
        }

        return Vector3.Distance(
            path[path.Count - 1],
            destinationPosition);
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
        if (npc == null)
            return;

        SystemNpcBehaviorType completedBehavior =
            npc.CurrentBehavior;

        SystemNpcTravelState completedTravelState =
            npc.TravelState;

        Vector3 completedFromPosition =
            npc.CurrentPosition;

        Vector3 completedTargetPosition =
            npc.TargetPosition;

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                npc.TargetPosition);

        float completedDistanceToTarget =
            Vector3.Distance(
                npc.CurrentPosition,
                npc.TargetPosition);

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-MOVEMENT] CompleteMovement start. " +
                "Npc=" + npc.RuntimeNpcId +
                ", CompletedBehavior=" + completedBehavior +
                ", PrevBehaviorBeforeComplete=" + npc.PrevBehavior +
                ", CompletedTravelState=" + completedTravelState +
                ", IsOnPlanetBefore=" + npc.IsOnPlanet +
                ", CurrentPlanetBefore=" + npc.CurrentPlanetId +
                ", TargetPlanetBefore=" + npc.TargetPlanetId +
                ", CurrentPositionBefore=" + npc.CurrentPosition +
                ", TargetPositionBefore=" + npc.TargetPosition +
                ", DistanceToTargetBefore=" + completedDistanceToTarget +
                ", ArrivalThresholdUsed=" + arrivalThreshold +
                ", Tick=" + currentTick);
        }

        if (npc.TravelState == SystemNpcTravelState.TravelingInsideSystem &&
            completedDistanceToTarget > arrivalThreshold)
        {
            if (IsMilitaryDebugNpc(npc))
            {
                LogCustom(
                    "[NPC-MILITARY-MOVEMENT] CompleteMovement blocked: target planet is still far. " +
                    "Npc=" + npc.RuntimeNpcId +
                    ", Behavior=" + npc.CurrentBehavior +
                    ", TravelState=" + npc.TravelState +
                    ", CurrentPosition=" + npc.CurrentPosition +
                    ", TargetPosition=" + npc.TargetPosition +
                    ", DistanceToTarget=" + completedDistanceToTarget +
                    ", ArrivalThresholdUsed=" + arrivalThreshold +
                    ", TargetPlanet=" + npc.TargetPlanetId +
                    ", Tick=" + currentTick);
            }

            ClearNpcMovementRoute(npc.RuntimeNpcId);
            npc.TravelProgress01 = 0f;
            return;
        }

        ClearNpcMovementRoute(npc.RuntimeNpcId);

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

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-MOVEMENT] CompleteMovement state BEFORE behavior complete. " +
                "Npc=" + npc.RuntimeNpcId +
                ", CompletedBehavior=" + completedBehavior +
                ", CompletedTravelState=" + completedTravelState +
                ", FromPosition=" + completedFromPosition +
                ", CompletedTargetPosition=" + completedTargetPosition +
                ", CurrentBehaviorNow=" + npc.CurrentBehavior +
                ", PrevBehaviorNow=" + npc.PrevBehavior +
                ", TravelStateNow=" + npc.TravelState +
                ", IsOnPlanetNow=" + npc.IsOnPlanet +
                ", CurrentPlanetNow=" + npc.CurrentPlanetId +
                ", TargetPlanetNow=" + npc.TargetPlanetId +
                ", CurrentPositionNow=" + npc.CurrentPosition +
                ", TargetPositionNow=" + npc.TargetPosition +
                ", Tick=" + currentTick);
        }

        _eventBus.Publish(new SystemNpcTravelStateChangedEvent(
            npc.RuntimeNpcId,
            npc,
            npc.TravelState,
            npc.CurrentSystemId));

        _behaviorService.CompleteBehavior(npc, currentTick);

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-MOVEMENT] CompleteMovement end AFTER new behavior may be assigned. " +
                "Npc=" + npc.RuntimeNpcId +
                ", CompletedBehavior=" + completedBehavior +
                ", CompletedTravelState=" + completedTravelState +
                ", CurrentBehaviorAfter=" + npc.CurrentBehavior +
                ", PrevBehaviorAfter=" + npc.PrevBehavior +
                ", TravelStateAfter=" + npc.TravelState +
                ", IsOnPlanetAfter=" + npc.IsOnPlanet +
                ", CurrentPlanetAfter=" + npc.CurrentPlanetId +
                ", TargetPlanetAfter=" + npc.TargetPlanetId +
                ", PositionAfter=" + npc.CurrentPosition +
                ", TargetPositionAfter=" + npc.TargetPosition +
                ", Tick=" + currentTick);
        }
    }

    private void CompleteSystemTravel(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return;

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-MOVEMENT] CompleteSystemTravel start. " +
                "Npc=" + npc.RuntimeNpcId +
                ", FromSystem=" + npc.CurrentSystemId +
                ", ToSystem=" + npc.TargetSystemId +
                ", ExitPoint=" + npc.TargetSystemExitPoint +
                ", EntryPoint=" + npc.TargetSystemEntryPoint +
                ", CurrentPlanet=" + npc.CurrentPlanetId +
                ", Position=" + npc.CurrentPosition);
        }

        if (!string.IsNullOrWhiteSpace(npc.TargetSystemId))
        {
            string arrivedSystemId = npc.TargetSystemId;

            npc.CurrentSystemId = arrivedSystemId;
            npc.CurrentPosition = npc.TargetSystemEntryPoint;

            npc.TargetSystemId = null;
            npc.TargetSystemExitPoint = Vector3.zero;
            npc.TargetSystemEntryPoint = Vector3.zero;

            npc.CurrentPlanetId = null;
            npc.TargetPlanetId = null;

            ApplyInitialFacingToSun(npc, arrivedSystemId);
        }

        npc.IsOnPlanet = false;
        npc.TravelState = SystemNpcTravelState.Idle;

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-MOVEMENT] CompleteSystemTravel end. " +
                "Npc=" + npc.RuntimeNpcId +
                ", CurrentSystem=" + npc.CurrentSystemId +
                ", CurrentPlanet=" + npc.CurrentPlanetId +
                ", TargetPlanet=" + npc.TargetPlanetId +
                ", IsOnPlanet=" + npc.IsOnPlanet +
                ", TravelState=" + npc.TravelState +
                ", Position=" + npc.CurrentPosition);
        }
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
        public SystemNpcBehaviorType BehaviorType;
        public SystemNpcTravelState TravelState;
        public string TargetSystemId;
        public string TargetPlanetId;
        public string CurrentTargetRuntimeNpcId;
    }

    private bool IsMilitaryDebugNpc(SystemNpcRuntimeState npc)
    {
        return npc != null &&
               npc.IsAlly;
            //     &&
            //    (npc.AllyRole == AllyRole2A.Military ||
            //     npc.AllyRole == AllyRole2A.Science);
    }

    private void ClearNpcMovementRoute(string runtimeNpcId)
    {
        if (string.IsNullOrWhiteSpace(runtimeNpcId))
            return;

        _npcMovementRoutes.Remove(runtimeNpcId);
        ClearSunAvoidanceRoute(runtimeNpcId);
    }

    private bool IsSameNpcMovementRouteContext(
        NpcMovementRouteState routeState,
        SystemNpcRuntimeState npc)
    {
        if (routeState == null || npc == null)
            return false;

        return routeState.BehaviorType == npc.CurrentBehavior &&
               routeState.TravelState == npc.TravelState &&
               routeState.TargetSystemId == npc.TargetSystemId &&
               routeState.TargetPlanetId == npc.TargetPlanetId &&
               routeState.CurrentTargetRuntimeNpcId == npc.CurrentTargetRuntimeNpcId;
    }

    private void SaveNpcMovementRouteContext(
        NpcMovementRouteState routeState,
        SystemNpcRuntimeState npc)
    {
        if (routeState == null || npc == null)
            return;

        routeState.BehaviorType = npc.CurrentBehavior;
        routeState.TravelState = npc.TravelState;
        routeState.TargetSystemId = npc.TargetSystemId;
        routeState.TargetPlanetId = npc.TargetPlanetId;
        routeState.CurrentTargetRuntimeNpcId = npc.CurrentTargetRuntimeNpcId;
    }

    private float GetCurrentTickRemainingFactor()
    {
        if (_gameTimeService == null &&
            Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null)
        {
            Bootstrapper.Instance.ServiceRegistry.TryGet(
                out _gameTimeService);
        }

        if (_gameTimeService == null ||
            _gameTimeService.State == null)
        {
            return 1f;
        }

        float secondsPerTick =
            Mathf.Max(
                0.01f,
                GameTimeState.SecondsPerDay);

        float elapsedFactor =
            Mathf.Clamp01(
                _gameTimeService.State.Accumulator /
                secondsPerTick);

        return Mathf.Clamp01(
            1f - elapsedFactor);
    }
}
