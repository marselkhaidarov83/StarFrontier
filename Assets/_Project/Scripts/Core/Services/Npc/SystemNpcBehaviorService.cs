using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public sealed class SystemNpcBehaviorService : CustomService, ISystemNpcBehaviorService
{
    private const int MinStayDays = 1;
    private const int MaxStayDays = 5;
    private const int PatrolBoundaryPlanetOrdinal = 8;
    private const float PatrolSunSafetyMargin = 80f;
    private const float PatrolSunFallbackPadding = 1f;
    private const int PatrolPointPickAttempts = 20;
    private const float InvalidRoutePointSqrMagnitude = 0.001f;

    private readonly ISystemNpcRuntimeService _npcRuntimeService;
    private readonly SimpleEventBus _eventBus;
    private readonly IConfigService _configService;
    private readonly IRouteService _routeService;
    private readonly IOrbitalMotionService _orbitalMotionService;
    private readonly ISystemSecurityService _systemSecurityService;

    public SystemNpcBehaviorService()
    {
        _debugStop = true;
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _npcRuntimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _routeService = Bootstrapper.Instance.ServiceRegistry.Get<IRouteService>();
        _orbitalMotionService = Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();
        _systemSecurityService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemSecurityService>();
    }

    public void Tick(StarSystemConfig starSystem, int currentTick)
    {
        if (starSystem == null || string.IsNullOrWhiteSpace(starSystem.Id))
            return;

        var npcs = _npcRuntimeService.GetAliveNpcsInSystem(starSystem.Id);
        bool hasEnemiesInSystem = HasEnemiesInSystem(starSystem.Id);

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];

            if (npc == null || !npc.IsAlive)
                continue;

            if (!npc.HasActiveBehavior)
            {
                AssignBehavior(npc, currentTick);
                continue;
            }

            if (ShouldInterruptForThreat(npc, hasEnemiesInSystem))
            {
                AssignBehavior(npc, currentTick);
                continue;
            }

            TickActiveBehavior(npc, currentTick);
        }
    }

    private bool ShouldInterruptForThreat(
    SystemNpcRuntimeState npc,
    bool hasEnemiesInSystem)
    {
        if (npc == null || !npc.IsAlive)
            return false;

        if (!hasEnemiesInSystem)
            return false;

        if (!CanNpcReactToThreats(npc))
            return false;

        if (npc.CurrentBehavior == SystemNpcBehaviorType.EngageEnemies ||
            npc.TravelState == SystemNpcTravelState.EngagingEnemy)
            return false;

        return !string.IsNullOrWhiteSpace(FindCombatTargetId(npc));
    }

    private bool CanNpcReactToThreats(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return false;

        if (npc.IsEnemy)
            return true;

        if (!npc.IsAlly)
            return false;

        return npc.AllyRole == AllyRole2A.Military ||
               npc.AllyRole == AllyRole2A.Ranger;
    }

    public void AssignBehavior(SystemNpcRuntimeState npc, int currentTick)
    {
        if (npc == null || !npc.IsAlive)
            return;

        SystemNpcBehaviorType nextBehavior = PickFallbackBehavior(npc);

        // LogCustom(
        //     $"NPC: {npc.RuntimeNpcId}, Type: {npc.NpcType}, CurrentBehavior: {npc.CurrentBehavior}, NextBehavior: {nextBehavior}");

        ApplyBehavior(npc, nextBehavior, currentTick);
    }

    public void CompleteBehavior(SystemNpcRuntimeState npc, int currentTick)
    {
        if (npc == null)
            return;

        ClearBehavior(npc);
        AssignBehavior(npc, currentTick);
    }

    public void ClearBehavior(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return;

        npc.HasActiveBehavior = false;
        npc.PrevBehavior = npc.CurrentBehavior;
        npc.CurrentBehavior = SystemNpcBehaviorType.None;
        npc.BehaviorTargetRuntimeNpcId = null;
        npc.DaysToStayOnPlanet = 0;
        npc.DaysStayedOnPlanet = 0;
    }

    private void TickActiveBehavior(SystemNpcRuntimeState npc, int currentTick)
    {
        switch (npc.CurrentBehavior)
        {
            case SystemNpcBehaviorType.StayOnPlanetForDays:
                TickStayOnPlanet(npc, currentTick);
                break;

            case SystemNpcBehaviorType.AnnihilateOnPlanet:
                TickAnnihilateOnPlanet(npc);
                break;

            case SystemNpcBehaviorType.EngageEnemies:
                TickEngageEnemies(npc, currentTick);
                break;

            default:
                // Остальные поведения будут завершаться movement service.
                break;
        }
    }

    private void TickStayOnPlanet(SystemNpcRuntimeState npc, int currentTick)
    {
        if (!npc.IsOnPlanet)
            return;

        if (npc.BehaviorEndsTick <= 0)
            return;

        SyncNpcPositionWithCurrentPlanet(
            npc,
            false);

        if (currentTick < npc.BehaviorEndsTick)
            return;

        LogCustom($"StayOnPlanet complete: {npc.RuntimeNpcId}");

        CompleteBehavior(npc, currentTick);
    }

    private void TickAnnihilateOnPlanet(SystemNpcRuntimeState npc)
    {
        if (!npc.IsOnPlanet)
            return;

        npc.Annihilate();

        LogCustom($"NPC annihilated on planet: {npc.RuntimeNpcId}");
    }

    private void TickEngageEnemies(SystemNpcRuntimeState npc, int currentTick)
    {
        if (npc == null || !npc.IsAlive)
            return;

        string targetId = FindCombatTargetId(npc);

        if (string.IsNullOrWhiteSpace(targetId) && npc.IsAlly)
        {
            npc.CurrentTargetRuntimeNpcId = null;
            npc.BehaviorTargetRuntimeNpcId = null;
            npc.CombatState = SystemNpcCombatState.None;
            npc.IsFighting = false;

            if (CanNpcPatrolSystem(npc))
            {
                npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
                SetupPatrolSystem(npc);
                return;
            }

            CompleteBehavior(npc, currentTick);
            return;
        }

        npc.BehaviorTargetRuntimeNpcId = targetId;
        npc.CurrentTargetRuntimeNpcId = targetId;
        npc.TravelState = SystemNpcTravelState.EngagingEnemy;
        npc.CombatState = SystemNpcCombatState.HasTarget;
        npc.IsFighting = true;
    }

    //Определяем, что Npc делает дальше
    private SystemNpcBehaviorType PickFallbackBehavior(SystemNpcRuntimeState npc)
    {
        switch (npc.NpcType)
        {
            case SystemNpcType.Enemy:
                return GetRandomBehaviorType4Enemy(npc);

            case SystemNpcType.Pirate:
                return GetRandomBehaviorType4Pirate(npc);

            default:
                return GetRandomBehaviorType4Ally(npc);
        }
    }

    public SystemNpcBehaviorType GetRandomBehaviorType4Enemy(SystemNpcRuntimeState npc)
    {
        EnemyConfig enemyConfig =
            _configService.GetEnemyConfigById(npc.ConfigId);

        NpcBehaviourScenarioConfig behaviorScenario =
            GetEnemyBehaviorScenario(enemyConfig, ResolveBehaviorScenario(npc));

        return PickScenarioBehavior(
            behaviorScenario,
            npc);
    }

    public SystemNpcBehaviorType GetRandomBehaviorType4Pirate(SystemNpcRuntimeState npc)
    {
        PirateGroupSpawnRuleConfig pirateGroupSpawnRuleConfig =
                _configService.GetPirateGroupSpawnRuleConfigById(npc.SpawnRuleId);
        List<SystemNpcBehaviorWeight> weights = pirateGroupSpawnRuleConfig.BehaviorWeights.ToList();

        //Отсекаем невозможные следующие состояния
        switch (npc.PrevBehavior)
        {
            case SystemNpcBehaviorType.AnnihilateOnPlanet:
                weights.Clear();
                break;

            case SystemNpcBehaviorType.EngageEnemies:
            case SystemNpcBehaviorType.PatrolSystem:
            case SystemNpcBehaviorType.TravelToAnotherSystem:
                // weights.Remove(weights.First(x => x.BehaviorType == SystemNpcBehaviorType.AnnihilateOnPlanet));
                weights.Remove(weights.First(x => x.BehaviorType == SystemNpcBehaviorType.StayOnPlanetForDays));
                break;

        }

        if (weights == null || weights.Count == 0)
            throw new InvalidOperationException("Behavior weights are empty.");

        int totalWeight = 0;

        foreach (SystemNpcBehaviorWeight item in weights)
            totalWeight += item.Weight;

        if (totalWeight <= 0)
            throw new InvalidOperationException("Total behavior weight must be greater than zero.");

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int cumulative = 0;

        foreach (SystemNpcBehaviorWeight item in weights)
        {
            cumulative += item.Weight;

            if (roll < cumulative)
                return item.BehaviorType;
        }

        return weights[^1].BehaviorType;
    }

    public SystemNpcBehaviorType GetRandomBehaviorType4Ally(SystemNpcRuntimeState npc)
    {
        AllyConfig allyConfig =
            _configService.GetAllyConfigById(npc.ConfigId);

        NpcBehaviourScenarioConfig behaviorScenario =
            GetAllyBehaviorScenario(allyConfig, ResolveBehaviorScenario(npc));

        return PickScenarioBehavior(
            behaviorScenario,
            npc);
    }

    private NpcBehaviourScenarioConfig GetAllyBehaviorScenario(
        AllyConfig allyConfig,
        AllyBehaviourScenario scenario)
    {
        if (allyConfig == null)
            return null;

        NpcBehaviourScenarioConfig behaviorScenario =
            allyConfig.GetBehaviorScenario(scenario);

        if (behaviorScenario != null)
            return behaviorScenario;

        if (scenario != AllyBehaviourScenario.Normal)
            return allyConfig.GetBehaviorScenario(AllyBehaviourScenario.Normal);

        return null;
    }

    private NpcBehaviourScenarioConfig GetEnemyBehaviorScenario(
        EnemyConfig enemyConfig,
        AllyBehaviourScenario scenario)
    {
        if (enemyConfig == null)
            return null;

        NpcBehaviourScenarioConfig behaviorScenario =
            enemyConfig.GetBehaviorScenario(scenario);

        if (behaviorScenario != null)
            return behaviorScenario;

        if (scenario != AllyBehaviourScenario.Normal)
            return enemyConfig.GetBehaviorScenario(AllyBehaviourScenario.Normal);

        return null;
    }

    private AllyBehaviourScenario ResolveBehaviorScenario(
     SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return AllyBehaviourScenario.Normal;

        if (npc.IsAlly && !CanNpcReactToThreats(npc))
            return AllyBehaviourScenario.Normal;

        if (_systemSecurityService != null &&
            _systemSecurityService.TryGetSystemStatus(
                npc.CurrentSystemId,
                out StarSystemStatus systemStatus))
        {
            if (systemStatus == StarSystemStatus.Captured)
                return AllyBehaviourScenario.EnemySystemInvasion;

            if (systemStatus == StarSystemStatus.Threatened ||
                systemStatus == StarSystemStatus.Invasion)
                return AllyBehaviourScenario.EnemyInvasion;
        }

        if (HasEnemiesInSystem(npc.CurrentSystemId))
            return npc.IsEnemy
                ? AllyBehaviourScenario.EnemySystemInvasion
                : AllyBehaviourScenario.EnemyInvasion;

        return AllyBehaviourScenario.Normal;
    }

    private SystemNpcBehaviorType PickScenarioBehavior(
    NpcBehaviourScenarioConfig behaviorScenario,
    SystemNpcRuntimeState npc)
    {
        if (behaviorScenario == null)
            return SystemNpcBehaviorType.None;

        IReadOnlyList<SystemNpcBehaviorWeight> behaviorWeights =
            behaviorScenario.BehaviorWeights;

        if (behaviorWeights == null || behaviorWeights.Count == 0)
            return SystemNpcBehaviorType.None;

        List<SystemNpcBehaviorWeight> weights =
            behaviorWeights
                .Where(weight => weight != null && weight.Weight > 0)
                .ToList();

        RemoveImpossibleNextBehaviors(weights, npc);
        RemoveRoleForbiddenBehaviors(weights, npc);

        if (weights.Count == 0)
            return SystemNpcBehaviorType.None;

        int totalWeight = 0;

        foreach (SystemNpcBehaviorWeight item in weights)
            totalWeight += Mathf.Max(0, item.Weight);

        if (totalWeight <= 0)
            return SystemNpcBehaviorType.None;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int cumulative = 0;

        foreach (SystemNpcBehaviorWeight item in weights)
        {
            cumulative += Mathf.Max(0, item.Weight);

            if (roll < cumulative)
                return item.BehaviorType;
        }

        return weights[^1].BehaviorType;
    }

    private void RemoveRoleForbiddenBehaviors(
    List<SystemNpcBehaviorWeight> weights,
    SystemNpcRuntimeState npc)
    {
        if (weights == null || npc == null)
            return;

        if (!CanNpcPatrolSystem(npc))
        {
            weights.RemoveAll(weight =>
                weight != null &&
                weight.BehaviorType == SystemNpcBehaviorType.PatrolSystem);
        }

        if (npc.IsAlly && !CanNpcReactToThreats(npc))
        {
            weights.RemoveAll(weight =>
                weight != null &&
                weight.BehaviorType == SystemNpcBehaviorType.EngageEnemies);
        }
    }

    private bool CanNpcPatrolSystem(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return false;

        if (!npc.IsAlly)
            return false;

        return npc.AllyRole == AllyRole2A.Military ||
               npc.AllyRole == AllyRole2A.Ranger;
    }

    private void RemoveImpossibleNextBehaviors(
        List<SystemNpcBehaviorWeight> weights,
        SystemNpcRuntimeState npc)
    {
        if (weights == null || npc == null)
            return;

        switch (npc.PrevBehavior)
        {
            case SystemNpcBehaviorType.AnnihilateOnPlanet:
                weights.Clear();
                break;

            case SystemNpcBehaviorType.EngageEnemies:
            case SystemNpcBehaviorType.PatrolSystem:
            case SystemNpcBehaviorType.TravelToAnotherSystem:
                weights.RemoveAll(x =>
                    x.BehaviorType == SystemNpcBehaviorType.AnnihilateOnPlanet ||
                    x.BehaviorType == SystemNpcBehaviorType.StayOnPlanetForDays);
                break;
        }
    }

    private void ApplyBehavior(
        SystemNpcRuntimeState npc,
        SystemNpcBehaviorType nextBehavior,
        int currentTick)
    {
        npc.CurrentBehavior = nextBehavior;
        npc.HasActiveBehavior = true;
        npc.BehaviorStartedTick = currentTick;
        npc.BehaviorEndsTick = 0;
        npc.BehaviorTargetRuntimeNpcId = null;

        // LogCustom(
        //     $"NPC: {npc.RuntimeNpcId}, Type: {npc.NpcType}, CurrentBehavior: {npc.CurrentBehavior}, NextBehavior: {nextBehavior}");

        switch (nextBehavior)
        {
            case SystemNpcBehaviorType.PlanetToPlanetTravel:
                SetupPlanetToPlanetTravel(npc);
                break;

            case SystemNpcBehaviorType.StayOnPlanetForDays:
                SetupStayOnPlanet(npc, currentTick);
                break;

            case SystemNpcBehaviorType.TravelToAnotherSystem:
                SetupTravelToAnotherSystem(npc);
                break;

            case SystemNpcBehaviorType.AnnihilateOnPlanet:
                SetupAnnihilateOnPlanet(npc);
                break;

            case SystemNpcBehaviorType.EngageEnemies:
                SetupEngageEnemies(npc);
                break;

            case SystemNpcBehaviorType.PatrolSystem:
                SetupPatrolSystem(npc);
                break;

            default:
                npc.HasActiveBehavior = false;
                break;
        }

        _eventBus.Publish(new SystemNpcBehaviorChangedEvent(
            npc.RuntimeNpcId,
            npc.CurrentBehavior
        ));
        LogCustom(
            $"NPC: {npc.RuntimeNpcId}, Type: {npc.NpcType}, CurrentBehavior: {npc.CurrentBehavior}, TargetPlanet: {npc.TargetPlanetId}");
    }

    private void SetupPlanetToPlanetTravel(SystemNpcRuntimeState npc)
    {
        SyncNpcPositionWithCurrentPlanet(
            npc,
            true);

        string previousPlanetId =
            npc.CurrentPlanetId;

        ClearMovementTargets(npc);

        npc.TravelState = SystemNpcTravelState.TravelingInsideSystem;
        npc.IsOnPlanet = false;

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        if (starSystem == null || starSystem.PlanetRefs == null)
        {
            npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
            SetupPatrolSystem(npc);
            return;
        }

        PlanetConfig[] inhabitedPlanets = starSystem.PlanetRefs
            .Where(p => p != null && p.IsInhabited == true)
            .ToArray();

        inhabitedPlanets = inhabitedPlanets
            .Where(p => p.Id != previousPlanetId)
            .ToArray();

        PlanetConfig randomPlanet = inhabitedPlanets.Length > 0
            ? inhabitedPlanets[UnityEngine.Random.Range(0, inhabitedPlanets.Length)]
            : null;

        if (randomPlanet == null)
        {
            npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
            SetupPatrolSystem(npc);
            return;
        }

        npc.StartPosition = npc.CurrentPosition;
        npc.TargetPlanetId = randomPlanet.Id;
        npc.TravelProgress01 = 0f;
    }

    private void SetupStayOnPlanet(SystemNpcRuntimeState npc, int currentTick)
    {
        ClearMovementTargets(npc);

        npc.TravelState = SystemNpcTravelState.OnPlanet;
        npc.IsOnPlanet = true;

        npc.DaysToStayOnPlanet = UnityEngine.Random.Range(MinStayDays, MaxStayDays + 1);
        npc.DaysStayedOnPlanet = 0;

        npc.BehaviorEndsTick = currentTick + npc.DaysToStayOnPlanet;
    }

    private void SetupTravelToAnotherSystem(SystemNpcRuntimeState npc)
    {
        StarSystemConfig currentSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        RouteConfig route = FindUnlockedRouteFromCurrentSystem(currentSystem);

        if (route == null)
        {
            npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
            SetupPatrolSystem(npc);
            return;
        }

        StarSystemConfig targetSystem = route.GetOtherSystem(currentSystem.Id);

        if (targetSystem == null)
        {
            npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
            SetupPatrolSystem(npc);
            return;
        }

        npc.TravelState = SystemNpcTravelState.TravelingToAnotherSystem;
        npc.IsOnPlanet = false;

        npc.TargetSystemId = targetSystem.Id;
        npc.TargetSystemExitPoint = route.GetExitPoint(currentSystem.Id);
        npc.TargetSystemEntryPoint = route.GetEntryPoint(targetSystem.Id);

        npc.StartPosition = npc.CurrentPosition;
        npc.TargetPosition = Vector3.zero;
        npc.TargetPlanetId = null;
        npc.CurrentTargetRuntimeNpcId = null;
        npc.TravelProgress01 = 0f;
    }


    private RouteConfig FindUnlockedRouteFromCurrentSystem(StarSystemConfig currentSystem)
    {
        if (currentSystem == null || currentSystem.Routes == null)
            return null;

        IRouteService routeService =
            Bootstrapper.Instance.ServiceRegistry.Get<IRouteService>();

        List<RouteConfig> availableRoutes = new();

        foreach (RouteConfig route in currentSystem.Routes)
        {
            if (route == null)
                continue;

            StarSystemConfig targetSystem = route.GetOtherSystem(currentSystem.Id);

            if (targetSystem == null)
                continue;

            if (!routeService.HasUnlockedRoute(currentSystem.Id, targetSystem.Id))
                continue;

            availableRoutes.Add(route);
        }

        if (availableRoutes.Count == 0)
            return null;

        return availableRoutes[UnityEngine.Random.Range(0, availableRoutes.Count)];
    }

    private void SetupLinkedSystemTravel(SystemNpcRuntimeState npc)
    {
        SyncNpcPositionWithCurrentPlanet(
            npc,
            true);

        ClearMovementTargets(npc);

        npc.TravelState = SystemNpcTravelState.TravelingToAnotherSystem;
        npc.IsOnPlanet = false;

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        StarSystemLink[] starSystemLinks =
            starSystem != null ? starSystem.LinkedSystems : null;

        StarSystemLink link =
            starSystemLinks != null && starSystemLinks.Length > 0
                ? starSystemLinks[UnityEngine.Random.Range(0, starSystemLinks.Length)]
                : null;

        npc.TargetSystemId = link?.LinkedSystem?.Id;
        npc.TargetSystemExitPoint = link?.ExitPoint ?? Vector3.zero;
        npc.TargetSystemEntryPoint = link?.EntryPoint ?? Vector3.zero;

        if (string.IsNullOrWhiteSpace(npc.TargetSystemId) ||
            IsInvalidRoutePoint(starSystem, npc.TargetSystemExitPoint) ||
            IsInvalidRoutePoint(link?.LinkedSystem, npc.TargetSystemEntryPoint))
        {
            npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
            SetupPatrolSystem(npc);
            return;
        }

        npc.StartPosition = npc.CurrentPosition;
        npc.TargetPosition = Vector3.zero;
        npc.TravelProgress01 = 0f;
    }

    private RouteConfig PickUnlockedRouteFromSystem(string currentSystemId)
    {
        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(currentSystemId);

        if (starSystem == null ||
            starSystem.Routes == null ||
            starSystem.Routes.Count == 0)
        {
            return null;
        }

        List<RouteConfig> unlockedRoutes = new();

        foreach (RouteConfig route in starSystem.Routes)
        {
            if (route == null)
                continue;

            StarSystemConfig targetSystem =
                route.GetOtherSystem(currentSystemId);

            if (targetSystem == null ||
                string.IsNullOrWhiteSpace(targetSystem.Id))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(route.Id))
                continue;

            if (!_routeService.IsRouteUnlocked(route.Id))
                continue;

            unlockedRoutes.Add(route);
        }

        if (unlockedRoutes.Count == 0)
            return null;

        return unlockedRoutes[
            UnityEngine.Random.Range(0, unlockedRoutes.Count)];
    }

    private void SetupAnnihilateOnPlanet(SystemNpcRuntimeState npc)
    {
        ClearMovementTargets(npc);

        if (!npc.IsOnPlanet)
        {
            npc.CurrentBehavior = SystemNpcBehaviorType.StayOnPlanetForDays;
            SetupStayOnPlanet(npc, npc.BehaviorStartedTick);
            return;
        }

        npc.TravelState = SystemNpcTravelState.OnPlanet;
    }

    private void SetupEngageEnemies(SystemNpcRuntimeState npc)
    {
        if (npc == null || !npc.IsAlive)
            return;

        SyncNpcPositionWithCurrentPlanet(
            npc,
            true);

        ClearMovementTargets(npc);

        npc.IsOnPlanet = false;

        if (!CanNpcReactToThreats(npc))
        {
            SystemNpcBehaviorType fallbackBehavior =
                PickScenarioBehaviorExcluding(
                    GetAllyBehaviorScenario(
                        _configService.GetAllyConfigById(npc.ConfigId),
                        AllyBehaviourScenario.Normal),
                    npc,
                    SystemNpcBehaviorType.EngageEnemies);

            if (fallbackBehavior == SystemNpcBehaviorType.None)
            {
                ClearBehavior(npc);
                return;
            }

            npc.CurrentBehavior = fallbackBehavior;
            SetupFallbackBehavior(npc, fallbackBehavior);
            return;
        }

        string targetId = FindCombatTargetId(npc);

        if (string.IsNullOrWhiteSpace(targetId) && npc.IsAlly)
        {
            npc.CurrentTargetRuntimeNpcId = null;
            npc.BehaviorTargetRuntimeNpcId = null;
            npc.CombatState = SystemNpcCombatState.None;
            npc.IsFighting = false;

            if (CanNpcPatrolSystem(npc))
            {
                npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
                SetupPatrolSystem(npc);
                return;
            }

            ClearBehavior(npc);
            return;
        }

        npc.CurrentTargetRuntimeNpcId = targetId;
        npc.BehaviorTargetRuntimeNpcId = targetId;
        npc.TravelState = SystemNpcTravelState.EngagingEnemy;
        npc.CombatState = SystemNpcCombatState.HasTarget;
        npc.IsFighting = true;
    }


    private bool SyncNpcPositionWithCurrentPlanet(
    SystemNpcRuntimeState npc,
    bool publishPositionChanged)
    {
        if (npc == null)
            return false;

        if (!npc.IsOnPlanet)
            return false;

        if (string.IsNullOrWhiteSpace(npc.CurrentPlanetId))
            return false;

        if (_configService == null ||
            _orbitalMotionService == null)
        {
            return false;
        }

        PlanetConfig planetConfig =
            _configService.GetPlanetConfigById(npc.CurrentPlanetId);

        if (planetConfig == null ||
            planetConfig.PlanetOrbit == null)
        {
            return false;
        }

        float previousZ =
            npc.CurrentPosition.z;

        Vector3 planetPosition =
            _orbitalMotionService.GetPlanetCurrentPosition(
                planetConfig.PlanetOrbit);

        planetPosition.z = previousZ;
        npc.CurrentPosition = planetPosition;

        if (publishPositionChanged && _eventBus != null)
        {
            _eventBus.Publish(
                new SystemNpcPositionChangedEvent(
                    npc.RuntimeNpcId,
                    npc.CurrentSystemId,
                    npc.CurrentPosition));
        }

        return true;
    }

    private SystemNpcBehaviorType PickScenarioBehaviorExcluding(
        NpcBehaviourScenarioConfig behaviorScenario,
        SystemNpcRuntimeState npc,
        SystemNpcBehaviorType excludedBehavior)
    {
        if (behaviorScenario == null || behaviorScenario.BehaviorWeights == null)
            return SystemNpcBehaviorType.None;

        List<SystemNpcBehaviorWeight> weights =
            behaviorScenario.BehaviorWeights
                .Where(weight =>
                    weight != null &&
                    weight.Weight > 0 &&
                    weight.BehaviorType != excludedBehavior)
                .ToList();

        RemoveImpossibleNextBehaviors(weights, npc);

        if (weights.Count == 0)
            return SystemNpcBehaviorType.None;

        return PickWeightedBehavior(weights);
    }

    private SystemNpcBehaviorType PickWeightedBehavior(
        List<SystemNpcBehaviorWeight> weights)
    {
        if (weights == null || weights.Count == 0)
            return SystemNpcBehaviorType.None;

        int totalWeight = 0;

        foreach (SystemNpcBehaviorWeight item in weights)
            totalWeight += Mathf.Max(0, item.Weight);

        if (totalWeight <= 0)
            return SystemNpcBehaviorType.None;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int cumulative = 0;

        foreach (SystemNpcBehaviorWeight item in weights)
        {
            cumulative += Mathf.Max(0, item.Weight);

            if (roll < cumulative)
                return item.BehaviorType;
        }

        return weights[^1].BehaviorType;
    }

    private void SetupFallbackBehavior(
        SystemNpcRuntimeState npc,
        SystemNpcBehaviorType fallbackBehavior)
    {
        npc.BehaviorTargetRuntimeNpcId = null;
        npc.CurrentTargetRuntimeNpcId = null;
        npc.CombatState = SystemNpcCombatState.None;
        npc.IsFighting = false;

        switch (fallbackBehavior)
        {
            case SystemNpcBehaviorType.PlanetToPlanetTravel:
                SetupPlanetToPlanetTravel(npc);
                break;

            case SystemNpcBehaviorType.StayOnPlanetForDays:
                SetupStayOnPlanet(npc, npc.BehaviorStartedTick);
                break;

            case SystemNpcBehaviorType.TravelToAnotherSystem:
                SetupTravelToAnotherSystem(npc);
                break;

            case SystemNpcBehaviorType.AnnihilateOnPlanet:
                SetupAnnihilateOnPlanet(npc);
                break;

            case SystemNpcBehaviorType.PatrolSystem:
                SetupPatrolSystem(npc);
                break;

            default:
                npc.HasActiveBehavior = false;
                break;
        }
    }

    private void SetupPatrolSystem(SystemNpcRuntimeState npc)
    {
        if (!CanNpcPatrolSystem(npc))
        {
            ClearBehavior(npc);
            return;
        }

        SyncNpcPositionWithCurrentPlanet(
            npc,
            true);

        ClearMovementTargets(npc);

        Vector3 patrolTargetPosition =
            RandomPatrolPosition(npc);

        if (!IsFinite(patrolTargetPosition))
        {
            ClearBehavior(npc);
            return;
        }

        if (Vector3.Distance(npc.CurrentPosition, patrolTargetPosition) <= 0.1f)
        {
            ClearBehavior(npc);
            return;
        }

        npc.TravelState = SystemNpcTravelState.Patrolling;
        npc.IsOnPlanet = false;

        npc.StartPosition = npc.CurrentPosition;
        npc.TargetPosition = patrolTargetPosition;
        npc.CurrentMovementTargetPosition = patrolTargetPosition;
        npc.TickMovementTargetPosition = patrolTargetPosition;
        npc.TravelProgress01 = 0f;

        Vector3 patrolDirection =
            patrolTargetPosition - npc.CurrentPosition;

        patrolDirection.z = 0f;

        if (patrolDirection.sqrMagnitude > 0.0001f)
        {
            patrolDirection.Normalize();
            npc.FacingDirection = patrolDirection;
            npc.TickMovementDirection = patrolDirection;
        }
    }

    private void ClearMovementTargets(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return;

        npc.TargetSystemId = null;
        npc.TargetSystemExitPoint = Vector3.zero;
        npc.TargetSystemEntryPoint = Vector3.zero;
        npc.TargetPlanetId = null;
        npc.CurrentTargetRuntimeNpcId = null;
        npc.BehaviorTargetRuntimeNpcId = null;
        npc.TargetPosition = Vector3.zero;
        npc.CurrentMovementTargetPosition = Vector3.zero;
    }

    private bool IsInvalidRoutePoint(
        StarSystemConfig starSystem,
        Vector3 point)
    {
        if (!IsFinite(point))
            return true;

        if (point.sqrMagnitude <= InvalidRoutePointSqrMagnitude)
            return true;

        if (starSystem == null || starSystem.Sun == null)
            return false;

        SunConfig sun = starSystem.Sun;

        Vector3 sunCenter = new Vector3(
            sun.LocalOffset.x,
            sun.LocalOffset.y,
            point.z);

        float sunRadius = Mathf.Max(0f, GetSunWorldSize(sun) * 0.5f);
        float safeRadius = sunRadius + PatrolSunSafetyMargin;

        return Vector3.Distance(point, sunCenter) <= safeRadius;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }

    private string FindCombatTargetId(SystemNpcRuntimeState npc)
    {
        if (npc == null || !npc.IsAlive)
            return null;

        float shotDistance = npc.getShotDistance();

        if (shotDistance <= 0f)
            return null;

        if (npc.IsEnemy)
        {
            List<SystemNpcRuntimeState> targets = new();

            foreach (SystemNpcRuntimeState npcRuntimeState in _npcRuntimeService.Npcs)
            {
                if (npcRuntimeState == null)
                    continue;

                if (npcRuntimeState.IsAlive &&
                    npcRuntimeState.IsAlly &&
                    npcRuntimeState.CurrentSystemId == npc.CurrentSystemId &&
                    npcRuntimeState.IsAvailableForCombat() &&
                    Vector3.Distance(npc.CurrentPosition, npcRuntimeState.CurrentPosition) <= shotDistance)
                {
                    targets.Add(npcRuntimeState);
                }
            }

            if (targets.Count == 0)
                return null;

            return targets[UnityEngine.Random.Range(0, targets.Count)].RuntimeNpcId;
        }

        if (!CanNpcReactToThreats(npc))
            return null;

        List<SystemNpcRuntimeState> enemyTargets = new();

        foreach (SystemNpcRuntimeState npcRuntimeState in _npcRuntimeService.Npcs)
        {
            if (npcRuntimeState == null)
                continue;

            if (npcRuntimeState.IsAlive &&
                npcRuntimeState.IsEnemy &&
                npcRuntimeState.CurrentSystemId == npc.CurrentSystemId &&
                npcRuntimeState.IsAvailableForCombat())
            {
                enemyTargets.Add(npcRuntimeState);
            }
        }

        if (enemyTargets.Count == 0)
            return null;

        return enemyTargets[UnityEngine.Random.Range(0, enemyTargets.Count)].RuntimeNpcId;
    }

    private bool HasEnemiesInSystem(string systemId)
    {
        return _npcRuntimeService.Npcs.Any(x =>
            x.IsAlive &&
            x.IsEnemy &&
            x.CurrentSystemId == systemId);
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

    private Vector3 RandomOffset(float radius)
    {
        Vector2 random = UnityEngine.Random.insideUnitCircle * radius;
        return new Vector3(random.x, random.y, 0f);
    }

    private Vector3 RandomPatrolPosition(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return Vector3.zero;

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        if (starSystem == null)
            return npc.CurrentPosition;

        if (!TryGetPatrolBounds(
                starSystem,
                out Vector3 patrolCenter,
                out float patrolRadius))
        {
            return npc.CurrentPosition;
        }

        return RandomPatrolOffset(starSystem, patrolCenter, patrolRadius);
    }

    private bool TryGetPatrolBounds(
        StarSystemConfig starSystem,
        out Vector3 patrolCenter,
        out float patrolRadius)
    {
        patrolCenter = Vector3.zero;
        patrolRadius = 0f;

        if (starSystem == null ||
            starSystem.PlanetRefs == null ||
            starSystem.PlanetRefs.Length == 0)
        {
            return false;
        }

        List<PlanetOrbitConfig> orbitConfigs =
            starSystem.PlanetRefs
                .Where(planet => planet != null && planet.PlanetOrbit != null)
                .Select(planet => planet.PlanetOrbit)
                .OrderBy(orbit => orbit.OrbitRadius)
                .ToList();

        if (orbitConfigs.Count == 0)
            return false;

        int boundaryIndex = Mathf.Min(
            PatrolBoundaryPlanetOrdinal - 1,
            orbitConfigs.Count - 1);

        PlanetOrbitConfig boundaryOrbit = orbitConfigs[boundaryIndex];

        patrolCenter = boundaryOrbit.OrbitCenterOffset;
        patrolCenter.z = 0f;
        patrolRadius = Mathf.Max(0f, boundaryOrbit.OrbitRadius);

        return patrolRadius > 0f;
    }

    private Vector3 RandomPatrolOffset(
        StarSystemConfig starSystem,
        Vector3 patrolCenter,
        float patrolRadius)
    {
        if (starSystem == null || starSystem.Sun == null)
            return patrolCenter + RandomOffset(patrolRadius);

        SunConfig sun = starSystem.Sun;
        Vector3 sunCenter = new Vector3(
            sun.LocalOffset.x,
            sun.LocalOffset.y,
            0f);

        float sunRadius = Mathf.Max(0f, GetSunWorldSize(sun) * 0.5f);
        float safeRadius = sunRadius + PatrolSunSafetyMargin;

        for (int i = 0; i < PatrolPointPickAttempts; i++)
        {
            Vector3 candidate = patrolCenter + RandomOffset(patrolRadius);

            if (Vector3.Distance(candidate, sunCenter) > safeRadius)
                return candidate;
        }

        Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;

        if (direction.sqrMagnitude <= 0.001f)
            direction = Vector2.right;

        Vector3 fallback = sunCenter + new Vector3(
            direction.x,
            direction.y,
            0f) * (safeRadius + PatrolSunFallbackPadding);

        if (Vector3.Distance(fallback, patrolCenter) > patrolRadius)
            fallback = patrolCenter +
                       (fallback - patrolCenter).normalized * patrolRadius;

        if (Vector3.Distance(fallback, sunCenter) <= safeRadius)
        {
            Vector3 awayFromSun = patrolCenter - sunCenter;

            if (awayFromSun.sqrMagnitude <= 0.001f)
                awayFromSun = new Vector3(direction.x, direction.y, 0f);

            fallback = patrolCenter + awayFromSun.normalized * patrolRadius;
        }

        return fallback;
    }
}
