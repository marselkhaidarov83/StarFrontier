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
    private readonly Dictionary<string, SystemBoundaryNavigation2A.BoundaryNavigationState> _npcBoundaryNavigationStates = new();
    private readonly List<Vector3> _npcRouteWaypointsBuffer = new();
    private readonly List<Vector3> _npcRoutePreviewPathBuffer = new();
    private readonly List<Vector3> _npcRouteTailPathBuffer = new();
    private readonly List<Vector3> _npcRouteSegmentPathBuffer = new();

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
                ", TickScaledDeltaTime=" + GetTickScaledDeltaTime(deltaTime) +
                ", SecondsPerTick=" + GameTimeState.SecondsPerDay +
                ", Tick=" + currentTick);
        }

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
            routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1 ||
            !IsSameNpcMovementRouteContext(routeState, npc))
        {
            return;
        }

        if (ConsumeNpcStartTurnInPlaceIfNeeded(
                npc,
                routeState,
                currentTick))
        {
            return;
        }

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                npc.TargetPosition);

        float totalRouteLength =
            GetNpcPathLength(routeState.Path);

        if (totalRouteLength <= arrivalThreshold)
        {
            CompleteMovement(npc, currentTick);
            return;
        }

        routeState.DistanceTravelled =
            Mathf.Clamp(
                routeState.DistanceTravelled,
                0f,
                totalRouteLength);

        float previousDistance =
            routeState.DistanceTravelled;

        float movementDistance =
            Mathf.Max(0f, npc.Speed) *
            GetTickScaledDeltaTime(deltaTime);

        float nextDistance =
            Mathf.Clamp(
                previousDistance + movementDistance,
                0f,
                totalRouteLength);

        Vector3 oldPosition =
            npc.CurrentPosition;

        Vector3 oldFacingDirection =
            npc.FacingDirection;

        Vector3 newPosition =
            GetNpcPointOnPathAtDistance(
                routeState.Path,
                nextDistance);

        Vector2 routeDirection =
            _shipRouteService != null
                ? _shipRouteService.GetDirectionOnPathAtDistance(
                    routeState.Path,
                    nextDistance)
                : GetNpcDirectionOnPathAtDistance(
                    routeState.Path,
                    nextDistance);

        bool movedAlongRoute =
            nextDistance > previousDistance + 0.001f &&
            routeDirection.sqrMagnitude > DirectionThresholdSqrMagnitude;

        if (movedAlongRoute)
        {
            Vector3 movementDirection =
                new Vector3(
                    routeDirection.x,
                    routeDirection.y,
                    0f).normalized;

            npc.FacingDirection = movementDirection;
            npc.TickMovementDirection = movementDirection;

            float turnAngle =
                GetSignedAngle(oldFacingDirection, movementDirection);

            if (IsNpcMovementDebugEnabled() &&
                Mathf.Abs(turnAngle) >= GetMovementTurnSpikeAngleDegrees())
            {
                LogNpcMovementDebug(
                    "[NPC-TURN-SPIKE]" +
                    " | Npc=" + npc.RuntimeNpcId +
                    " | Type=" + npc.NpcType +
                    " | TurnAngle=" + turnAngle.ToString("0.###") +
                    " | OldPosition=" + FormatVector3(oldPosition) +
                    " | NewPosition=" + FormatVector3(newPosition) +
                    " | OldFacing=" + FormatVector3(oldFacingDirection) +
                    " | NewFacing=" + FormatVector3(movementDirection) +
                    " | RouteDirection=" + FormatVector2(routeDirection) +
                    " | PreviousDistance=" + previousDistance.ToString("0.###") +
                    " | NextDistance=" + nextDistance.ToString("0.###") +
                    " | TotalRouteLength=" + totalRouteLength.ToString("0.###") +
                    " | TargetPosition=" + FormatVector3(npc.TargetPosition));
            }
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
                ClearNpcMovementRoute(npc.RuntimeNpcId);
                return;
            }

            CompleteMovement(npc, currentTick);
        }
    }

    private void EnsureTickMovementDirection(SystemNpcRuntimeState npc, int currentTick)
    {
        Vector3 rawFinalTargetPosition =
            _routeService.GetNextTargetPosition(npc);

        rawFinalTargetPosition.z = -2f;

        SystemBoundaryNavigation2A.BoundaryNavigationState boundaryState =
            GetOrCreateNpcBoundaryNavigationState(npc.RuntimeNpcId);

        Vector3 finalTargetPosition =
            SystemBoundaryNavigation2A.GetRouteDestinationInsideSystemBounds(
                npc.CurrentSystemId,
                npc.CurrentPosition,
                rawFinalTargetPosition,
                GetNpcSafeFacingDirection(npc),
                _configService != null ? _configService.ShipMovementConfig : null,
                boundaryState,
                currentTick,
                GetNpcBoundaryRouteStepDistance(npc),
                npc.TravelState == SystemNpcTravelState.EngagingEnemy,
                out bool isBoundaryAdjusted);

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

        if (npc.TravelState == SystemNpcTravelState.EngagingEnemy &&
            distanceToFinalTarget <= arrivalThreshold)
        {
            ClearNpcMovementRoute(npc.RuntimeNpcId);

            Vector3 holdPosition =
                npc.CurrentPosition;

            holdPosition.z = -2f;
            npc.CurrentPosition = holdPosition;
            npc.CurrentMovementTargetPosition = holdPosition;
            npc.TickMovementTargetPosition = holdPosition;
            npc.TickMovementDirectionTick = currentTick;
            npc.TickMovementArrived = true;

            return;
        }

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

        bool hasActiveRoute =
            _npcMovementRoutes.TryGetValue(
                npc.RuntimeNpcId,
                out NpcMovementRouteState routeState) &&
            routeState != null &&
            routeState.Path != null &&
            routeState.Path.Count > 1 &&
            IsSameNpcMovementRouteContext(routeState, npc);

        NpcMovementRouteState existingRouteState =
            hasActiveRoute
                ? routeState
                : null;

        if (hasActiveRoute &&
            routeState.IsBoundaryEscapeRoute &&
            IsNpcBoundaryRouteStillUseful(
                npc,
                routeState,
                finalTargetPosition) &&
            TryUpdateNpcMovementTargetFromRoute(
                npc,
                routeState,
                currentTick))
        {
            return;
        }

        if (hasActiveRoute &&
            IsNpcRouteRefreshBlockedByInitialTicks(
                npc,
                routeState,
                currentTick) &&
            TryUpdateNpcMovementTargetFromRoute(
                npc,
                routeState,
                currentTick))
        {
            return;
        }

        float routeReuseDistanceThreshold =
            GetNpcRouteReuseDistanceThreshold(npc);

        bool hasReusableRoute =
            hasActiveRoute &&
            Vector3.Distance(
                routeState.Destination,
                finalTargetPosition) <= routeReuseDistanceThreshold;

        if (hasReusableRoute &&
            ShouldKeepNpcRouteUntilArrival(npc) &&
            TryUpdateNpcMovementTargetFromRoute(
                npc,
                routeState,
                currentTick))
        {
            return;
        }

        if (npc.TickMovementDirectionTick == currentTick &&
            npc.TickMovementTargetPosition != Vector3.zero &&
            hasReusableRoute)
        {
            return;
        }

        npc.TickMovementArrived = false;

        if (!TryBuildNpcMovementRoutePath(
                npc,
                finalTargetPosition,
                _npcRoutePreviewPathBuffer,
                currentTick,
                out float builtRouteDistanceTravelled))
        {
            if (TryContinueCurrentNpcRouteAfterFailedRefresh(
                    npc,
                    existingRouteState,
                    finalTargetPosition,
                    currentTick))
            {
                return;
            }

            ClearNpcMovementRoute(npc.RuntimeNpcId);

            npc.CurrentMovementTargetPosition = finalTargetPosition;
            npc.TickMovementTargetPosition = finalTargetPosition;
            npc.TickMovementDirectionTick = currentTick;

            return;
        }

        routeState = GetOrCreateNpcMovementRouteState(npc.RuntimeNpcId);

        routeState.Path.Clear();
        routeState.Path.AddRange(_npcRoutePreviewPathBuffer);
        routeState.Destination = finalTargetPosition;
        routeState.Tick = currentTick;
        routeState.BuildTick = currentTick;
        routeState.IsBoundaryEscapeRoute =
            isBoundaryAdjusted ||
            SystemBoundaryNavigation2A.IsPositionNearSystemBounds(
                npc.CurrentSystemId,
                finalTargetPosition,
                _configService != null ? _configService.ShipMovementConfig : null);

        float totalRouteLength =
            GetNpcPathLength(routeState.Path);

        routeState.DistanceTravelled =
            Mathf.Clamp(
                builtRouteDistanceTravelled,
                0f,
                totalRouteLength);

        SaveNpcMovementRouteContext(routeState, npc);
        RebuildNpcMovementRoutePlan(routeState, npc);

        if (routeState.StartTurnInPlacePending)
        {
            npc.CurrentMovementTargetPosition = npc.CurrentPosition;
            npc.TickMovementTargetPosition = npc.CurrentPosition;
            npc.TickMovementDirectionTick = currentTick;
            return;
        }

        float builtDistancePerTick =
            Mathf.Max(0f, npc.Speed);

        float builtPreviewDistance =
            Mathf.Clamp(
                routeState.DistanceTravelled + builtDistancePerTick,
                0f,
                totalRouteLength);

        Vector3 builtMovementTargetPosition =
            GetNpcPointOnPathAtDistance(
                routeState.Path,
                builtPreviewDistance);

        npc.CurrentMovementTargetPosition = builtMovementTargetPosition;
        npc.TickMovementTargetPosition = builtMovementTargetPosition;
        npc.TickMovementDirectionTick = currentTick;
    }

    private bool IsNpcBoundaryRouteStillUseful(
        SystemNpcRuntimeState npc,
        NpcMovementRouteState routeState,
        Vector3 finalTargetPosition)
    {
        if (npc == null ||
            routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1)
        {
            return false;
        }

        if (!SystemBoundaryNavigation2A.IsPositionNearSystemBounds(
                npc.CurrentSystemId,
                finalTargetPosition,
                _configService != null ? _configService.ShipMovementConfig : null))
        {
            return false;
        }

        float totalRouteLength =
            GetNpcPathLength(routeState.Path);

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                finalTargetPosition);

        if (totalRouteLength <= arrivalThreshold)
            return false;

        return routeState.DistanceTravelled <
               totalRouteLength - arrivalThreshold;
    }

    private bool TryUpdateNpcMovementTargetFromRoute(
        SystemNpcRuntimeState npc,
        NpcMovementRouteState routeState,
        int currentTick)
    {
        if (npc == null ||
            routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1)
        {
            return false;
        }

        routeState.Tick = currentTick;

        if (routeState.StartTurnInPlacePending)
        {
            npc.CurrentMovementTargetPosition = npc.CurrentPosition;
            npc.TickMovementTargetPosition = npc.CurrentPosition;
            npc.TickMovementDirectionTick = currentTick;
            npc.TickMovementArrived = false;
            return true;
        }

        float totalRouteLength =
            GetNpcPathLength(routeState.Path);

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                routeState.Destination);

        if (routeState.DistanceTravelled >= totalRouteLength - arrivalThreshold)
            return false;

        float distancePerTick =
            Mathf.Max(0f, npc.Speed);

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

        return true;
    }

    private bool IsNpcRouteRefreshBlockedByInitialTicks(
        SystemNpcRuntimeState npc,
        NpcMovementRouteState routeState,
        int currentTick)
    {
        if (!ShouldUseNpcRouteLockedPrefix(npc) ||
            routeState == null)
        {
            return false;
        }

        int blockedTicks =
            GetNpcRouteRefreshBlockedInitialTicks();

        if (blockedTicks <= 0)
            return false;

        if (routeState.BuildTick < 0)
            return false;

        int passedTicks =
            currentTick - routeState.BuildTick;

        if (passedTicks < 0)
            return false;

        return passedTicks < blockedTicks;
    }

    private int GetNpcRouteRefreshBlockedInitialTicks()
    {
        ShipMovementConfig movementConfig =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        return movementConfig != null
            ? movementConfig.MovingDestinationRouteRefreshBlockedInitialTicks
            : 1;
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
                    _npcRoutePreviewPathBuffer,
                    -1,
                    out float previewRouteDistanceTravelled))
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
        List<Vector3> routePath,
        int currentTick,
        out float routeDistanceTravelled)
    {
        routeDistanceTravelled = 0f;

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

        if (Vector3.Distance(
                npc.CurrentPosition,
                destinationPosition) <= arrivalThreshold)
        {
            return false;
        }

        if (_shipRouteService == null)
            return false;

        if (TryBuildNpcRouteWithPreservedPrefix(
                npc,
                destinationPosition,
                arrivalThreshold,
                routePath,
                currentTick,
                out routeDistanceTravelled))
        {
            return true;
        }

        Vector2 startFacingDirection =
            GetNpcSafeFacingDirection(npc);

        routeDistanceTravelled = 0f;

        return TryBuildNpcRoutePathFrom(
            npc,
            npc.CurrentPosition,
            destinationPosition,
            startFacingDirection,
            arrivalThreshold,
            routePath,
            currentTick);
    }

    private bool TryBuildNpcRoutePathFrom(
        SystemNpcRuntimeState npc,
        Vector3 startPosition,
        Vector3 destinationPosition,
        Vector2 startFacingDirection,
        float arrivalThreshold,
        List<Vector3> routePath,
        int currentTick)
    {
        if (npc == null ||
            routePath == null)
        {
            return false;
        }

        routePath.Clear();

        float boundaryStepDistance =
            GetNpcBoundaryRouteStepDistance(npc);

        Vector3 adjustedDestinationPosition =
            SystemBoundaryNavigation2A.GetRouteDestinationInsideSystemBounds(
                npc.CurrentSystemId,
                startPosition,
                destinationPosition,
                startFacingDirection,
                _configService != null ? _configService.ShipMovementConfig : null,
                GetOrCreateNpcBoundaryNavigationState(npc.RuntimeNpcId),
                currentTick,
                boundaryStepDistance,
                npc.TravelState == SystemNpcTravelState.EngagingEnemy,
                out _);

        adjustedDestinationPosition.z = -2f;

        if (Vector3.Distance(
                startPosition,
                adjustedDestinationPosition) <= arrivalThreshold)
        {
            return false;
        }

        SystemShipRouteRequest2A request =
            new SystemShipRouteRequest2A
            {
                SystemId = npc.CurrentSystemId,
                StartPosition = startPosition,
                DestinationPosition = adjustedDestinationPosition,
                StartFacingDirection = startFacingDirection,
                TargetKind = npc.IsEnemy
                    ? SystemShipRouteTargetKind2A.Enemy
                    : SystemShipRouteTargetKind2A.Npc,
                Settings = CreateNpcRouteSettings(
                    npc,
                    arrivalThreshold,
                    false)
            };

        bool routeBuilt =
            _shipRouteService.TryBuildRoute(
                request,
                _npcRouteBuildResult);

        if (!routeBuilt ||
            _npcRouteBuildResult.Path == null ||
            _npcRouteBuildResult.Path.Count <= 1)
        {
            return false;
        }

        if (ShouldRejectNpcRawFallbackRoute(
                npc,
                startFacingDirection,
                _npcRouteBuildResult))
        {
            return false;
        }

        if (ShouldRejectNpcStartTurnSpikeRoute(
                npc,
                startFacingDirection,
                _npcRouteBuildResult))
        {
            return false;
        }

        routePath.AddRange(_npcRouteBuildResult.Path);
        return true;
    }

    private float GetNpcBoundaryRouteStepDistance(
        SystemNpcRuntimeState npc)
    {
        float speed =
            npc != null
                ? Mathf.Max(0f, npc.Speed)
                : 0f;

        return Mathf.Clamp(
            speed * 2.5f,
            ArrivalDistanceThreshold * 4f,
            260f);
    }

    private bool ShouldRejectNpcStartTurnSpikeRoute(
        SystemNpcRuntimeState npc,
        Vector2 startFacingDirection,
        SystemShipRouteResult2A routeResult)
    {
        if (npc == null ||
            routeResult == null ||
            routeResult.Path == null ||
            routeResult.Path.Count <= 1 ||
            startFacingDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
        {
            return false;
        }

        Vector2 routeStartDirection =
            _shipRouteService != null
                ? _shipRouteService.GetDirectionOnPathAtDistance(
                    routeResult.Path,
                    0f)
                : GetNpcDirectionOnPathAtDistance(
                    routeResult.Path,
                    0f);

        if (routeStartDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            return false;

        float startTurnAngle =
            Mathf.Abs(
                Vector2.SignedAngle(
                    startFacingDirection.normalized,
                    routeStartDirection.normalized));

        float maxAllowedStartTurnAngle =
            Mathf.Clamp(
                GetMovementTurnSpikeAngleDegrees(),
                1f,
                179f);

        bool shouldReject =
            startTurnAngle > maxAllowedStartTurnAngle;

        if (shouldReject &&
            ShouldLogNpcRouteDecision())
        {
            LogNpcMovementDebug(
                "[NPC-ROUTE-START-SPIKE-REJECTED]" +
                " | Npc=" + npc.RuntimeNpcId +
                " | Type=" + npc.NpcType +
                " | StartTurnAngle=" + startTurnAngle.ToString("0.###") +
                " | MaxAllowed=" + maxAllowedStartTurnAngle.ToString("0.###") +
                " | StartFacing=" + FormatVector2(startFacingDirection) +
                " | RouteDirection=" + FormatVector2(routeStartDirection) +
                " | PathLength=" + routeResult.PathLength.ToString("0.###") +
                " | UsedRawSafeFallback=" + routeResult.UsedRawSafeFallback +
                " | UsedSunAvoidance=" + routeResult.UsedSunAvoidance +
                " | Destination=" + FormatVector3(routeResult.Path[routeResult.Path.Count - 1]));
        }

        return shouldReject;
    }

    private bool TryBuildNpcRouteWithPreservedPrefix(
        SystemNpcRuntimeState npc,
        Vector3 destinationPosition,
        float arrivalThreshold,
        List<Vector3> routePath,
        int currentTick,
        out float routeDistanceTravelled)
    {
        routeDistanceTravelled = 0f;

        if (!ShouldUseNpcRouteLockedPrefix(npc) ||
            routePath == null)
        {
            return false;
        }

        if (!_npcMovementRoutes.TryGetValue(
                npc.RuntimeNpcId,
                out NpcMovementRouteState routeState) ||
            routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1 ||
            !IsSameNpcMovementRouteContext(routeState, npc))
        {
            return false;
        }

        if (!TryGetNpcLockedPrefixState(
                npc,
                routeState,
                out float currentDistance,
                out float lockedPrefixDistance,
                out Vector3 lockedPrefixEndPosition,
                out Vector2 lockedPrefixEndFacingDirection))
        {
            return false;
        }

        if (!TryBuildNpcRoutePathFrom(
                npc,
                lockedPrefixEndPosition,
                destinationPosition,
                lockedPrefixEndFacingDirection,
                arrivalThreshold,
                _npcRouteTailPathBuffer,
                currentTick))
        {
            return false;
        }

        BuildNpcRoutePrefix(
            routeState.Path,
            lockedPrefixDistance,
            _npcRouteSegmentPathBuffer);

        if (_npcRouteSegmentPathBuffer.Count <= 1)
            return false;

        routePath.Clear();
        routePath.AddRange(_npcRouteSegmentPathBuffer);

        AppendNpcRouteTail(
            _npcRouteTailPathBuffer,
            routePath);

        if (routePath.Count <= 1)
            return false;

        routeDistanceTravelled =
            Mathf.Min(
                currentDistance,
                lockedPrefixDistance);

        if (ShouldLogNpcRouteDecision())
        {
            LogNpcMovementDebug(
                "[NPC-ROUTE-PREFIX-PRESERVED]" +
                " | Npc=" + npc.RuntimeNpcId +
                " | Type=" + npc.NpcType +
                " | CurrentDistance=" + currentDistance.ToString("0.###") +
                " | LockedPrefixDistance=" + lockedPrefixDistance.ToString("0.###") +
                " | PreservedDistance=" + routeDistanceTravelled.ToString("0.###") +
                " | PrefixEnd=" + FormatVector3(lockedPrefixEndPosition) +
                " | PrefixFacing=" + FormatVector2(lockedPrefixEndFacingDirection) +
                " | Destination=" + FormatVector3(destinationPosition) +
                " | PathCount=" + routePath.Count);
        }

        return true;
    }

    private bool TryGetNpcLockedPrefixState(
        SystemNpcRuntimeState npc,
        NpcMovementRouteState routeState,
        out float currentDistance,
        out float lockedPrefixDistance,
        out Vector3 lockedPrefixEndPosition,
        out Vector2 lockedPrefixEndFacingDirection)
    {
        currentDistance = 0f;
        lockedPrefixDistance = 0f;
        lockedPrefixEndPosition = Vector3.zero;
        lockedPrefixEndFacingDirection = Vector2.up;

        if (npc == null ||
            routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1)
        {
            return false;
        }

        int lockedPrefixSlots =
            GetNpcRouteLockedPrefixSlots();

        if (lockedPrefixSlots <= 0)
            return false;

        float totalPathLength =
            GetNpcPathLength(routeState.Path);

        if (totalPathLength <= ArrivalDistanceThreshold)
            return false;

        currentDistance =
            Mathf.Clamp(
                routeState.DistanceTravelled,
                0f,
                totalPathLength);

        if (currentDistance >= totalPathLength - ArrivalDistanceThreshold)
            return false;

        float routeStepDistance =
            GetNpcRoutePlanStepDistance(
                Mathf.Max(0f, npc.Speed));

        float lockedPrefixLength =
            Mathf.Max(
                ArrivalDistanceThreshold,
                routeStepDistance * lockedPrefixSlots);

        lockedPrefixDistance =
            Mathf.Clamp(
                currentDistance + lockedPrefixLength,
                0f,
                totalPathLength);

        if (lockedPrefixDistance <= currentDistance + ArrivalDistanceThreshold)
            return false;

        lockedPrefixEndPosition =
            GetNpcPointOnPathAtDistance(
                routeState.Path,
                lockedPrefixDistance);

        lockedPrefixEndFacingDirection =
            _shipRouteService != null
                ? _shipRouteService.GetDirectionOnPathAtDistance(
                    routeState.Path,
                    lockedPrefixDistance)
                : GetNpcDirectionOnPathAtDistance(
                    routeState.Path,
                    lockedPrefixDistance);

        if (lockedPrefixEndFacingDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            lockedPrefixEndFacingDirection = GetNpcSafeFacingDirection(npc);

        if (lockedPrefixEndFacingDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            return false;

        lockedPrefixEndFacingDirection.Normalize();
        return true;
    }

    private bool TryContinueCurrentNpcRouteAfterFailedRefresh(
        SystemNpcRuntimeState npc,
        NpcMovementRouteState routeState,
        Vector3 finalTargetPosition,
        int currentTick)
    {
        if (!ShouldUseNpcRouteLockedPrefix(npc) ||
            routeState == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1 ||
            !IsSameNpcMovementRouteContext(routeState, npc))
        {
            return false;
        }

        float totalRouteLength =
            GetNpcPathLength(routeState.Path);

        float arrivalThreshold =
            GetNpcRouteArrivalDistanceThreshold(
                npc,
                finalTargetPosition);

        if (routeState.DistanceTravelled >= totalRouteLength - arrivalThreshold)
            return false;

        routeState.Tick = currentTick;

        float distancePerTick =
            Mathf.Max(0f, npc.Speed);

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

        if (ShouldLogNpcRouteDecision())
        {
            LogNpcMovementDebug(
                "[NPC-ROUTE-REFRESH-FAILED-KEEP-OLD]" +
                " | Npc=" + npc.RuntimeNpcId +
                " | Type=" + npc.NpcType +
                " | DistanceTravelled=" + routeState.DistanceTravelled.ToString("0.###") +
                " | TotalRouteLength=" + totalRouteLength.ToString("0.###") +
                " | OldDestination=" + FormatVector3(routeState.Destination) +
                " | NewDestination=" + FormatVector3(finalTargetPosition));
        }

        return true;
    }

    private bool ShouldUseNpcRouteLockedPrefix(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return false;

        if (!ShouldKeepNpcRouteUntilArrival(npc))
            return false;

        if (npc.TravelState == SystemNpcTravelState.EngagingEnemy)
            return true;

        if (npc.TravelState == SystemNpcTravelState.TravelingInsideSystem &&
            npc.CurrentBehavior == SystemNpcBehaviorType.PlanetToPlanetTravel)
        {
            return true;
        }

        if (npc.TravelState == SystemNpcTravelState.Patrolling)
            return true;

        return false;
    }

    private int GetNpcRouteLockedPrefixSlots()
    {
        ShipMovementConfig movementConfig =
            _configService != null
                ? _configService.ShipMovementConfig
                : null;

        return movementConfig != null
            ? movementConfig.MovingDestinationRouteRefreshBlockedInitialSlots
            : 2;
    }

    private void BuildNpcRoutePrefix(
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
            Vector3 from = sourcePath[i - 1];
            Vector3 to = sourcePath[i];

            float segmentDistance =
                Vector3.Distance(from, to);

            if (segmentDistance <= 0.001f)
                continue;

            if (remainingDistance >= segmentDistance)
            {
                AddNpcRoutePointIfDifferent(
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

            AddNpcRoutePointIfDifferent(
                destinationPath,
                prefixEnd);

            return;
        }
    }

    private void AppendNpcRouteTail(
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
            AddNpcRoutePointIfDifferent(
                destinationPath,
                tailPath[i]);
        }
    }

    private void AddNpcRoutePointIfDifferent(
        List<Vector3> path,
        Vector3 point)
    {
        if (path == null)
            return;

        if (path.Count > 0 &&
            Vector3.Distance(
                path[path.Count - 1],
                point) <= 0.001f)
        {
            return;
        }

        path.Add(point);
    }

    private bool ShouldRejectNpcRawFallbackRoute(
        SystemNpcRuntimeState npc,
        Vector2 startFacingDirection,
        SystemShipRouteResult2A routeResult)
    {
        if (npc == null ||
            routeResult == null ||
            !routeResult.UsedRawSafeFallback ||
            routeResult.Path == null ||
            routeResult.Path.Count <= 1)
        {
            return false;
        }

        Vector2 routeStartDirection =
            _shipRouteService != null
                ? _shipRouteService.GetDirectionOnPathAtDistance(
                    routeResult.Path,
                    0f)
                : GetNpcDirectionOnPathAtDistance(
                    routeResult.Path,
                    0f);

        if (routeStartDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude ||
            startFacingDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
        {
            return false;
        }

        float startTurnAngle =
            Mathf.Abs(
                Vector2.SignedAngle(
                    startFacingDirection.normalized,
                    routeStartDirection.normalized));

        float maxAllowedStartTurnAngle =
            GetNpcRawFallbackMaxStartTurnAngleDegrees(npc);

        bool shouldReject =
            startTurnAngle > maxAllowedStartTurnAngle;

        if (shouldReject &&
            ShouldLogNpcRouteDecision())
        {
            LogNpcMovementDebug(
                "[NPC-RAW-FALLBACK-REJECTED]" +
                " | Npc=" + npc.RuntimeNpcId +
                " | Type=" + npc.NpcType +
                " | StartTurnAngle=" + startTurnAngle.ToString("0.###") +
                " | MaxAllowed=" + maxAllowedStartTurnAngle.ToString("0.###") +
                " | StartFacing=" + FormatVector2(startFacingDirection) +
                " | RouteDirection=" + FormatVector2(routeStartDirection) +
                " | PathLength=" + routeResult.PathLength.ToString("0.###") +
                " | Destination=" + FormatVector3(routeResult.Path[routeResult.Path.Count - 1]));
        }

        return shouldReject;
    }

    private float GetNpcRawFallbackMaxStartTurnAngleDegrees(SystemNpcRuntimeState npc)
    {
        return Mathf.Clamp(
            GetMovementTurnSpikeAngleDegrees(),
            1f,
            179f);
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

        System.Action<string> routeDebugLog =
            ShouldLogShipRouteInternals()
                ? Bootstrapper.Instance.CreateDebugLogAction(DebugLogChannel.ShipRoute)
                : null;

        float minTurnRadiusAbsolute =
            movementConfig != null
                ? movementConfig.MinRouteTurnRadiusAbsolute
                : 30f;

        float turnRadius =
            Mathf.Max(0f, npc != null ? npc.TurnRadius : 0f);

        if (minTurnRadiusAbsolute > 0f)
        {
            turnRadius =
                Mathf.Max(
                    turnRadius,
                    minTurnRadiusAbsolute);
        }

        return new SystemShipRouteSettings2A
        {
            Speed = Mathf.Max(0.01f, npc != null ? npc.Speed : 0f),
            TurnRadius = turnRadius,
            ArrivalDistanceThreshold = arrivalThreshold,
            SunAvoidanceSafetyMargin = SunAvoidanceSafetyMargin,
            SunAvoidanceArcSegments = SunAvoidanceArcSegments,
            AllowSunAvoidance = true,
            RouteSubstepsPerTick = movementConfig != null ? movementConfig.RouteSubstepsPerTick : 10,
            RouteStraightExitAngleDegrees = movementConfig != null ? movementConfig.RouteStraightExitAngleDegrees : 3f,
            TurnRadiusAdjustmentStepPercent = movementConfig != null ? movementConfig.RouteTurnRadiusAdjustmentStepPercent : 5f,
            SpeedAdjustmentStepPercent = movementConfig != null ? movementConfig.RouteSpeedAdjustmentStepPercent : 2.5f,
            MinTurnRadiusAdjustmentFactor = movementConfig != null ? movementConfig.MinRouteTurnRadiusAdjustmentFactor : 0.05f,
            MinTurnRadiusAbsolute = minTurnRadiusAbsolute,
            BehindSmallTurnAngleToleranceDegrees = movementConfig != null ? movementConfig.RouteBehindSmallTurnAngleToleranceDegrees : 75f,
            MaxRoutePlanSteps = NpcRoutePlanMaxSteps,
            SunAvoidanceTurnRouteReserveMultiplier = 1.5f,
            DebugLog = routeDebugLog,
            DebugPrefix = "[NpcMovement] "
        };
    }

    private bool ShouldLogShipRouteInternals()
    {
        DebugLogConfig debugLogConfig =
            Bootstrapper.Instance != null
                ? Bootstrapper.Instance.DebugLogConfig
                : null;

        return debugLogConfig != null &&
               debugLogConfig.IncludeShipRouteInternalLogs &&
               debugLogConfig.IsEnabled(DebugLogChannel.ShipRoute);
    }

    private bool ShouldLogNpcRouteDecision()
    {
        DebugLogConfig debugLogConfig =
            Bootstrapper.Instance != null
                ? Bootstrapper.Instance.DebugLogConfig
                : null;

        return debugLogConfig != null &&
               debugLogConfig.NpcRouteDecisionLogs &&
               debugLogConfig.IsEnabled(DebugLogChannel.NpcMovement);
    }

    private bool IsNpcMovementDebugEnabled()
    {
        return Bootstrapper.Instance != null &&
               Bootstrapper.Instance.IsDebugLogEnabled(DebugLogChannel.NpcMovement);
    }

    private float GetMovementTurnSpikeAngleDegrees()
    {
        DebugLogConfig debugLogConfig =
            Bootstrapper.Instance != null
                ? Bootstrapper.Instance.DebugLogConfig
                : null;

        return debugLogConfig != null
            ? debugLogConfig.MovementTurnSpikeAngleDegrees
            : 120f;
    }

    private void LogNpcMovementDebug(string message)
    {
        if (Bootstrapper.Instance == null)
            return;

        Bootstrapper.Instance.LogDebug(
            DebugLogChannel.NpcMovement,
            "[SystemNpcMovementService] " + message);
    }

    private float GetSignedAngle(Vector3 from, Vector3 to)
    {
        Vector2 from2 =
            new Vector2(from.x, from.y);

        Vector2 to2 =
            new Vector2(to.x, to.y);

        if (from2.sqrMagnitude <= DirectionThresholdSqrMagnitude ||
            to2.sqrMagnitude <= DirectionThresholdSqrMagnitude)
        {
            return 0f;
        }

        return Vector2.SignedAngle(from2.normalized, to2.normalized);
    }

    private string FormatVector3(Vector3 value)
    {
        return "(" +
               value.x.ToString("0.###") + ", " +
               value.y.ToString("0.###") + ", " +
               value.z.ToString("0.###") + ")";
    }

    private string FormatVector2(Vector2 value)
    {
        return "(" +
               value.x.ToString("0.###") + ", " +
               value.y.ToString("0.###") + ")";
    }

    private float GetNpcRouteArrivalDistanceThreshold(
        SystemNpcRuntimeState npc,
        Vector3 destinationPosition)
    {
        if (npc == null)
            return ArrivalDistanceThreshold;

        if (npc.TravelState == SystemNpcTravelState.EngagingEnemy)
        {
            return Mathf.Max(
                ArrivalDistanceThreshold,
                GetNpcCombatRouteArrivalDistanceThreshold(npc));
        }

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

    private float GetNpcCombatRouteArrivalDistanceThreshold(
        SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return ArrivalDistanceThreshold;

        float turnRadiusPart =
            Mathf.Max(0f, npc.TurnRadius) * 0.05f;

        return Mathf.Clamp(
            turnRadiusPart,
            ArrivalDistanceThreshold,
            25f);
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

    private Vector2 GetNpcSafeFacingDirection(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return Vector2.up;

        Vector3 facing = npc.FacingDirection;
        facing.z = 0f;

        if (facing.sqrMagnitude > DirectionThresholdSqrMagnitude)
            return new Vector2(facing.x, facing.y).normalized;

        Vector3 tickDirection = npc.TickMovementDirection;
        tickDirection.z = 0f;

        if (tickDirection.sqrMagnitude > DirectionThresholdSqrMagnitude)
            return new Vector2(tickDirection.x, tickDirection.y).normalized;

        Vector3 targetDirection = npc.TargetPosition - npc.CurrentPosition;
        targetDirection.z = 0f;

        if (targetDirection.sqrMagnitude > DirectionThresholdSqrMagnitude)
            return new Vector2(targetDirection.x, targetDirection.y).normalized;

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
        public readonly TravelRoutePlan RoutePlan = new();

        public Vector3 Destination;
        public float DistanceTravelled;
        public int Tick = -1;
        public int BuildTick = -1;
        public SystemNpcBehaviorType BehaviorType;
        public SystemNpcTravelState TravelState;
        public string TargetSystemId;
        public string TargetPlanetId;
        public string CurrentTargetRuntimeNpcId;
        public bool IsBoundaryEscapeRoute;

        public bool StartTurnInPlacePending;
        public Vector2 StartTurnInPlaceDirection = Vector2.up;
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
        _npcBoundaryNavigationStates.Remove(runtimeNpcId);
        ClearSunAvoidanceRoute(runtimeNpcId);
    }

    private SystemBoundaryNavigation2A.BoundaryNavigationState GetOrCreateNpcBoundaryNavigationState(
    string runtimeNpcId)
    {
        if (string.IsNullOrWhiteSpace(runtimeNpcId))
            return null;

        if (!_npcBoundaryNavigationStates.TryGetValue(
                runtimeNpcId,
                out SystemBoundaryNavigation2A.BoundaryNavigationState state) ||
            state == null)
        {
            state =
                new SystemBoundaryNavigation2A.BoundaryNavigationState();

            _npcBoundaryNavigationStates[runtimeNpcId] = state;
        }

        return state;
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

    private static float GetTickScaledDeltaTime(float deltaTime)
    {
        return deltaTime /
               Mathf.Max(0.01f, GameTimeState.SecondsPerDay);
    }

    private void RebuildNpcMovementRoutePlan(
    NpcMovementRouteState routeState,
    SystemNpcRuntimeState npc)
    {
        if (routeState == null)
            return;

        routeState.RoutePlan.Clear();
        routeState.StartTurnInPlacePending = false;
        routeState.StartTurnInPlaceDirection = Vector2.up;

        if (npc == null ||
            routeState.Path == null ||
            routeState.Path.Count <= 1)
        {
            return;
        }

        Vector2 startFacingDirection =
            GetNpcSafeFacingDirection(npc);

        Vector2 firstRouteDirection =
            _shipRouteService != null
                ? _shipRouteService.GetDirectionOnPathAtDistance(
                    routeState.Path,
                    0f)
                : GetNpcDirectionOnPathAtDistance(
                    routeState.Path,
                    0f);

        if (firstRouteDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
        {
            routeState.RoutePlan.SetMovePath(routeState.Path);
            return;
        }

        firstRouteDirection.Normalize();

        float startTurnAngle =
            Vector2.SignedAngle(
                startFacingDirection,
                firstRouteDirection);

        if (ShouldUseNpcStartTurnInPlace(
                Mathf.Abs(startTurnAngle),
                npc,
                routeState))
        {
            routeState.RoutePlan.AddTurnInPlace(
                npc.CurrentPosition,
                startFacingDirection,
                firstRouteDirection);

            routeState.StartTurnInPlacePending = true;
            routeState.StartTurnInPlaceDirection = firstRouteDirection;

            if (IsNpcMovementDebugEnabled())
            {
                LogNpcMovementDebug(
                    "[NPC-TURN-IN-PLACE-PLAN]" +
                    " | Npc=" + npc.RuntimeNpcId +
                    " | Type=" + npc.NpcType +
                    " | TurnAngle=" + startTurnAngle.ToString("0.###") +
                    " | Position=" + FormatVector3(npc.CurrentPosition) +
                    " | From=" + FormatVector2(startFacingDirection) +
                    " | To=" + FormatVector2(firstRouteDirection) +
                    " | Destination=" + FormatVector3(routeState.Destination) +
                    " | PathCount=" + routeState.Path.Count);
            }
        }

        routeState.RoutePlan.SetMovePath(routeState.Path);
    }

    private bool ShouldUseNpcStartTurnInPlace(
     float turnAngleDegrees,
     SystemNpcRuntimeState npc,
     NpcMovementRouteState routeState)
    {
        return false;
    }

    private bool ConsumeNpcStartTurnInPlaceIfNeeded(
        SystemNpcRuntimeState npc,
        NpcMovementRouteState routeState,
        int currentTick)
    {
        if (npc == null ||
            routeState == null ||
            !routeState.StartTurnInPlacePending)
        {
            return false;
        }

        Vector2 turnDirection =
            routeState.StartTurnInPlaceDirection;

        if (routeState.RoutePlan.HasTurnInPlaceAtStart &&
            routeState.RoutePlan.Steps.Count > 0 &&
            routeState.RoutePlan.Steps[0].ToDirection.sqrMagnitude >
            DirectionThresholdSqrMagnitude)
        {
            turnDirection =
                routeState.RoutePlan.Steps[0].ToDirection;
        }

        if (turnDirection.sqrMagnitude <= DirectionThresholdSqrMagnitude)
        {
            routeState.StartTurnInPlacePending = false;
            return false;
        }

        turnDirection.Normalize();

        Vector3 oldFacingDirection =
            npc.FacingDirection;

        Vector3 newFacingDirection =
            new Vector3(
                turnDirection.x,
                turnDirection.y,
                0f);

        npc.FacingDirection = newFacingDirection;
        npc.TickMovementDirection = newFacingDirection;

        npc.CurrentMovementTargetPosition = npc.CurrentPosition;
        npc.TickMovementTargetPosition = npc.CurrentPosition;
        npc.TickMovementDirectionTick = currentTick;
        npc.TickMovementArrived = false;

        routeState.StartTurnInPlacePending = false;
        routeState.Tick = currentTick;

        if (IsNpcMovementDebugEnabled())
        {
            LogNpcMovementDebug(
                "[NPC-TURN-IN-PLACE]" +
                " | Npc=" + npc.RuntimeNpcId +
                " | Type=" + npc.NpcType +
                " | TurnAngle=" + GetSignedAngle(
                    oldFacingDirection,
                    newFacingDirection).ToString("0.###") +
                " | Position=" + FormatVector3(npc.CurrentPosition) +
                " | OldFacing=" + FormatVector3(oldFacingDirection) +
                " | NewFacing=" + FormatVector3(newFacingDirection) +
                " | Destination=" + FormatVector3(routeState.Destination));
        }

        _eventBus.Publish(new SystemNpcPositionChangedEvent(
            npc.RuntimeNpcId,
            npc.CurrentSystemId,
            npc.CurrentPosition));

        return true;
    }
}
