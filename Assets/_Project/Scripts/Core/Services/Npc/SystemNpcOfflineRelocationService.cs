using System;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;

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

        List<StarSystemConfig> destinationCandidates =
            BuildOfflineDestinationCandidates(
                state,
                npcs);

        if (destinationCandidates.Count == 0)
            return false;

        Dictionary<string, bool> travelScenarioByConfigId =
            new Dictionary<string, bool>();

        int movedCount = 0;

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];

            if (!CanNpcTryOfflineRelocation(
                    npc,
                    travelScenarioByConfigId))
            {
                continue;
            }

            if (!TryPickDestinationSystemFromCandidates(
                    npc,
                    destinationCandidates,
                    out StarSystemConfig destinationSystem))
            {
                continue;
            }

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
            ", moved=" + movedCount +
            ", candidates=" + destinationCandidates.Count);

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
            npc,
            destinationSystem);

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

    private void PlaceNpcOnRandomMapPoint(
        SystemNpcRuntimeState npc,
        StarSystemConfig system)
    {
        Vector3 position =
            PickRandomMapPosition(system);

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

    private Vector3 PickRandomMapPosition(
        StarSystemConfig system)
    {
        Vector2 center =
            ResolveBoundaryProtectionCenter(system);

        float minRadius =
            MinMapSpawnRadiusFromSun;

        float maxRadius =
            GetMapSpawnMaxRadiusFromSun();

        if (maxRadius < minRadius)
            maxRadius = minRadius;

        float minRadiusSqr =
            minRadius * minRadius;

        float maxRadiusSqr =
            maxRadius * maxRadius;

        for (int i = 0; i < 20; i++)
        {
            float angle =
                UnityEngine.Random.Range(
                    0f,
                    Mathf.PI * 2f);

            float radius =
                Mathf.Sqrt(
                    UnityEngine.Random.Range(
                        minRadiusSqr,
                        maxRadiusSqr));

            Vector2 direction =
                new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle));

            Vector2 candidate =
                center + direction * radius;

            return new Vector3(
                candidate.x,
                candidate.y,
                0f);
        }

        return new Vector3(
            center.x,
            center.y - minRadius,
            0f);
    }

    private float GetMapSpawnMaxRadiusFromSun()
    {
        if (_configService != null &&
            _configService.ShipMovementConfig != null &&
            _configService.ShipMovementConfig.BoundaryProtectionRadiusWorld > 0f)
        {
            return _configService
                .ShipMovementConfig
                .BoundaryProtectionRadiusWorld;
        }

        Vector2 halfSize =
            Vector2.zero;

        if (_configService != null &&
            _configService.ShipMovementConfig != null)
        {
            halfSize =
                _configService.ShipMovementConfig.SystemBoundsHalfSize;
        }

        float fallbackRadius =
            Mathf.Min(
                Mathf.Abs(halfSize.x),
                Mathf.Abs(halfSize.y));

        if (fallbackRadius > MinMapSpawnRadiusFromSun)
            return fallbackRadius;

        return MinMapSpawnRadiusFromSun;
    }

    private static Vector2 ResolveBoundaryProtectionCenter(
        StarSystemConfig system)
    {
        if (system != null &&
            system.Sun != null)
        {
            return new Vector2(
                system.Sun.LocalOffset.x,
                system.Sun.LocalOffset.y);
        }

        return Vector2.zero;
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

    public IEnumerator TryProcessOfflineRoutine(
    GameRuntimeState state,
    float progressFrom01,
    float progressTo01,
    Action<bool> completed)
    {
        if (state == null || state.Meta == null)
        {
            LogOfflineRelocationRoutine(
                "Exit.StateOrMetaNull",
                0d,
                0,
                0,
                0,
                0,
                0,
                LoadingSceneContext.OfflineRelocationNpcPercent,
                0);

            completed?.Invoke(false);
            yield break;
        }

        double offlineHours =
            CalculateOfflineHours(state.Meta);

        if (offlineHours <= 0d)
        {
            LogOfflineRelocationRoutine(
                "Exit.NoOfflineHours",
                offlineHours,
                0,
                0,
                0,
                0,
                0,
                LoadingSceneContext.OfflineRelocationNpcPercent,
                0);

            completed?.Invoke(false);
            yield break;
        }

        IReadOnlyList<SystemNpcRuntimeState> npcs =
            _npcRuntimeService.Npcs;

        int npcCount =
            npcs != null
                ? npcs.Count
                : 0;

        if (npcs == null || npcs.Count == 0)
        {
            LogOfflineRelocationRoutine(
                "Exit.NoNpcs",
                offlineHours,
                npcCount,
                0,
                0,
                0,
                0,
                LoadingSceneContext.OfflineRelocationNpcPercent,
                0);

            completed?.Invoke(false);
            yield break;
        }

        float npcPercent =
            LoadingSceneContext.OfflineRelocationNpcPercent;

        if (npcPercent <= 0f)
        {
            state.Meta.LastSaveUtc =
                DateTime.UtcNow.Ticks;

            LoadingSceneContext.SetProgress(
                string.Empty,
                progressTo01);

            LogOfflineRelocationRoutine(
                "Exit.PercentZero",
                offlineHours,
                npcCount,
                0,
                0,
                0,
                0,
                npcPercent,
                0);

            completed?.Invoke(false);
            yield break;
        }

        LoadingSceneContext.SetProgress(
            string.Empty,
            progressFrom01);

        yield return null;

        List<StarSystemConfig> destinationCandidates =
            BuildOfflineDestinationCandidates(
                state,
                npcs);

        int candidateCount =
            destinationCandidates != null
                ? destinationCandidates.Count
                : 0;

        if (destinationCandidates == null ||
            destinationCandidates.Count == 0)
        {
            LogOfflineRelocationRoutine(
                "Exit.NoDestinationCandidates",
                offlineHours,
                npcCount,
                candidateCount,
                0,
                0,
                0,
                npcPercent,
                0);

            completed?.Invoke(false);
            yield break;
        }

        Dictionary<string, bool> travelScenarioByConfigId =
            new Dictionary<string, bool>();

        int eligibleCount =
            CountOfflineRelocationEligibleNpcs(
                npcs,
                travelScenarioByConfigId);

        int targetEligibleCount =
            Mathf.CeilToInt(
                eligibleCount * npcPercent / 100f);

        if (targetEligibleCount <= 0)
        {
            state.Meta.LastSaveUtc =
                DateTime.UtcNow.Ticks;

            LoadingSceneContext.SetProgress(
                string.Empty,
                progressTo01);

            LogOfflineRelocationRoutine(
                "Exit.NoTargetEligible",
                offlineHours,
                npcCount,
                candidateCount,
                eligibleCount,
                targetEligibleCount,
                0,
                npcPercent,
                0);

            completed?.Invoke(false);
            yield break;
        }

        int movedCount = 0;
        int processedEligibleCount = 0;
        int failedPickDestinationCount = 0;
        int scannedSinceYield = 0;

        long sliceStartedAt =
            System.Diagnostics.Stopwatch.GetTimestamp();

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];

            if (!CanNpcTryOfflineRelocation(
                    npc,
                    travelScenarioByConfigId))
            {
                continue;
            }

            processedEligibleCount++;

            if (TryPickDestinationSystemFromCandidates(
                    npc,
                    destinationCandidates,
                    out StarSystemConfig destinationSystem))
            {
                ApplyOfflineRelocation(
                    npc,
                    destinationSystem);

                movedCount++;
            }
            else
            {
                failedPickDestinationCount++;
            }

            scannedSinceYield++;

            if (processedEligibleCount >= targetEligibleCount)
                break;

            if (ShouldYieldOfflineRelocationSlice(
                    scannedSinceYield,
                    sliceStartedAt))
            {
                float progress01 =
                    Mathf.Lerp(
                        progressFrom01,
                        progressTo01,
                        (float)processedEligibleCount / targetEligibleCount);

                LoadingSceneContext.SetProgress(
                    string.Empty,
                    progress01);

                scannedSinceYield = 0;
                sliceStartedAt =
                    System.Diagnostics.Stopwatch.GetTimestamp();

                yield return null;
            }
        }

        state.Meta.LastSaveUtc =
            DateTime.UtcNow.Ticks;

        if (movedCount > 0)
        {
            _eventBus.Publish(
                new SaveNeedEvent("npc_offline_relocation"));
        }

        LoadingSceneContext.SetProgress(
            string.Empty,
            progressTo01);

        LogOfflineRelocationRoutine(
            movedCount > 0
                ? "Complete.Moved"
                : "Complete.NoMoved",
            offlineHours,
            npcCount,
            candidateCount,
            eligibleCount,
            targetEligibleCount,
            processedEligibleCount,
            npcPercent,
            movedCount,
            failedPickDestinationCount);

        completed?.Invoke(movedCount > 0);
    }

    private static void LogOfflineRelocationRoutine(
        string reason,
        double offlineHours,
        int npcCount,
        int candidateCount,
        int eligibleCount,
        int targetEligibleCount,
        int processedEligibleCount,
        float npcPercent,
        int movedCount,
        int failedPickDestinationCount = 0)
    {
        if (!IsLoadingSceneDiagnosticsLogEnabled())
            return;

        float movedOfEligiblePercent =
            eligibleCount > 0
                ? movedCount * 100f / eligibleCount
                : 0f;

        float movedOfTargetPercent =
            targetEligibleCount > 0
                ? movedCount * 100f / targetEligibleCount
                : 0f;

        Debug.Log(
            "[LOADING_DIAG][OfflineRelocation] " +
            reason +
            " | Frame=" + Time.frameCount +
            " | Time=" + Time.unscaledTime.ToString("F3") +
            " | Hours=" + offlineHours.ToString("0.00") +
            " | Npcs=" + npcCount +
            " | Candidates=" + candidateCount +
            " | Eligible=" + eligibleCount +
            " | PercentSetting=" + npcPercent.ToString("0.0") +
            " | TargetEligible=" + targetEligibleCount +
            " | ProcessedEligible=" + processedEligibleCount +
            " | Moved=" + movedCount +
            " | FailedPickDestination=" + failedPickDestinationCount +
            " | MovedOfEligiblePercent=" + movedOfEligiblePercent.ToString("0.0") +
            " | MovedOfTargetPercent=" + movedOfTargetPercent.ToString("0.0"));
    }

    private static bool IsLoadingSceneDiagnosticsLogEnabled()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.DebugLogConfig == null)
        {
            return false;
        }

        return Bootstrapper.Instance
            .DebugLogConfig
            .LoadingSceneDiagnosticsLogs;
    }

    private int CountOfflineRelocationEligibleNpcs(
        IReadOnlyList<SystemNpcRuntimeState> npcs,
        Dictionary<string, bool> travelScenarioByConfigId)
    {
        if (npcs == null || npcs.Count == 0)
            return 0;

        int count = 0;

        for (int i = 0; i < npcs.Count; i++)
        {
            if (CanNpcTryOfflineRelocation(
                    npcs[i],
                    travelScenarioByConfigId))
            {
                count++;
            }
        }

        return count;
    }

    private bool CanNpcTryOfflineRelocation(
        SystemNpcRuntimeState npc,
        Dictionary<string, bool> travelScenarioByConfigId)
    {
        if (npc == null)
            return false;

        if (!npc.IsAlive)
            return false;

        if (!npc.IsAlly)
            return false;

        if (string.IsNullOrWhiteSpace(npc.CurrentSystemId))
            return false;

        string configId =
            npc.ConfigId ?? string.Empty;

        if (!travelScenarioByConfigId.TryGetValue(
                configId,
                out bool hasTravelScenario))
        {
            hasTravelScenario =
                AllyHasTravelToAnotherSystemScenario(configId);

            travelScenarioByConfigId[configId] =
                hasTravelScenario;
        }

        return hasTravelScenario;
    }

    private List<StarSystemConfig> BuildOfflineDestinationCandidates(
        GameRuntimeState state,
        IReadOnlyList<SystemNpcRuntimeState> npcs)
    {
        List<StarSystemConfig> candidates =
            new List<StarSystemConfig>();

        IReadOnlyList<StarSystemConfig> allSystems =
            _configService.GetAllStarSystems();

        if (allSystems == null || allSystems.Count == 0)
            return candidates;

        HashSet<string> hostileSystemIds =
            BuildAliveHostileSystemIds(npcs);

        for (int i = 0; i < allSystems.Count; i++)
        {
            StarSystemConfig system =
                allSystems[i];

            if (system == null ||
                string.IsNullOrWhiteSpace(system.Id))
            {
                continue;
            }

            if (!IsSystemOpen(system, state))
                continue;

            if (hostileSystemIds.Contains(system.Id))
                continue;

            candidates.Add(system);
        }

        return candidates;
    }

    private HashSet<string> BuildAliveHostileSystemIds(
        IReadOnlyList<SystemNpcRuntimeState> npcs)
    {
        HashSet<string> hostileSystemIds =
            new HashSet<string>();

        if (npcs == null)
            return hostileSystemIds;

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc =
                npcs[i];

            if (npc == null)
                continue;

            if (!npc.IsAlive)
                continue;

            if (!npc.IsHostileToPlayer)
                continue;

            if (string.IsNullOrWhiteSpace(npc.CurrentSystemId))
                continue;

            hostileSystemIds.Add(
                npc.CurrentSystemId);
        }

        return hostileSystemIds;
    }

    private bool TryPickDestinationSystemFromCandidates(
        SystemNpcRuntimeState npc,
        IReadOnlyList<StarSystemConfig> candidates,
        out StarSystemConfig destinationSystem)
    {
        destinationSystem = null;

        if (npc == null ||
            candidates == null ||
            candidates.Count == 0)
        {
            return false;
        }

        if (candidates.Count == 1)
        {
            StarSystemConfig onlyCandidate =
                candidates[0];

            if (onlyCandidate == null ||
                onlyCandidate.Id == npc.CurrentSystemId)
            {
                return false;
            }

            destinationSystem = onlyCandidate;
            return true;
        }

        const int RandomAttempts = 8;

        for (int i = 0; i < RandomAttempts; i++)
        {
            StarSystemConfig candidate =
                candidates[
                    UnityEngine.Random.Range(
                        0,
                        candidates.Count)];

            if (candidate == null)
                continue;

            if (candidate.Id == npc.CurrentSystemId)
                continue;

            destinationSystem = candidate;
            return true;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            StarSystemConfig candidate =
                candidates[i];

            if (candidate == null)
                continue;

            if (candidate.Id == npc.CurrentSystemId)
                continue;

            destinationSystem = candidate;
            return true;
        }

        return false;
    }

    private static bool ShouldYieldOfflineRelocationSlice(
        int scannedSinceYield,
        long sliceStartedAt)
    {
        int maxNpcsPerSlice =
            LoadingSceneContext.OfflineRelocationMaxNpcsPerSlice;

        float maxSliceMs =
            LoadingSceneContext.OfflineRelocationMaxSliceMs;

        if (scannedSinceYield >= maxNpcsPerSlice)
            return true;

        double elapsedMs =
            (System.Diagnostics.Stopwatch.GetTimestamp() - sliceStartedAt) *
            1000.0 /
            System.Diagnostics.Stopwatch.Frequency;

        return elapsedMs >= maxSliceMs;
    }
}