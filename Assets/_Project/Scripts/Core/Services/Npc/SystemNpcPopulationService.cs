using System;
using System.Linq;
using UnityEngine;

public sealed class SystemNpcPopulationService : CustomService, ISystemNpcPopulationService
{
    private readonly ISystemNpcRuntimeService _npcRuntimeService;
    private readonly IOrbitalMotionService _orbitalMotionService;
    private readonly IGameSessionService _gameSessionService;
    private readonly IConfigService _configService;

    private float randomPosition = 200f;

    public SystemPopulationRuntimeState RuntimeState { get; } = new();

    public SystemNpcPopulationService()
    {
        _debugStop = true;

        _gameSessionService =
            Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();

        _configService =
            Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();

        _npcRuntimeService =
            Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();

        _orbitalMotionService =
            Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();
    }

    public void Tick(StarSystemConfig starSystem, float deltaTime)
    {
        if (starSystem == null)
            return;

        SystemPopulationConfig config =
            starSystem.SystemPopulation;

        if (config == null)
            return;

        if (deltaTime <= 0f)
            return;

        TickAllies(starSystem, deltaTime);
        TickEnemyGroups(starSystem, deltaTime);
    }

    public void ClearRuntimeState()
    {
        RuntimeState.Clear();
    }

    public int ScheduleRespawn(
        SystemNpcRuntimeState npc,
        int destroyedAtTick)
    {
        if (npc == null)
            return 0;

        if (npc.IsPirate)
            return 0;

        string systemId = string.IsNullOrWhiteSpace(npc.OriginSystemId)
            ? npc.CurrentSystemId
            : npc.OriginSystemId;

        if (string.IsNullOrWhiteSpace(systemId))
            return 0;

        string timerRuleId;
        float intervalSeconds;

        if (!TryResolveRespawnRule(
                npc,
                systemId,
                out timerRuleId,
                out intervalSeconds))
        {
            return 0;
        }

        if (npc.IsEnemy &&
            !string.IsNullOrWhiteSpace(npc.GroupRuntimeId) &&
            _npcRuntimeService.GetAliveNpcsByGroupId(
                npc.GroupRuntimeId).Count > 0)
        {
            return 0;
        }

        int delayTicks = Mathf.Max(
            1,
            Mathf.CeilToInt(
                intervalSeconds / GameTimeState.SecondsPerDay));

        int nextRespawnTick =
            Mathf.Max(1, destroyedAtTick) + delayTicks;

        SystemPopulationRuleTimerState timer =
            RuntimeState.GetOrCreateTimer(systemId, timerRuleId);

        timer.TimerSeconds = 0f;
        timer.NextSpawnTick = nextRespawnTick;

        return nextRespawnTick;
    }

    private void TickAllies(
        StarSystemConfig starSystem,
        float deltaTime)
    {
        SystemPopulationConfig config =
            starSystem.SystemPopulation;

        if (config == null)
            return;

        if (config.AllySpawnRules == null)
            return;

        foreach (AllySpawnRuleConfig rule in config.AllySpawnRules)
        {
            if (rule == null)
                continue;

            if (!rule.HasValidAllies())
                continue;

            if (rule.Allies == null)
                continue;

            for (int i = 0; i < rule.Allies.Count; i++)
            {
                AllyGroupEntryConfig entry =
                    rule.Allies[i];

                if (entry == null)
                    continue;

                if (!entry.IsValid())
                    continue;

                AllyConfig allyConfig =
                    entry.AllyConfig;

                if (allyConfig == null)
                    continue;

                int aliveCount =
                    CountAliveAlliesForRule(
                        starSystem.Id,
                        rule,
                        allyConfig);

                string timerKey =
                    BuildAllyTimerKey(
                        rule,
                        allyConfig);

                SystemPopulationRuleTimerState timer =
                    RuntimeState.GetOrCreateTimer(
                        starSystem.Id,
                        timerKey);

                bool respawnIsDue =
                    ConsumeDueRespawn(timer);

                if (timer.NextSpawnTick > 0)
                    continue;

                if (aliveCount < entry.MinCount)
                {
                    int missing =
                        entry.MinCount - aliveCount;

                    for (int c = 0; c < missing; c++)
                    {
                        CreateAlly(
                            starSystem,
                            rule,
                            allyConfig);
                    }

                    continue;
                }

                if (aliveCount >= entry.MaxCount)
                    continue;

                if (respawnIsDue)
                {
                    CreateAlly(
                        starSystem,
                        rule,
                        allyConfig);

                    continue;
                }

                timer.TimerSeconds += deltaTime;

                if (timer.TimerSeconds < rule.SpawnIntervalSeconds)
                    continue;

                timer.TimerSeconds = 0f;

                CreateAlly(
                    starSystem,
                    rule,
                    allyConfig);
            }
        }
    }

