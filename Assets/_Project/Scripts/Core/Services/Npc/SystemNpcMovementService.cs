using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SystemNpcMovementService : CustomService, ISystemNpcMovementService
{
    private const float ArrivalDistanceThreshold = 3f;
    private const float SunAvoidanceSafetyMargin = 80f;
    private const int SunAvoidanceArcSegments = 18;
    private const float SunAvoidanceDestinationRefreshThreshold = 40f;

    private readonly ISystemNpcRuntimeService _runtimeService;
    private readonly ISystemNpcBehaviorService _behaviorService;
    private readonly ISystemNpcMovementRouteService _routeService;
    private readonly IConfigService _configService;
    private readonly SimpleEventBus _eventBus;
    private readonly List<Vector3> _sunAvoidancePath = new();
    private readonly Dictionary<string, SunAvoidanceRouteState> _sunAvoidanceRoutes = new();

    public SystemNpcMovementService()
    {
        _debugStop = true;
        _runtimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _behaviorService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcBehaviorService>();
        _routeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcMovementRouteService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
    }

    public void Tick(StarSystemConfig starSystem, float deltaTime, int currentTick)
    {
        if (string.IsNullOrWhiteSpace(starSystem.Id))
            return;

        if (deltaTime <= 0f)
            return;

        var npcs = _runtimeService.GetAliveNpcsInSystem(starSystem.Id);

        // LogCustom("npcs.Count = " + npcs.Count);
        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];

            if (!CanMove(npc))
            {
                LogCustom("CanMove = false, npc.RuntimeNpcId = " + npc.RuntimeNpcId);
                continue;
            }

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
        LogCustom("");
        if (npc.TargetPosition == Vector3.zero)
        {
            npc.StartPosition = npc.CurrentPosition;
            npc.TravelProgress01 = 0f;
        }

        String linkedSystemId = "";
        if (npc.TargetSystemId != null)
            linkedSystemId = npc.TargetSystemId;
        LogCustom("npc.TargetSystemLink = " + linkedSystemId +
                " npc.TargetPlanetId = " + npc.TargetPlanetId +
                " npc.TargetPosition = " + npc.TargetPosition +
                " npc.CurrentTargetRuntimeNpcId = " + npc.CurrentTargetRuntimeNpcId);
        Vector3 finalTargetPosition = _routeService.GetNextTargetPosition(npc);
        npc.TargetPosition = finalTargetPosition;
        Vector3 movementTargetPosition = GetSunSafeNextTargetPosition(
            npc,
            finalTargetPosition);
        npc.CurrentMovementTargetPosition = movementTargetPosition;
        // LogCustom("npc.TargetSystemLink = " + linkedSystemId +
        //         " npc.TargetPlanetId = " + npc.TargetPlanetId +
        //         " npc.TargetPosition = " + npc.TargetPosition);

        SystemTravelMathResult result = SystemTravelMath.MoveTowards(
            npc.CurrentPosition,
            npc.StartPosition,
            movementTargetPosition,
            npc.Speed,
            deltaTime,
            ArrivalDistanceThreshold
        );

        npc.CurrentPosition = result.NewPosition;
        npc.TravelProgress01 = result.Progress01;

        _eventBus.Publish(new SystemNpcPositionChangedEvent(
            npc.RuntimeNpcId,
            npc.CurrentSystemId,
            npc.CurrentPosition
        ));

        if (result.Arrived &&
            Vector3.Distance(npc.CurrentPosition, finalTargetPosition) >
            ArrivalDistanceThreshold)
        {
            AdvanceSunAvoidanceRoute(npc);
            npc.StartPosition = npc.CurrentPosition;
            npc.TravelProgress01 = 0f;
            npc.CurrentMovementTargetPosition = GetSunSafeNextTargetPosition(
                npc,
                finalTargetPosition);
            return;
        }

        if (result.Arrived)
            CompleteMovement(npc, currentTick);
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

        float sunRadius = Mathf.Max(0f, sun.VisualSize * 0.5f);
        float avoidanceRadius = sunRadius + SunAvoidanceSafetyMargin;

        SunAvoidanceRouteState routeState = GetOrCreateSunAvoidanceRoute(
            npc,
            finalTargetPosition,
            sunCenter,
            avoidanceRadius);

        if (routeState == null)
            return finalTargetPosition;

        while (routeState.WaypointIndex < routeState.Waypoints.Count - 1 &&
               Vector3.Distance(npc.CurrentPosition, routeState.Waypoints[routeState.WaypointIndex]) <=
               ArrivalDistanceThreshold)
        {
            routeState.WaypointIndex++;
        }

        if (routeState.WaypointIndex >= routeState.Waypoints.Count - 1)
            return finalTargetPosition;

        return routeState.Waypoints[routeState.WaypointIndex];
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
        ClearSunAvoidanceRoute(npc.RuntimeNpcId);

        npc.CurrentPosition = npc.TargetPosition;
        npc.TravelProgress01 = 1f;

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
        npc.TargetPosition = Vector3.zero;
        npc.CurrentMovementTargetPosition = Vector3.zero;

        _eventBus.Publish(new SystemNpcTravelStateChangedEvent(
            npc.RuntimeNpcId,
            npc,
            npc.TravelState,
            npc.CurrentSystemId
        ));

        _behaviorService.CompleteBehavior(npc, currentTick);

        LogCustom($"Movement complete. " +
            $"NPC: {npc.RuntimeNpcId}, State: {npc.TravelState}, Behavior: {npc.CurrentBehavior}"
        );
    }

    private void CompleteSystemTravel(SystemNpcRuntimeState npc)
    {
        // if (!string.IsNullOrWhiteSpace(npc.TargetSystemId))
        // {
        //     npc.CurrentSystemId = npc.TargetSystemId;
        //     npc.TargetSystemId = null;
        // }
        LogCustom("started");
        if (!string.IsNullOrWhiteSpace(npc.TargetSystemId))
        {
            npc.CurrentSystemId = npc.TargetSystemId;
            npc.CurrentPosition = npc.TargetSystemEntryPoint;

            npc.TargetSystemId = null;
            npc.TargetSystemExitPoint = Vector3.zero;
            npc.TargetSystemEntryPoint = Vector3.zero;
        }

        npc.IsOnPlanet = false;
        npc.TravelState = SystemNpcTravelState.Idle;
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
}
