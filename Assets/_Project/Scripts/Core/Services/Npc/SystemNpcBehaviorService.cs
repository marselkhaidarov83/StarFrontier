using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public sealed class SystemNpcBehaviorService : CustomService, ISystemNpcBehaviorService
{
    private const int MinStayDays = 1;
    private const int MaxStayDays = 5;
    private const float offsetPatrol = 600f;

    private readonly ISystemNpcRuntimeService _npcRuntimeService;
    private readonly SimpleEventBus _eventBus;
    private readonly IConfigService _configService;
    private readonly IOrbitalMotionService _orbitalMotionService;

    public SystemNpcBehaviorService()
    {
        _debugStop = true;
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _npcRuntimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _orbitalMotionService = Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();
    }

    public void Tick(StarSystemConfig starSystem, int currentTick)
    {
        // LogCustom("StarSystem = " + starSystem.Id);
        if (string.IsNullOrWhiteSpace(starSystem.Id))
            return;

        var npcs = _npcRuntimeService.GetAliveNpcsInSystem(starSystem.Id);
        // LogCustom("npcs.Count = " + npcs.Count);

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];
            if (npc == null || !npc.IsAlive)
                continue;

            // if (!npc.HasActiveBehavior)
            if (!npc.HasActiveBehavior || HasEnemiesInSystem(npc.CurrentSystemId))
            {
                AssignBehavior(npc, currentTick);
                // LogCustom("npc.RuntimeNpcId = " + npc.RuntimeNpcId + " AssignBehavior");
                continue;
            }

            // LogCustom("npc.RuntimeNpcId = " + npc.RuntimeNpcId + " TickActiveBehavior");
            TickActiveBehavior(npc, currentTick);
        }
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

        LogCustom("npc.RuntimeNpcId = " + npc.RuntimeNpcId);
        LogCustom("npc.CurrentPlanetId = " + npc.CurrentPlanetId);
        if (!string.IsNullOrEmpty(npc.CurrentPlanetId))
        {
            PlanetConfig planetConfig = _configService.GetPlanetConfigById(npc.CurrentPlanetId);
            LogCustom("planetConfig = " + planetConfig);
            npc.CurrentPosition = _orbitalMotionService.GetPlanetCurrentPosition(planetConfig.PlanetOrbit);
        }

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
        string targetId = FindCombatTargetId(npc);

        // if (string.IsNullOrWhiteSpace(targetId))
        if (string.IsNullOrWhiteSpace(targetId) && npc.IsAlly)
        {
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
                return SystemNpcBehaviorType.EngageEnemies;

            case SystemNpcType.Pirate:
                // if (HasEnemiesInSystem(npc.CurrentSystemId))
                // {
                //     float ver = UnityEngine.Random.Range(0, 100);
                //     PirateGroupSpawnRuleConfig pirateGroupSpawnRuleConfig = _configService.GetAllySpawnRuleConfigById(npc.SpawnRuleId);
                //     if (ver < allySpawnRule.EngageEnemiesWeight)
                //     {
                //         LogCustom("SystemNpcBehaviorType.EngageEnemies = " + SystemNpcBehaviorType.EngageEnemies);
                //         return SystemNpcBehaviorType.EngageEnemies;                    
                //     }
                // }

                return GetRandomBehaviorType4Pirate(npc);
            default:
                if (HasEnemiesInSystem(npc.CurrentSystemId))
                {
                    float ver = UnityEngine.Random.Range(0, 100);
                    AllySpawnRuleConfig allySpawnRule = _configService.GetAllySpawnRuleConfigById(npc.SpawnRuleId);
                    if (ver < allySpawnRule.EngageEnemiesWeight)
                    {
                        LogCustom("SystemNpcBehaviorType.EngageEnemies = " + SystemNpcBehaviorType.EngageEnemies);
                        return SystemNpcBehaviorType.EngageEnemies;
                    }
                }

                return GetRandomBehaviorType4Ally(npc);
        }
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
        AllySpawnRuleConfig allySpawnRuleConfig = _configService.GetAllySpawnRuleConfigById(npc.SpawnRuleId);
        List<SystemNpcBehaviorWeight> weights = allySpawnRuleConfig.BehaviorWeights.ToList();

        //Отсекаем невозможные следующие состояния
        switch (npc.PrevBehavior)
        {
            case SystemNpcBehaviorType.AnnihilateOnPlanet:
                weights.Clear();
                break;

            case SystemNpcBehaviorType.EngageEnemies:
            case SystemNpcBehaviorType.PatrolSystem:
            case SystemNpcBehaviorType.TravelToAnotherSystem:
                weights.Remove(weights.First(x => x.BehaviorType == SystemNpcBehaviorType.AnnihilateOnPlanet));
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
        npc.TravelState = SystemNpcTravelState.TravelingInsideSystem;
        npc.IsOnPlanet = false;

        StarSystemConfig starSystem = _configService.GetStarSystemConfigById(npc.CurrentSystemId);
        SystemPopulationConfig config = starSystem.SystemPopulation;

        // LogCustom("starSystem = " + npc.CurrentSystemId);
        // LogCustom("starSystem = " + starSystem.Id);
        // LogCustom("starSystem.PlanetRefs.Length = " + starSystem.PlanetRefs.Length);
        PlanetConfig[] inhabitedPlanets = starSystem.PlanetRefs
            .Where(p => p.IsInhabited == true)
            .ToArray();
        inhabitedPlanets = inhabitedPlanets
            .Where(p => p.Id != npc.CurrentPlanetId)
            .ToArray();

        // LogCustom("inhabitedPlanets.Length = " + inhabitedPlanets.Length);
        PlanetConfig randomPlanet = inhabitedPlanets.Length > 0
            ? inhabitedPlanets[UnityEngine.Random.Range(0, inhabitedPlanets.Length)]
            : null;
        // LogCustom("randomPlanet = " + randomPlanet.Id);

        npc.StartPosition = npc.CurrentPosition;
        npc.TargetPlanetId = randomPlanet.Id;
        npc.TravelProgress01 = 0f;
    }

    private void SetupStayOnPlanet(SystemNpcRuntimeState npc, int currentTick)
    {
        npc.TravelState = SystemNpcTravelState.OnPlanet;
        npc.IsOnPlanet = true;

        npc.DaysToStayOnPlanet = UnityEngine.Random.Range(MinStayDays, MaxStayDays + 1);
        npc.DaysStayedOnPlanet = 0;

        npc.BehaviorEndsTick = currentTick + npc.DaysToStayOnPlanet;
    }

    private void SetupTravelToAnotherSystem(SystemNpcRuntimeState npc)
    {
        npc.TravelState = SystemNpcTravelState.TravelingToAnotherSystem;
        npc.IsOnPlanet = false;

        StarSystemConfig starSystem = _configService.GetStarSystemConfigById(npc.CurrentSystemId);
        StarSystemLink[] starSystemLinks = starSystem.LinkedSystems;

        StarSystemLink starSystemLinkRandom = starSystemLinks.Length > 0
            ? starSystemLinks[UnityEngine.Random.Range(0, starSystemLinks.Length)]
            : null;

        npc.TargetSystemLink = starSystemLinkRandom;
        // npc.TargetSystemId = starSystemLinkRandom.LinkedSystem.Id;
        npc.StartPosition = npc.CurrentPosition;
        // npc.TargetPosition = npc.CurrentPosition + RandomOffset(6f);
        npc.TravelProgress01 = 0f;
    }

    private void SetupAnnihilateOnPlanet(SystemNpcRuntimeState npc)
    {
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
        npc.IsOnPlanet = false;
        string targetId = FindCombatTargetId(npc);

        // if (string.IsNullOrWhiteSpace(targetId))
        if (string.IsNullOrWhiteSpace(targetId) && npc.IsAlly)
        {
            npc.CurrentBehavior = SystemNpcBehaviorType.PatrolSystem;
            SetupPatrolSystem(npc);
            return;
        }

        npc.BehaviorTargetRuntimeNpcId = targetId;
        npc.CurrentTargetRuntimeNpcId = targetId;
        npc.TravelState = SystemNpcTravelState.EngagingEnemy;
        npc.CombatState = SystemNpcCombatState.HasTarget;
        npc.IsFighting = true;
    }

    private void SetupPatrolSystem(SystemNpcRuntimeState npc)
    {
        npc.TravelState = SystemNpcTravelState.Patrolling;
        npc.IsOnPlanet = false;

        npc.StartPosition = npc.CurrentPosition;
        npc.TargetPosition = RandomOffset(offsetPatrol);
        npc.TravelProgress01 = 0f;
    }

    private string FindCombatTargetId(SystemNpcRuntimeState npc)
    {
        float shotDistance = npc.getShotDistance();

        if (npc.IsEnemy)
        {
            List<SystemNpcRuntimeState> list = new();
            foreach (SystemNpcRuntimeState npcRuntimeState in _npcRuntimeService.Npcs)
            {
                if (npcRuntimeState.IsAlive &&
                    npcRuntimeState.IsAlly &&
                    npcRuntimeState.CurrentSystemId == npc.CurrentSystemId &&
                    npcRuntimeState.IsAvailableForCombat() &&
                    Vector3.Distance(npc.CurrentPosition, npcRuntimeState.CurrentPosition) <= shotDistance &&
                    shotDistance != 0f)
                    list.Add(npcRuntimeState);
            }

            SystemNpcRuntimeState allyTarget = null;
            if (list.Count > 0)
                allyTarget = list[UnityEngine.Random.Range(0, list.Count)];

            // SystemNpcRuntimeState allyTarget = _npcRuntimeService.Npcs.FirstOrDefault(x =>
            //     x.IsAlive &&
            //     x.IsAlly &&
            //     x.CurrentSystemId == npc.CurrentSystemId &&
            //     x.IsAvailableForCombat());

            return allyTarget?.RuntimeNpcId;
        }
        else
        {
            List<SystemNpcRuntimeState> list = new();
            foreach (SystemNpcRuntimeState npcRuntimeState in _npcRuntimeService.Npcs)
            {
                if (npcRuntimeState.IsAlive &&
                    npcRuntimeState.IsEnemy &&
                    npcRuntimeState.CurrentSystemId == npc.CurrentSystemId &&
                    npcRuntimeState.IsAvailableForCombat() &&
                    // Vector3.Distance(npc.CurrentPosition, npcRuntimeState.CurrentPosition) <= shotDistance &&
                    shotDistance != 0f)
                    list.Add(npcRuntimeState);
            }

            SystemNpcRuntimeState enemyTarget = null;
            LogCustom("list.Count = " + list.Count);
            if (list.Count > 0)
                enemyTarget = list[UnityEngine.Random.Range(0, list.Count)];

            // SystemNpcRuntimeState allyTarget = _npcRuntimeService.Npcs.FirstOrDefault(x =>
            //     x.IsAlive &&
            //     x.IsAlly &&
            //     x.CurrentSystemId == npc.CurrentSystemId &&
            //     x.IsAvailableForCombat());

            return enemyTarget?.RuntimeNpcId;
        }

        // SystemNpcRuntimeState enemyTarget = _npcRuntimeService.Npcs.FirstOrDefault(x =>
        //     x.IsAlive &&
        //     x.IsEnemy &&
        //     x.CurrentSystemId == npc.CurrentSystemId &&
        //     x.IsAvailableForCombat());

        // return enemyTarget?.RuntimeNpcId;
    }

    private bool HasEnemiesInSystem(string systemId)
    {
        return _npcRuntimeService.Npcs.Any(x =>
            x.IsAlive &&
            x.IsEnemy &&
            x.CurrentSystemId == systemId);
    }

    private Vector3 RandomOffset(float radius)
    {
        Vector2 random = UnityEngine.Random.insideUnitCircle * radius;
        return new Vector3(random.x, random.y, 0f);
    }
}