    private void TickEnemyGroups(
        StarSystemConfig starSystem,
        float deltaTime)
    {
        SystemPopulationConfig config =
            starSystem.SystemPopulation;

        if (config == null)
            return;

        if (config.EnemyGroupSpawnRules == null)
            return;

        foreach (EnemyGroupSpawnRuleConfig rule in config.EnemyGroupSpawnRules)
        {
            if (rule == null)
                continue;

            if (!rule.HasValidEnemies())
                continue;

            int aliveGroupCount =
                _npcRuntimeService
                    .GetAliveEnemyGroupsByRule(
                        starSystem.Id,
                        rule.Id)
                    .Count;

            if (aliveGroupCount >= rule.MaxAliveGroupsFromThisRule)
                continue;

            SystemPopulationRuleTimerState timer =
                RuntimeState.GetOrCreateTimer(
                    starSystem.Id,
                    rule.Id);

            if (ConsumeDueRespawn(timer))
            {
                CreateEnemyGroup(
                    starSystem,
                    rule);

                continue;
            }

            if (timer.NextSpawnTick > 0)
                continue;

            timer.TimerSeconds += deltaTime;

            if (timer.TimerSeconds < rule.SpawnIntervalSeconds)
                continue;

            timer.TimerSeconds = 0f;

            CreateEnemyGroup(
                starSystem,
                rule);
        }
    }

    private int CountAliveAlliesForRule(
        string systemId,
        AllySpawnRuleConfig rule,
        AllyConfig allyConfig)
    {
        if (rule == null)
            return 0;

        if (allyConfig == null)
            return 0;

        var aliveAllies =
            _npcRuntimeService.GetAliveNpcsBySpawnRule(
                systemId,
                rule.Id,
                SystemNpcType.Ally);

        int count = 0;

        for (int i = 0; i < aliveAllies.Count; i++)
        {
            SystemNpcRuntimeState npc =
                aliveAllies[i];

            if (npc == null)
                continue;

            if (npc.ConfigId != allyConfig.Id)
                continue;

            count++;
        }

        return count;
    }

    private void CreateAlly(
        StarSystemConfig starSystem,
        AllySpawnRuleConfig rule,
        AllyConfig allyConfig)
    {
        if (starSystem == null)
            return;

        if (rule == null)
            return;

        if (allyConfig == null)
            return;

        PlanetConfig randomPlanet =
            PickRandomInhabitedPlanet(starSystem);

        string planetId =
            randomPlanet != null
                ? randomPlanet.Id
                : string.Empty;

        Vector3 position =
            ResolveAllySpawnPosition(
                starSystem,
                randomPlanet);

        SystemNpcRuntimeState ally =
            SystemNpcRuntimeFactory.CreateAlly(
                allyConfig,
                starSystem.Id,
                starSystem.Id,
                planetId,
                position,
                rule.Id);

        ally.BehaviorStartedTick = 0;
        ally.CanChangeLocationOnRestore = true;

        _npcRuntimeService.AddNpc(ally);

        LogCustom(
            "[SystemPopulationService] Ally spawned. " +
            "System: " + starSystem.Id + ", " +
            "Planet: " + planetId + ", " +
            "Rule: " + rule.Id + ", " +
            "Config: " + allyConfig.Id + ", " +
            "Behavior: " + ally.CurrentBehavior);
    }

