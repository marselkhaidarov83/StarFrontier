using System;
using UnityEngine;

public sealed class SystemNpcMovementService : CustomService, ISystemNpcMovementService
{
    private const float ArrivalDistanceThreshold = 3f;

    private readonly ISystemNpcRuntimeService _runtimeService;
    private readonly ISystemNpcBehaviorService _behaviorService;
    private readonly ISystemNpcMovementRouteService _routeService;
    private readonly SimpleEventBus _eventBus;

    public SystemNpcMovementService()
    {
        _debugStop = true;
        _runtimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _behaviorService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcBehaviorService>();
        _routeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcMovementRouteService>();
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
        if (npc.TargetSystemLink != null && npc.TargetSystemLink.LinkedSystem != null)
            linkedSystemId = npc.TargetSystemLink?.LinkedSystem?.Id;
        LogCustom("npc.TargetSystemLink = " + linkedSystemId +
                " npc.TargetPlanetId = " + npc.TargetPlanetId +
                " npc.TargetPosition = " + npc.TargetPosition +
                " npc.CurrentTargetRuntimeNpcId = " + npc.CurrentTargetRuntimeNpcId);
        npc.TargetPosition = _routeService.GetNextTargetPosition(npc);
        // LogCustom("npc.TargetSystemLink = " + linkedSystemId +
        //         " npc.TargetPlanetId = " + npc.TargetPlanetId +
        //         " npc.TargetPosition = " + npc.TargetPosition);

        SystemTravelMathResult result = SystemTravelMath.MoveTowards(
            npc.CurrentPosition,
            npc.StartPosition,
            npc.TargetPosition,
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

        if (result.Arrived)
            CompleteMovement(npc, currentTick);
    }

    private void CompleteMovement(SystemNpcRuntimeState npc, int currentTick)
    {
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
        if (npc.TargetSystemLink != null)
        {
            npc.CurrentSystemId = npc.TargetSystemLink.LinkedSystem.Id;
            npc.CurrentPosition = npc.TargetSystemLink.EntryPoint;
            npc.TargetSystemLink = null;
        }

        npc.IsOnPlanet = false;
        npc.TravelState = SystemNpcTravelState.Idle;
    }
}