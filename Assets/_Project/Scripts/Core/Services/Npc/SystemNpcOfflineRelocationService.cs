using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SystemNpcOfflineRelocationService :
    CustomService,
    ISystemNpcOfflineRelocationService
{
    private const double MaxOfflineHours = 12d;
    private const float MinMapSpawnRadiusFromSun = 120f;

    private readonly IConfigService _configService;
    private readonly ISystemNpcRuntimeService _npcRuntimeService;
    private readonly IRouteService _routeService;
    private readonly SimpleEventBus _eventBus;

    public SystemNpcOfflineRelocationService()
    {
        _debugStop = true;

        _configService =
            Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();

        _npcRuntimeService =
            Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();

        _eventBus =
            Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        _routeService =
            Bootstrapper.Instance.ServiceRegistry.Get<IRouteService>();
    }

    public bool TryProcessOffline(GameRuntimeState state)
    {
        if (state == null || state.Meta == null)
            return false;

        double offlineHours =
            CalculateOfflineHours(state.Meta);

        if (offlineHours <= 0d)
            return false;

        IReadOnlyList<SystemNpcRuntimeState> npcs =
            _npcRuntimeService.Npcs;

        if (npcs == null || npcs.Count == 0)
            return false;

        int movedCount = 0;

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];

            if (!CanNpcTryOfflineRelocation(npc))
                continue;

            if (!TryPickDestinationSystem(
                    npc,
                    state,
                    out StarSystemConfig destinationSystem))
                continue;

            ApplyOfflineRelocation(
                npc,
                destinationSystem);

            movedCount++;
        }

        state.Meta.LastSaveUtc =
            DateTime.UtcNow.Ticks;

        if (movedCount > 0)
        {
            _eventBus.Publish(
                new SaveNeedEvent("npc_offline_relocation"));
        }

        LogCustom(
            "[SystemNpcOfflineRelocationService] Offline relocation finished. " +
            "Hours=" + offlineHours.ToString("0.00") +
            ", moved=" + movedCount);

        return movedCount > 0;
    }

    private double CalculateOfflineHours(GameRuntimeMetaState meta)
    {
        if (meta == null || meta.LastSaveUtc <= 0)
            return 0d;

        long nowTicks =
            DateTime.UtcNow.Ticks;

        if (nowTicks <= meta.LastSaveUtc)
            return 0d;

        double offlineHours =
            new TimeSpan(nowTicks - meta.LastSaveUtc).TotalHours;

        if (offlineHours <= 0d)
            return 0d;

        return Math.Min(
            MaxOfflineHours,
            offlineHours);
    }

    private bool CanNpcTryOfflineRelocation(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return false;

        if (!npc.IsAlive)
            return false;

        if (!npc.IsAlly)
            return false;

        if (string.IsNullOrWhiteSpace(npc.CurrentSystemId))
            return false;

        return AllyHasTravelToAnotherSystemScenario(npc.ConfigId);
    }

    private bool AllyHasTravelToAnotherSystemScenario(string allyConfigId)
    {
        if (string.IsNullOrWhiteSpace(allyConfigId))
            return false;

        AllyConfig allyConfig =
            _configService.GetAllyConfigById(allyConfigId);

        if (allyConfig == null ||
            allyConfig.BehaviorScenarios == null)
            return false;

        for (int i = 0; i < allyConfig.BehaviorScenarios.Count; i++)
        {
            AllyBehaviourScenarioEntry entry =
                allyConfig.BehaviorScenarios[i];

            if (entry == null ||
                entry.BehaviorConfig == null ||
                entry.BehaviorConfig.BehaviorWeights == null)
                continue;

            IReadOnlyList<SystemNpcBehaviorWeight> weights =
                entry.BehaviorConfig.BehaviorWeights;

            for (int j = 0; j < weights.Count; j++)
            {
                SystemNpcBehaviorWeight weight =
                    weights[j];

                if (weight == null)
                    continue;

                if (weight.BehaviorType ==
                    SystemNpcBehaviorType.TravelToAnotherSystem)
                    return true;
            }
        }

        return false;
    }

    private bool TryPickDestinationSystem(
        SystemNpcRuntimeState npc,
        GameRuntimeState state,
        out StarSystemConfig destinationSystem)
    {
        destinationSystem = null;

        IReadOnlyList<StarSystemConfig> allSystems =
            _configService.GetAllStarSystems();

        if (allSystems == null || allSystems.Count == 0)
            return false;

        List<StarSystemConfig> candidates =
            new List<StarSystemConfig>();

        for (int i = 0; i < allSystems.Count; i++)
        {
            StarSystemConfig system =
                allSystems[i];

            if (system == null)
                continue;

            if (system.Id == npc.CurrentSystemId)
                continue;

            if (!IsSystemOpen(system, state))
                continue;

            if (HasAliveHostiles(system.Id))
                continue;

            candidates.Add(system);
        }

        if (candidates.Count == 0)
            return false;

        int index =
            UnityEngine.Random.Range(0, candidates.Count);

        destinationSystem =
            candidates[index];

        return destinationSystem != null;
    }

    private bool IsSystemOpen(
        StarSystemConfig system,
        GameRuntimeState state)
    {
        if (system == null)
            return false;

        if (system.IsStartSystem)
            return true;

        if (!IsSystemInUnlockedSector(system.Id, state))
            return false;

        if (state == null ||
            state.Galaxy == null ||
            state.Galaxy.Systems == null)
            return false;

        for (int i = 0; i < state.Galaxy.Systems.Count; i++)
        {
            StarSystemRuntimeState runtimeSystem =
                state.Galaxy.Systems[i];

            if (runtimeSystem == null)
                continue;

            if (runtimeSystem.SystemId != system.Id)
                continue;

            return runtimeSystem.IsDiscovered;
        }

        return false;
    }

    private bool IsSystemInUnlockedSector(
        string systemId,
        GameRuntimeState state)
    {
        IReadOnlyList<SectorConfig> sectors =
            _configService.GetAllSectors();

        if (sectors == null)
            return false;

        for (int i = 0; i < sectors.Count; i++)
        {
            SectorConfig sector =
                sectors[i];

            if (sector == null || sector.Systems == null)
                continue;

            bool containsSystem = false;

            for (int j = 0; j < sector.Systems.Length; j++)
            {
                StarSystemConfig sectorSystem =
                    sector.Systems[j];

                if (sectorSystem == null)
                    continue;

                if (sectorSystem.Id == systemId)
                {
                    containsSystem = true;
                    break;
                }
            }

            if (!containsSystem)
                continue;

            return IsSectorUnlocked(
                sector,
                state);
        }

        return false;
    }

    private bool IsSectorUnlocked(
        SectorConfig sector,
        GameRuntimeState state)
    {
        if (sector == null)
            return false;

        if (state == null ||
            state.Galaxy == null ||
            state.Galaxy.Sectors == null)
            return sector.IsUnlocked;

        for (int i = 0; i < state.Galaxy.Sectors.Count; i++)
        {
            SectorRuntimeState runtimeSector =
                state.Galaxy.Sectors[i];

            if (runtimeSector == null)
                continue;

            if (runtimeSector.SectorId == sector.Id)
                return runtimeSector.IsUnlocked;
        }

        return sector.IsUnlocked;
    }

    private bool HasAliveHostiles(string systemId)
    {
        IReadOnlyList<SystemNpcRuntimeState> npcs =
            _npcRuntimeService.Npcs;

        if (npcs == null)
            return false;

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc =
                npcs[i];

            if (npc == null)
                continue;

            if (!npc.IsAlive)
                continue;

            if (npc.CurrentSystemId != systemId)
                continue;

            if (npc.IsHostileToPlayer)
                return true;
        }

        return false;
    }

    private void ApplyOfflineRelocation(
        SystemNpcRuntimeState npc,
        StarSystemConfig destinationSystem)
    {
        npc.CurrentSystemId =
            destinationSystem.Id;

        npc.TargetSystemId =
            null;

        npc.TargetSystemExitPoint =
            Vector3.zero;

        npc.TargetSystemEntryPoint =
            Vector3.zero;

        npc.TargetPlanetId =
            null;

        npc.CurrentTargetRuntimeNpcId =
            null;

        npc.BehaviorTargetRuntimeNpcId =
            null;

        npc.IsFighting =
            false;

        npc.CombatState =
            SystemNpcCombatState.None;

        npc.TravelProgress01 =
            0f;

        npc.TravelStartTick =
            0;

        npc.TravelEndTick =
            0;

        npc.PrevBehavior =
            SystemNpcBehaviorType.TravelToAnotherSystem;

        npc.CurrentBehavior =
            SystemNpcBehaviorType.None;

        npc.HasActiveBehavior =
            false;

        npc.BehaviorStartedTick =
            0;

        npc.BehaviorEndsTick =
            0;

        if (TryPlaceNpcOnRandomPlanet(
                npc,
                destinationSystem))
        {
            PublishNpcLocationChanged(npc);
            return;
        }

        PlaceNpcOnRandomMapPoint(
            npc);

        PublishNpcLocationChanged(npc);
    }

    private bool TryPlaceNpcOnRandomPlanet(
        SystemNpcRuntimeState npc,
        StarSystemConfig system)
    {
        PlanetConfig[] planets =
            system.PlanetInhabited();

        if (planets == null || planets.Length == 0)
            return false;

        if (UnityEngine.Random.value >= 0.5f)
            return false;

        PlanetConfig planet =
            planets[UnityEngine.Random.Range(0, planets.Length)];

        if (planet == null)
            return false;

        Vector3 position =
            GetPlanetPosition(planet);

        npc.CurrentPlanetId =
            planet.Id;

        npc.IsOnPlanet =
            true;

        npc.TravelState =
            SystemNpcTravelState.OnPlanet;

        SetNpcPositionFields(
            npc,
            position);

        return true;
    }

    private Vector3 GetPlanetPosition(PlanetConfig planet)
    {
        if (planet == null || planet.PlanetOrbit == null)
            return Vector3.zero;

        float angleRad =
            planet.PlanetOrbit.StartAngleDeg * Mathf.Deg2Rad;

        Vector3 offset =
            new Vector3(
                Mathf.Cos(angleRad),
                Mathf.Sin(angleRad),
                0f) * planet.PlanetOrbit.OrbitRadius;

        return planet.PlanetOrbit.OrbitCenterOffset + offset;
    }

    private void PlaceNpcOnRandomMapPoint(SystemNpcRuntimeState npc)
    {
        Vector3 position =
            PickRandomMapPosition();

        npc.CurrentPlanetId =
            null;

        npc.IsOnPlanet =
            false;

        npc.TravelState =
            SystemNpcTravelState.Idle;

        SetNpcPositionFields(
            npc,
            position);
    }

    private Vector3 PickRandomMapPosition()
    {
        Vector2 halfSize =
            Vector2.zero;

        if (_configService.ShipMovementConfig != null)
            halfSize = _configService.ShipMovementConfig.SystemBoundsHalfSize;

        if (halfSize.x <= 0f)
            halfSize.x = 600f;

        if (halfSize.y <= 0f)
            halfSize.y = 600f;

        for (int i = 0; i < 20; i++)
        {
            Vector2 candidate =
                new Vector2(
                    UnityEngine.Random.Range(-halfSize.x, halfSize.x),
                    UnityEngine.Random.Range(-halfSize.y, halfSize.y));

            if (candidate.magnitude >= MinMapSpawnRadiusFromSun)
                return new Vector3(candidate.x, candidate.y, 0f);
        }

        return new Vector3(
            0f,
            -MinMapSpawnRadiusFromSun,
            0f);
    }

    private void SetNpcPositionFields(
        SystemNpcRuntimeState npc,
        Vector3 position)
    {
        npc.CurrentPosition =
            position;

        npc.StartPosition =
            position;

        npc.TargetPosition =
            position;

        npc.CurrentMovementTargetPosition =
            position;

        npc.TickMovementTargetPosition =
            position;

        Vector3 directionToSun =
            Vector3.zero - position;

        if (directionToSun.sqrMagnitude <= 0.0001f)
            directionToSun = Vector3.up;

        directionToSun.Normalize();

        npc.FacingDirection =
            directionToSun;

        npc.TickMovementDirection =
            directionToSun;

        npc.TickMovementArrived =
            true;

        npc.TickMovementDirectionTick =
            -1;
    }

    private void PublishNpcLocationChanged(SystemNpcRuntimeState npc)
    {
        _eventBus.Publish(
            new SystemNpcPositionChangedEvent(
                npc.RuntimeNpcId,
                npc.CurrentSystemId,
                npc.CurrentPosition));

        _eventBus.Publish(
            new SystemNpcTravelStateChangedEvent(
                npc.RuntimeNpcId,
                npc,
                npc.TravelState,
                npc.TargetSystemId));
    }

    public bool DebugProcessOfflineStep(
    GameRuntimeState state,
    float offlineHours)
    {
        if (state == null)
            return false;

        offlineHours =
            Mathf.Max(0.1f, offlineHours);

        bool moved =
            ProcessOfflineRelocation(
                state,
                offlineHours,
                "npc_debug_offline_step");

        LogCustom(
            "[SystemNpcOfflineRelocationService] Debug offline step finished. " +
            "Hours=" + offlineHours.ToString("0.00") +
            ", moved=" + moved);

        return moved;
    }

    public bool DebugForceTargetNpcRoute(
        string runtimeNpcId,
        GameRuntimeState state)
    {
        if (string.IsNullOrWhiteSpace(runtimeNpcId))
            return false;

        if (!_npcRuntimeService.TryGetNpc(
                runtimeNpcId,
                out SystemNpcRuntimeState npc) ||
            npc == null ||
            !npc.IsAlive)
            return false;

        StarSystemConfig currentSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        RouteConfig route =
            PickDebugUnlockedRoute(currentSystem);

        if (route == null)
            return false;

        StarSystemConfig targetSystem =
            route.GetOtherSystem(currentSystem.Id);

        if (targetSystem == null)
            return false;

        int currentTick =
            GetCurrentQuantTick();

        npc.TravelState =
            SystemNpcTravelState.TravelingToAnotherSystem;

        npc.IsOnPlanet =
            false;

        npc.CurrentPlanetId =
            null;

        npc.TargetPlanetId =
            null;

        npc.TargetSystemId =
            targetSystem.Id;

        npc.TargetSystemExitPoint =
            route.GetExitPoint(currentSystem.Id);

        npc.TargetSystemEntryPoint =
            route.GetEntryPoint(targetSystem.Id);

        npc.StartPosition =
            npc.CurrentPosition;

        npc.TargetPosition =
            Vector3.zero;

        npc.CurrentMovementTargetPosition =
            npc.TargetSystemExitPoint;

        npc.TickMovementTargetPosition =
            npc.TargetSystemExitPoint;

        npc.TickMovementArrived =
            false;

        npc.TickMovementDirectionTick =
            -1;

        npc.CurrentTargetRuntimeNpcId =
            null;

        npc.BehaviorTargetRuntimeNpcId =
            null;

        npc.IsFighting =
            false;

        npc.CombatState =
            SystemNpcCombatState.None;

        npc.PrevBehavior =
            npc.CurrentBehavior;

        npc.CurrentBehavior =
            SystemNpcBehaviorType.TravelToAnotherSystem;

        npc.HasActiveBehavior =
            true;

        npc.BehaviorStartedTick =
            currentTick;

        npc.BehaviorEndsTick =
            currentTick + 1;

        npc.TravelStartTick =
            currentTick;

        npc.TravelEndTick =
            currentTick + 1;

        npc.TravelProgress01 =
            0f;

        _eventBus.Publish(
            new SystemNpcTravelStateChangedEvent(
                npc.RuntimeNpcId,
                npc,
                npc.TravelState,
                npc.TargetSystemId));

        _eventBus.Publish(
            new SaveNeedEvent("npc_debug_force_route"));

        LogCustom(
            "[SystemNpcOfflineRelocationService] Debug forced route. " +
            "Npc=" + npc.RuntimeNpcId +
            ", From=" + currentSystem.Id +
            ", To=" + targetSystem.Id);

        return true;
    }

    private bool ProcessOfflineRelocation(
    GameRuntimeState state,
    double offlineHours,
    string saveReason)
    {
        if (offlineHours <= 0d)
            return false;

        IReadOnlyList<SystemNpcRuntimeState> npcs =
            _npcRuntimeService.Npcs;

        if (npcs == null || npcs.Count == 0)
            return false;

        int movedCount = 0;

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];

            if (!CanNpcTryOfflineRelocation(npc))
                continue;

            if (!TryPickDestinationSystem(
                    npc,
                    state,
                    out StarSystemConfig destinationSystem))
                continue;

            ApplyOfflineRelocation(
                npc,
                destinationSystem);

            movedCount++;
        }

        if (state.Meta != null)
        {
            state.Meta.LastSaveUtc =
                DateTime.UtcNow.Ticks;
        }

        if (movedCount > 0)
        {
            _eventBus.Publish(
                new SaveNeedEvent(saveReason));
        }

        LogCustom(
            "[SystemNpcOfflineRelocationService] Offline relocation finished. " +
            "Hours=" + offlineHours.ToString("0.00") +
            ", moved=" + movedCount);

        return movedCount > 0;
    }

    private RouteConfig PickDebugUnlockedRoute(
    StarSystemConfig currentSystem)
    {
        if (currentSystem == null ||
            currentSystem.Routes == null ||
            currentSystem.Routes.Count == 0)
            return null;

        List<RouteConfig> routes =
            new List<RouteConfig>();

        for (int i = 0; i < currentSystem.Routes.Count; i++)
        {
            RouteConfig route =
                currentSystem.Routes[i];

            if (route == null)
                continue;

            StarSystemConfig targetSystem =
                route.GetOtherSystem(currentSystem.Id);

            if (targetSystem == null)
                continue;

            if (_routeService != null &&
                !_routeService.HasUnlockedRoute(
                    currentSystem.Id,
                    targetSystem.Id))
                continue;

            routes.Add(route);
        }

        if (routes.Count == 0)
            return null;

        return routes[UnityEngine.Random.Range(0, routes.Count)];
    }

    private int GetCurrentQuantTick()
    {
        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null &&
            Bootstrapper.Instance.ServiceRegistry.TryGet<IGameTimeService>(
                out IGameTimeService gameTimeService) &&
            gameTimeService != null)
        {
            return Mathf.Max(1, gameTimeService.CurrentQuantTick);
        }

        return 1;
    }
}