    private void CreateEnemyGroup(
        StarSystemConfig starSystem,
        EnemyGroupSpawnRuleConfig rule)
    {
        if (starSystem == null)
            return;

        if (rule == null)
            return;

        if (rule.Enemies == null)
            return;

        string groupRuntimeId =
            Guid.NewGuid().ToString("N");

        foreach (EnemyGroupEntryConfig entry in rule.Enemies)
        {
            if (entry == null)
                continue;

            if (entry.EnemyConfig == null)
                continue;

            if (!entry.IsValid())
                continue;

            int count =
                UnityEngine.Random.Range(
                    entry.MinCount,
                    entry.MaxCount + 1);

            for (int i = 0; i < count; i++)
            {
                Vector3 position =
                    BuildEnemySpawnPosition(rule);

                SystemNpcRuntimeState enemy =
                    SystemNpcRuntimeFactory.CreateEnemy(
                        entry.EnemyConfig,
                        starSystem.Id,
                        starSystem.Id,
                        position,
                        rule.Id,
                        groupRuntimeId);

                enemy.CanChangeLocationOnRestore = false;

                _npcRuntimeService.AddNpc(enemy);

                Debug.Log(
                    "[SystemPopulationService] Enemy spawned. " +
                    "System: " + starSystem.Id + ", " +
                    "GroupRule: " + rule.Id + ", " +
                    "Config: " + entry.EnemyConfig.Id + ", " +
                    "GroupRuntimeId: " + groupRuntimeId);
            }
        }
    }

    public string CreatePirateGroup(PirateGroupSpawnRuleConfig rule)
    {
        string groupRuntimeId =
            Guid.NewGuid().ToString("N");

        if (rule == null)
            return groupRuntimeId;

        if (rule.Pirates == null)
            return groupRuntimeId;

        foreach (PirateGroupEntryConfig entry in rule.Pirates)
        {
            if (entry == null || entry.PirateConfig == null)
                continue;

            int count =
                UnityEngine.Random.Range(
                    entry.MinCount,
                    entry.MaxCount + 1);

            for (int i = 0; i < count; i++)
            {
                string currentSystemId =
                    _gameSessionService.State.Player.CurrentSystemId;

                StarSystemConfig starSystem =
                    _configService.GetStarSystemConfigById(
                        currentSystemId);

                if (starSystem == null)
                    continue;

                PlanetConfig randomPlanet =
                    PickRandomInhabitedPlanet(starSystem);

                string planetId =
                    randomPlanet != null
                        ? randomPlanet.Id
                        : string.Empty;

                Vector3 position =
                    ResolveAllySpawnPosition(
                        starSystem,
                        randomPlanet);

                SystemNpcRuntimeState pirate =
                    SystemNpcRuntimeFactory.CreatePirate(
                        entry.PirateConfig,
                        currentSystemId,
                        currentSystemId,
                        position,
                        rule.Id,
                        groupRuntimeId);

                pirate.CurrentPlanetId = planetId;
                pirate.CanChangeLocationOnRestore = false;

                _npcRuntimeService.AddNpc(pirate);

                Debug.Log(
                    "[SystemPopulationService] Pirate spawned. " +
                    "System: " + currentSystemId + ", " +
                    "GroupRule: " + rule.Id + ", " +
                    "Config: " + entry.PirateConfig.Id + ", " +
                    "GroupRuntimeId: " + groupRuntimeId);
            }
        }

        return groupRuntimeId;
    }

