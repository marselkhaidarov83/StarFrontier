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
        _debugEnabled = true;
        _debugStop = false;

        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _npcRuntimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _routeService = Bootstrapper.Instance.ServiceRegistry.Get<IRouteService>();
        _orbitalMotionService = Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();
        _systemSecurityService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemSecurityService>();

        LogCustom("[NPC-MILITARY-BEHAVIOR] Service debug enabled.");
    }

    public void Tick(StarSystemConfig starSystem, int currentTick)
    {
        if (starSystem == null || string.IsNullOrWhiteSpace(starSystem.Id))
            return;

        var npcs = _npcRuntimeService.GetAliveNpcsInSystem(starSystem.Id);

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];

            if (npc == null || !npc.IsAlive)
                continue;

            AllyBehaviourScenario resolvedScenario =
                ResolveBehaviorScenario(npc);

            if (!npc.HasActiveBehavior)
            {
                AssignBehavior(npc, currentTick, resolvedScenario);
                continue;
            }

            if (ShouldReassignForScenarioChange(npc, resolvedScenario))
            {
                AssignBehavior(npc, currentTick, resolvedScenario);
                continue;
            }

            TickActiveBehavior(npc, currentTick);
        }
    }

    private bool ShouldReassignForScenarioChange(
    SystemNpcRuntimeState npc,
    AllyBehaviourScenario resolvedScenario)
    {
        if (npc == null || !npc.IsAlive)
            return false;

        if (npc.CurrentBehaviorScenario == resolvedScenario)
            return false;

        return true;
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
        AssignBehavior(
            npc,
            currentTick,
            ResolveBehaviorScenario(npc));
    }

    private void AssignBehavior(
        SystemNpcRuntimeState npc,
        int currentTick,
        AllyBehaviourScenario resolvedScenario)
    {
        if (npc == null || !npc.IsAlive)
            return;

        SystemNpcBehaviorType nextBehavior =
            PickFallbackBehavior(npc, resolvedScenario);

        npc.CurrentBehaviorScenario = resolvedScenario;

        ApplyBehavior(npc, nextBehavior, currentTick);
    }

    private void NormalizeInvalidBehaviorTransitionContext(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return;

        bool hasKnownCurrentPlanet =
            npc.IsOnPlanet &&
            !string.IsNullOrWhiteSpace(npc.CurrentPlanetId);

        if (npc.PrevBehavior != SystemNpcBehaviorType.PlanetToPlanetTravel)
            return;

        if (hasKnownCurrentPlanet)
            return;

        LogCustom(
            "[NPC-BEHAVIOR-DEBUG] Invalid previous PlanetToPlanetTravel context fixed. " +
            "Npc=" + npc.RuntimeNpcId +
            ", PrevBehavior=" + npc.PrevBehavior +
            ", CurrentBehavior=" + npc.CurrentBehavior +
            ", TravelState=" + npc.TravelState +
            ", IsOnPlanet=" + npc.IsOnPlanet +
            ", CurrentPlanet=" + npc.CurrentPlanetId);

        npc.PrevBehavior = SystemNpcBehaviorType.None;
    }

    public void CompleteBehavior(SystemNpcRuntimeState npc, int currentTick)
    {
        if (npc == null)
            return;

        LogCustom(
            "[NPC-BEHAVIOR-DEBUG] CompleteBehavior before clear. " +
            "Npc=" + npc.RuntimeNpcId +
            ", PrevBehavior=" + npc.PrevBehavior +
            ", CurrentBehavior=" + npc.CurrentBehavior +
            ", TravelState=" + npc.TravelState +
            ", IsOnPlanet=" + npc.IsOnPlanet +
            ", CurrentPlanet=" + npc.CurrentPlanetId +
            ", TargetPlanet=" + npc.TargetPlanetId +
            ", Position=" + npc.CurrentPosition +
            ", TargetPosition=" + npc.TargetPosition +
            ", Tick=" + currentTick);

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
    private SystemNpcBehaviorType PickFallbackBehavior(
    SystemNpcRuntimeState npc,
    AllyBehaviourScenario resolvedScenario)
    {
        switch (npc.NpcType)
        {
            case SystemNpcType.Enemy:
                return GetRandomBehaviorType4Enemy(npc, resolvedScenario);

            case SystemNpcType.Pirate:
                return GetRandomBehaviorType4Pirate(npc);

            default:
                return GetRandomBehaviorType4Ally(npc, resolvedScenario);
        }
    }

    public SystemNpcBehaviorType GetRandomBehaviorType4Enemy(SystemNpcRuntimeState npc)
    {
        return GetRandomBehaviorType4Enemy(
            npc,
            ResolveBehaviorScenario(npc));
    }

    private SystemNpcBehaviorType GetRandomBehaviorType4Enemy(
        SystemNpcRuntimeState npc,
        AllyBehaviourScenario resolvedScenario)
    {
        EnemyConfig enemyConfig =
            _configService.GetEnemyConfigById(npc.ConfigId);

        NpcBehaviourScenarioConfig behaviorScenario =
            GetEnemyBehaviorScenario(enemyConfig, resolvedScenario);

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

    private AllyBehaviourScenario ResolveBehaviorScenario(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return AllyBehaviourScenario.Normal;

        if (_systemSecurityService != null &&
            _systemSecurityService.TryGetSystemStatus(
                npc.CurrentSystemId,
                out StarSystemStatus systemStatus))
        {
            if (systemStatus == StarSystemStatus.Captured ||
                systemStatus == StarSystemStatus.Threatened ||
                systemStatus == StarSystemStatus.Invasion)
            {
                if (npc.IsEnemy)
                    return AllyBehaviourScenario.EnemySystemInvasion;

                if (npc.IsAlly)
                    return AllyBehaviourScenario.EnemyInvasion;
            }
        }

        if (HasEnemiesInSystem(npc.CurrentSystemId))
        {
            if (npc.IsEnemy)
                return AllyBehaviourScenario.EnemySystemInvasion;

            if (npc.IsAlly)
                return AllyBehaviourScenario.EnemyInvasion;
        }

        return AllyBehaviourScenario.Normal;
    }

    private string FormatNpcRoleForDebug(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return "NULL";

        if (npc.IsAlly)
            return npc.AllyRole.ToString();

        if (npc.IsEnemy)
            return "Enemy";

        if (npc.IsPirate)
            return "Pirate";

        return npc.NpcType.ToString();
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

        bool shouldLog =
            ShouldLogAllyBehaviorPickTrace(npc, behaviorScenario) ||
            IsMilitaryDebugNpc(npc);

        if (shouldLog)
        {
            LogCustom(
                "[NPC-BEHAVIOR-PICK] Weights before filters. " +
                "Npc=" + (npc != null ? npc.RuntimeNpcId : "NULL_NPC") +
                ", ConfigId=" + (npc != null ? npc.ConfigId : "NULL_CONFIG") +
                ", RuntimeRole=" + (npc != null ? FormatNpcRoleForDebug(npc) : "NULL_ROLE") +
                ", PrevBehavior=" + (npc != null ? npc.PrevBehavior.ToString() : "NULL_PREV") +
                ", Scenario=" + behaviorScenario.Id +
                ", Weights=" + FormatBehaviorWeightsForDebug(weights));
        }

        RemoveImpossibleNextBehaviors(weights, npc);
        RemoveRoleForbiddenBehaviors(weights, npc);

        if (shouldLog)
        {
            LogCustom(
                "[NPC-BEHAVIOR-PICK] Weights after filters. " +
                "Npc=" + (npc != null ? npc.RuntimeNpcId : "NULL_NPC") +
                ", ConfigId=" + (npc != null ? npc.ConfigId : "NULL_CONFIG") +
                ", RuntimeRole=" + (npc != null ? FormatNpcRoleForDebug(npc) : "NULL_ROLE") +
                ", CanReactToThreats=" + CanNpcReactToThreats(npc) +
                ", Scenario=" + behaviorScenario.Id +
                ", Weights=" + FormatBehaviorWeightsForDebug(weights));
        }

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
            {
                if (shouldLog || item.BehaviorType == SystemNpcBehaviorType.EngageEnemies)
                {
                    LogCustom(
                        "[NPC-BEHAVIOR-PICK] Roll result. " +
                        "Npc=" + (npc != null ? npc.RuntimeNpcId : "NULL_NPC") +
                        ", ConfigId=" + (npc != null ? npc.ConfigId : "NULL_CONFIG") +
                        ", RuntimeRole=" + (npc != null ? FormatNpcRoleForDebug(npc) : "NULL_ROLE") +
                        ", Scenario=" + behaviorScenario.Id +
                        ", Roll=" + roll +
                        ", TotalWeight=" + totalWeight +
                        ", PickedBehavior=" + item.BehaviorType);
                }

                return item.BehaviorType;
            }
        }

        return weights[^1].BehaviorType;
    }

    private bool ShouldLogAllyBehaviorPickTrace(
    SystemNpcRuntimeState npc,
    NpcBehaviourScenarioConfig behaviorScenario)
    {
        if (npc == null || !npc.IsAlly)
            return false;

        if (HasEnemiesInSystem(npc.CurrentSystemId))
            return true;

        if (_systemSecurityService != null &&
            _systemSecurityService.TryGetSystemStatus(
                npc.CurrentSystemId,
                out StarSystemStatus systemStatus))
        {
            if (systemStatus == StarSystemStatus.Threatened ||
                systemStatus == StarSystemStatus.Invasion ||
                systemStatus == StarSystemStatus.Captured)
            {
                return true;
            }
        }

        return behaviorScenario != null &&
               !string.IsNullOrWhiteSpace(behaviorScenario.Id) &&
               behaviorScenario.Id.Contains("enemy_invasion");
    }

    private string FormatBehaviorWeightsForDebug(
    IReadOnlyList<SystemNpcBehaviorWeight> weights)
    {
        if (weights == null || weights.Count == 0)
            return "empty";

        return string.Join(
            "; ",
            weights.Select(weight =>
                weight.BehaviorType + "=" + weight.Weight));
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

        RemoveTransitionMatrixForbiddenBehaviors(weights, npc);
        RemoveUnavailableNextBehaviors(weights, npc);
    }

    private void RemoveTransitionMatrixForbiddenBehaviors(
        List<SystemNpcBehaviorWeight> weights,
        SystemNpcRuntimeState npc)
    {
        NpcBehaviourTransitionMatrixConfig transitionMatrix =
            _configService != null
                ? _configService.NpcBehaviourTransitionMatrixConfig
                : null;

        if (transitionMatrix == null)
            return;

        weights.RemoveAll(weight =>
            weight == null ||
            !transitionMatrix.IsNextBehaviorAllowed(
                npc.PrevBehavior,
                weight.BehaviorType));
    }

    private void RemoveUnavailableNextBehaviors(
        List<SystemNpcBehaviorWeight> weights,
        SystemNpcRuntimeState npc)
    {
        weights.RemoveAll(weight =>
            weight == null ||
            !CanUseBehaviorInCurrentConditions(
                npc,
                weight.BehaviorType));
    }

    private bool CanUseBehaviorInCurrentConditions(
    SystemNpcRuntimeState npc,
    SystemNpcBehaviorType behaviorType)
    {
        switch (behaviorType)
        {
            case SystemNpcBehaviorType.StayOnPlanetForDays:
                return IsNpcOnKnownPlanet(npc);

            case SystemNpcBehaviorType.AnnihilateOnPlanet:
                return IsNpcOnKnownPlanet(npc);

            case SystemNpcBehaviorType.AttackMeteorite:
                return false;

            case SystemNpcBehaviorType.AttackMilitaryStation:
                return HasAliveStation(npc, StationType.Military);

            case SystemNpcBehaviorType.AttackRangerBaseStation:
                return HasAliveStation(npc, StationType.RangerBase);

            case SystemNpcBehaviorType.AttackTradeStation:
                return HasAliveStation(npc, StationType.Trade);

            case SystemNpcBehaviorType.AttackScienceStation:
                return HasAliveStation(npc, StationType.Science);

            case SystemNpcBehaviorType.AttackMedicalStation:
                return HasAliveStation(npc, StationType.Medical);

            case SystemNpcBehaviorType.AttackMilitaryAlly:
                return HasAliveAlly(npc, AllyRole2A.Military);

            case SystemNpcBehaviorType.AttackRangerAlly:
                return HasAliveAlly(npc, AllyRole2A.Ranger);

            case SystemNpcBehaviorType.AttackTraderAlly:
                return HasAliveAlly(npc, AllyRole2A.Trader);

            case SystemNpcBehaviorType.AttackScienceAlly:
                return HasAliveAlly(npc, AllyRole2A.Science);

            case SystemNpcBehaviorType.AttackMedicAlly:
                return HasAliveAlly(npc, AllyRole2A.Medic);

            default:
                return true;
        }
    }

    private bool IsNpcOnKnownPlanet(SystemNpcRuntimeState npc)
    {
        return npc != null &&
               npc.IsOnPlanet &&
               !string.IsNullOrWhiteSpace(npc.CurrentPlanetId);
    }

    private bool HasAliveStation(
        SystemNpcRuntimeState npc,
        StationType stationType)
    {
        if (npc == null ||
            _configService == null ||
            string.IsNullOrWhiteSpace(npc.CurrentSystemId))
        {
            return false;
        }

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        StationConfig station = starSystem != null
            ? starSystem.Station
            : null;

        return station != null &&
               station.IsActive &&
               !station.IsDestroyed &&
               station.StationType == stationType;
    }

    private bool HasAliveAlly(
        SystemNpcRuntimeState npc,
        AllyRole2A allyRole)
    {
        if (npc == null ||
            _npcRuntimeService == null ||
            _npcRuntimeService.Npcs == null ||
            string.IsNullOrWhiteSpace(npc.CurrentSystemId))
        {
            return false;
        }

        return _npcRuntimeService.Npcs.Any(candidate =>
            candidate != null &&
            candidate.IsAlive &&
            candidate.IsAlly &&
            candidate.CurrentSystemId == npc.CurrentSystemId &&
            candidate.RuntimeNpcId != npc.RuntimeNpcId &&
            candidate.AllyRole == allyRole);
    }

    private void ApplyBehavior(
    SystemNpcRuntimeState npc,
    SystemNpcBehaviorType nextBehavior,
    int currentTick)
    {
        if (npc == null)
            return;

        SystemNpcBehaviorType previousBehaviorBeforeApply =
            npc.PrevBehavior;

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-BEHAVIOR] ApplyBehavior start. " +
                "Npc=" + npc.RuntimeNpcId +
                ", PrevBehavior=" + npc.PrevBehavior +
                ", NextBehavior=" + nextBehavior +
                ", CurrentBehaviorBefore=" + npc.CurrentBehavior +
                ", TravelStateBefore=" + npc.TravelState +
                ", IsOnPlanetBefore=" + npc.IsOnPlanet +
                ", CurrentPlanetBefore=" + npc.CurrentPlanetId +
                ", TargetPlanetBefore=" + npc.TargetPlanetId +
                ", PositionBefore=" + npc.CurrentPosition +
                ", TargetPositionBefore=" + npc.TargetPosition +
                ", Tick=" + currentTick);
        }

        npc.CurrentBehavior = nextBehavior;
        npc.HasActiveBehavior = true;
        npc.BehaviorStartedTick = currentTick;
        npc.BehaviorEndsTick = 0;
        npc.BehaviorTargetRuntimeNpcId = null;

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

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-BEHAVIOR] ApplyBehavior after setup BEFORE event. " +
                "Npc=" + npc.RuntimeNpcId +
                ", PrevBehaviorBeforeApply=" + previousBehaviorBeforeApply +
                ", CurrentBehavior=" + npc.CurrentBehavior +
                ", HasActiveBehavior=" + npc.HasActiveBehavior +
                ", TravelState=" + npc.TravelState +
                ", IsOnPlanet=" + npc.IsOnPlanet +
                ", CurrentPlanet=" + npc.CurrentPlanetId +
                ", TargetPlanet=" + npc.TargetPlanetId +
                ", StartPosition=" + npc.StartPosition +
                ", CurrentPosition=" + npc.CurrentPosition +
                ", TargetPosition=" + npc.TargetPosition +
                ", CurrentMovementTargetPosition=" + npc.CurrentMovementTargetPosition +
                ", TickMovementTargetPosition=" + npc.TickMovementTargetPosition +
                ", BehaviorEndsTick=" + npc.BehaviorEndsTick +
                ", Tick=" + currentTick);
        }

        _eventBus.Publish(new SystemNpcBehaviorChangedEvent(
            npc.RuntimeNpcId,
            npc.CurrentBehavior));

        _eventBus.Publish(new SystemNpcTravelStateChangedEvent(
            npc.RuntimeNpcId,
            npc,
            npc.TravelState,
            npc.CurrentSystemId));

        LogCustom(
            $"NPC: {npc.RuntimeNpcId}, Type: {npc.NpcType}, CurrentBehavior: {npc.CurrentBehavior}, TargetPlanet: {npc.TargetPlanetId}");
    }

    private void SetupPlanetToPlanetTravel(SystemNpcRuntimeState npc)
    {
        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-BEHAVIOR] SetupPlanetToPlanetTravel start. " +
                "Npc=" + npc.RuntimeNpcId +
                ", CurrentPlanet=" + npc.CurrentPlanetId +
                ", Position=" + npc.CurrentPosition +
                ", TargetPosition=" + npc.TargetPosition +
                ", TravelState=" + npc.TravelState);
        }

        SyncNpcPositionWithCurrentPlanet(npc, true);

        string previousPlanetId = npc.CurrentPlanetId;

        ClearMovementTargets(npc);

        npc.TravelState = SystemNpcTravelState.TravelingInsideSystem;
        npc.IsOnPlanet = false;

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(npc.CurrentSystemId);

        if (starSystem == null || starSystem.PlanetRefs == null)
        {
            if (IsMilitaryDebugNpc(npc))
                LogCustom("[NPC-MILITARY-BEHAVIOR] Planet travel fallback: starSystem or planets missing.");

            npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
            SetupPatrolSystem(npc);
            return;
        }

        PlanetConfig[] inhabitedPlanets = starSystem.PlanetRefs
            .Where(p => p != null && p.IsInhabited == true)
            .Where(p => p.Id != previousPlanetId)
            .ToArray();

        PlanetConfig randomPlanet = inhabitedPlanets.Length > 0
            ? inhabitedPlanets[UnityEngine.Random.Range(0, inhabitedPlanets.Length)]
            : null;

        if (randomPlanet == null)
        {
            if (IsMilitaryDebugNpc(npc))
            {
                LogCustom(
                    "[NPC-MILITARY-BEHAVIOR] Planet travel fallback: no target planet. " +
                    "PreviousPlanet=" + previousPlanetId);
            }

            npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
            SetupPatrolSystem(npc);
            return;
        }

        Vector3 planetPosition = _orbitalMotionService != null &&
                                 randomPlanet.PlanetOrbit != null
            ? _orbitalMotionService.GetPlanetCurrentPosition(randomPlanet.PlanetOrbit)
            : Vector3.zero;

        planetPosition.z = npc.CurrentPosition.z;

        npc.StartPosition = npc.CurrentPosition;
        npc.TargetPlanetId = randomPlanet.Id;
        npc.TargetPosition = planetPosition;
        npc.CurrentMovementTargetPosition = planetPosition;
        npc.TickMovementTargetPosition = planetPosition;
        npc.TravelProgress01 = 0f;

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-BEHAVIOR] SetupPlanetToPlanetTravel target set. " +
                "Npc=" + npc.RuntimeNpcId +
                ", FromPlanet=" + previousPlanetId +
                ", ToPlanet=" + randomPlanet.Id +
                ", StartPosition=" + npc.StartPosition +
                ", PlanetPosition=" + planetPosition +
                ", Distance=" + Vector3.Distance(npc.StartPosition, planetPosition) +
                ", Speed=" + npc.Speed);
        }
    }

    private void SetupStayOnPlanet(SystemNpcRuntimeState npc, int currentTick)
    {
        if (!IsNpcOnKnownPlanet(npc))
        {
            LogCustom(
                "[NPC-BEHAVIOR-DEBUG] StayOnPlanet rejected: NPC is not on known planet. " +
                "Npc=" + npc.RuntimeNpcId +
                ", PrevBehavior=" + npc.PrevBehavior +
                ", TravelState=" + npc.TravelState +
                ", IsOnPlanet=" + npc.IsOnPlanet +
                ", CurrentPlanet=" + npc.CurrentPlanetId);

            npc.PrevBehavior = SystemNpcBehaviorType.None;

            if (CanNpcPatrolSystem(npc))
            {
                npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
                SetupPatrolSystem(npc);
                return;
            }

            ClearBehavior(npc);
            return;
        }

        ClearMovementTargets(npc);

        npc.TravelState = SystemNpcTravelState.OnPlanet;
        npc.IsOnPlanet = true;

        npc.DaysToStayOnPlanet = UnityEngine.Random.Range(MinStayDays, MaxStayDays + 1);
        npc.DaysStayedOnPlanet = 0;

        npc.BehaviorEndsTick = currentTick + npc.DaysToStayOnPlanet;
    }

    private void SetupTravelToAnotherSystem(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return;

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-BEHAVIOR] SetupTravelToAnotherSystem start. " +
                "Npc=" + npc.RuntimeNpcId +
                ", CurrentSystem=" + npc.CurrentSystemId +
                ", CurrentPlanet=" + npc.CurrentPlanetId +
                ", IsOnPlanet=" + npc.IsOnPlanet +
                ", Position=" + npc.CurrentPosition +
                ", TravelState=" + npc.TravelState);
        }

        SyncNpcPositionWithCurrentPlanet(
            npc,
            true);

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

        Vector3 exitPoint = route.GetExitPoint(currentSystem.Id);
        Vector3 entryPoint = route.GetEntryPoint(targetSystem.Id);

        if (IsInvalidRoutePoint(currentSystem, exitPoint) ||
            IsInvalidRoutePoint(targetSystem, entryPoint))
        {
            npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
            SetupPatrolSystem(npc);
            return;
        }

        ClearMovementTargets(npc);

        npc.TravelState = SystemNpcTravelState.TravelingToAnotherSystem;
        npc.IsOnPlanet = false;
        npc.CurrentPlanetId = null;

        npc.TargetSystemId = targetSystem.Id;
        npc.TargetSystemExitPoint = exitPoint;
        npc.TargetSystemEntryPoint = entryPoint;

        npc.StartPosition = npc.CurrentPosition;
        npc.TargetPosition = exitPoint;
        npc.CurrentMovementTargetPosition = exitPoint;
        npc.TickMovementTargetPosition = exitPoint;
        npc.TickMovementDirectionTick = -1;
        npc.TickMovementArrived = false;
        npc.TravelProgress01 = 0f;

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-BEHAVIOR] SetupTravelToAnotherSystem target set without forced facing. " +
                "Npc=" + npc.RuntimeNpcId +
                ", FromSystem=" + currentSystem.Id +
                ", ToSystem=" + targetSystem.Id +
                ", StartPosition=" + npc.StartPosition +
                ", ExitPoint=" + npc.TargetSystemExitPoint +
                ", EntryPoint=" + npc.TargetSystemEntryPoint +
                ", DistanceToExit=" + Vector3.Distance(npc.StartPosition, npc.TargetSystemExitPoint) +
                ", Speed=" + npc.Speed +
                ", FacingDirectionKept=" + npc.FacingDirection +
                ", TickMovementDirectionKept=" + npc.TickMovementDirection);
        }
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

        SyncNpcPositionWithCurrentPlanet(npc, true);
        ClearMovementTargets(npc);

        npc.IsOnPlanet = false;

        string targetId = FindCombatTargetId(npc);

        if (string.IsNullOrWhiteSpace(targetId) && npc.IsAlly)
        {
            npc.CurrentTargetRuntimeNpcId = null;
            npc.BehaviorTargetRuntimeNpcId = null;
            npc.CombatState = SystemNpcCombatState.None;
            npc.IsFighting = false;

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

        patrolTargetPosition.z = -2f;

        Vector3 currentPosition =
            npc.CurrentPosition;

        currentPosition.z = -2f;
        npc.CurrentPosition = currentPosition;

        if (Vector3.Distance(npc.CurrentPosition, patrolTargetPosition) <= 0.1f)
        {
            ClearBehavior(npc);
            return;
        }

        npc.TravelState = SystemNpcTravelState.Patrolling;
        npc.IsOnPlanet = false;

        npc.StartPosition = npc.CurrentPosition;
        npc.TargetPosition = patrolTargetPosition;

        npc.CurrentMovementTargetPosition = Vector3.zero;
        npc.TickMovementTargetPosition = Vector3.zero;
        npc.TickMovementDirectionTick = -1;
        npc.TickMovementArrived = false;

        npc.TravelProgress01 = 0f;

        if (IsMilitaryDebugNpc(npc))
        {
            LogCustom(
                "[NPC-MILITARY-BEHAVIOR] SetupPatrolSystem route target assigned without forced facing. " +
                "Npc=" + npc.RuntimeNpcId +
                ", CurrentPosition=" + npc.CurrentPosition +
                ", PatrolTarget=" + patrolTargetPosition +
                ", FacingDirectionKept=" + npc.FacingDirection +
                ", TickMovementDirectionKept=" + npc.TickMovementDirection);
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
        npc.TickMovementTargetPosition = Vector3.zero;
        npc.TickMovementDirection = Vector3.zero;
        npc.TickMovementDirectionTick = -1;
        npc.TickMovementArrived = false;

        npc.TravelProgress01 = 0f;
        npc.CombatState = SystemNpcCombatState.None;
        npc.IsFighting = false;
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

    private bool IsMilitaryDebugNpc(SystemNpcRuntimeState npc)
    {
        return npc != null &&
               npc.IsAlly;
        //     &&
        //    (npc.AllyRole == AllyRole2A.Military ||
        //     npc.AllyRole == AllyRole2A.Science);
    }

    public SystemNpcBehaviorType GetRandomBehaviorType4Ally(SystemNpcRuntimeState npc)
    {
        return GetRandomBehaviorType4Ally(
            npc,
            ResolveBehaviorScenario(npc));
    }

    private SystemNpcBehaviorType GetRandomBehaviorType4Ally(
        SystemNpcRuntimeState npc,
        AllyBehaviourScenario resolvedScenario)
    {
        AllyConfig allyConfig =
            _configService.GetAllyConfigById(npc.ConfigId);

        NpcBehaviourScenarioConfig behaviorScenario =
            GetAllyBehaviorScenario(allyConfig, resolvedScenario);

        bool shouldLog =
            ShouldLogAllyBehaviorPickTrace(npc, behaviorScenario);

        if (shouldLog)
        {
            LogCustom(
                "[NPC-BEHAVIOR-PICK] Ally pick start. " +
                "Npc=" + npc.RuntimeNpcId +
                ", ConfigId=" + npc.ConfigId +
                ", NpcType=" + npc.NpcType +
                ", RuntimeRole=" + npc.AllyRole +
                ", ConfigRole=" + (allyConfig != null ? allyConfig.Role.ToString() : "NULL_CONFIG") +
                ", Level=" + npc.Level +
                ", CurrentSystem=" + npc.CurrentSystemId +
                ", CurrentBehavior=" + npc.CurrentBehavior +
                ", PrevBehavior=" + npc.PrevBehavior +
                ", TravelState=" + npc.TravelState +
                ", IsOnPlanet=" + npc.IsOnPlanet +
                ", CurrentPlanet=" + npc.CurrentPlanetId +
                ", HasEnemiesInSystem=" + HasEnemiesInSystem(npc.CurrentSystemId) +
                ", ResolvedScenario=" + resolvedScenario +
                ", ScenarioId=" + (behaviorScenario != null ? behaviorScenario.Id : "NULL_SCENARIO") +
                ", ScenarioWeights=" + FormatBehaviorWeightsForDebug(
                    behaviorScenario != null
                        ? behaviorScenario.BehaviorWeights
                        : null));
        }

        SystemNpcBehaviorType pickedBehavior =
            PickScenarioBehavior(
                behaviorScenario,
                npc);

        if (shouldLog || pickedBehavior == SystemNpcBehaviorType.EngageEnemies)
        {
            LogCustom(
                "[NPC-BEHAVIOR-PICK] Ally pick result. " +
                "Npc=" + npc.RuntimeNpcId +
                ", ConfigId=" + npc.ConfigId +
                ", RuntimeRole=" + npc.AllyRole +
                ", PickedBehavior=" + pickedBehavior +
                ", ResolvedScenario=" + resolvedScenario +
                ", ScenarioId=" + (behaviorScenario != null ? behaviorScenario.Id : "NULL_SCENARIO"));
        }

        return pickedBehavior;
    }
}