    private PlanetConfig PickRandomInhabitedPlanet(
        StarSystemConfig starSystem)
    {
        if (starSystem == null)
            return null;

        if (starSystem.PlanetRefs == null ||
            starSystem.PlanetRefs.Length == 0)
            return null;

        PlanetConfig[] inhabitedPlanets =
            starSystem.PlanetRefs
                .Where(p => p != null && p.IsInhabited)
                .ToArray();

        if (inhabitedPlanets.Length <= 0)
            return null;

        int index =
            UnityEngine.Random.Range(
                0,
                inhabitedPlanets.Length);

        return inhabitedPlanets[index];
    }

    private Vector3 ResolveAllySpawnPosition(
        StarSystemConfig starSystem,
        PlanetConfig planet)
    {
        if (planet != null && planet.PlanetOrbit != null)
        {
            return _orbitalMotionService.GetPlanetCurrentPosition(
                planet.PlanetOrbit);
        }

        if (starSystem != null)
            return starSystem.MapPosition;

        return Vector3.zero;
    }

    private Vector3 BuildEnemySpawnPosition(
        EnemyGroupSpawnRuleConfig rule)
    {
        Vector3 position =
            rule.StartPosition;

        Vector2 randomOffset =
            UnityEngine.Random.insideUnitCircle * randomPosition;

        position.x += randomOffset.x;
        position.y += randomOffset.y;
        position.z = 0f;

        return position;
    }

    private string BuildAllyTimerKey(
        AllySpawnRuleConfig rule,
        AllyConfig allyConfig)
    {
        string ruleId =
            rule != null
                ? rule.Id
                : "unknown_rule";

        string allyConfigId =
            allyConfig != null
                ? allyConfig.Id
                : "unknown_ally";

        return ruleId + "_" + allyConfigId;
    }

    private bool TryResolveRespawnRule(
        SystemNpcRuntimeState npc,
        string systemId,
        out string timerRuleId,
        out float intervalSeconds)
    {
        timerRuleId = string.Empty;
        intervalSeconds = 0f;

        if (npc.IsAlly)
        {
            AllySpawnRuleConfig rule =
                _configService.GetAllySpawnRuleConfigById(
                    npc.SpawnRuleId);

            if (rule == null)
                return false;

            timerRuleId = BuildAllyTimerKey(
                rule,
                _configService.GetAllyConfigById(npc.ConfigId));
            intervalSeconds = rule.SpawnIntervalSeconds;
            return true;
        }

        if (!npc.IsEnemy)
            return false;

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(systemId);

        if (starSystem == null ||
            starSystem.SystemPopulation == null ||
            starSystem.SystemPopulation.EnemyGroupSpawnRules == null)
        {
            return false;
        }

        for (int i = 0;
             i < starSystem.SystemPopulation.EnemyGroupSpawnRules.Length;
             i++)
        {
            EnemyGroupSpawnRuleConfig rule =
                starSystem.SystemPopulation.EnemyGroupSpawnRules[i];

            if (rule == null || rule.Id != npc.SpawnRuleId)
                continue;

            timerRuleId = rule.Id;
            intervalSeconds = rule.SpawnIntervalSeconds;
            return true;
        }

        return false;
    }

    private bool ConsumeDueRespawn(
        SystemPopulationRuleTimerState timer)
    {
        if (timer == null)
            return false;

        if (timer.NextSpawnTick <= 0)
            return false;

        int currentTick = GetCurrentQuantTick();

        if (currentTick < timer.NextSpawnTick)
            return false;

        timer.NextSpawnTick = 0;
        timer.TimerSeconds = 0f;
        return true;
    }

    private int GetCurrentQuantTick()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return 1;
        }

        if (Bootstrapper.Instance.ServiceRegistry.TryGet<IGameTimeService>(
                out IGameTimeService gameTimeService))
        {
            return Mathf.Max(1, gameTimeService.CurrentQuantTick);
        }

        return 1;
    }
}