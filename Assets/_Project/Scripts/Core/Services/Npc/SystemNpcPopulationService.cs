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
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _npcRuntimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _orbitalMotionService = Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();
    }

    public void Tick(StarSystemConfig starSystem, float deltaTime)
    {
        SystemPopulationConfig config = starSystem.SystemPopulation;

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

    private void TickAllies(StarSystemConfig starSystem, float deltaTime)
    {
        //TODO: Надо сделать, чтобы в захваченной системе не создавались союзники

        SystemPopulationConfig config = starSystem.SystemPopulation;
        foreach (AllySpawnRuleConfig rule in config.AllySpawnRules)
        {
            if (rule == null || rule.AllyConfig == null)
                continue;

            int aliveCount = CountAliveAlliesForRule(starSystem.Id, rule);

            if (aliveCount < rule.MinCount)
            {
                int missing = rule.MinCount - aliveCount;

                for (int i = 0; i < missing; i++)
                    CreateAlly(starSystem, rule);

                continue;
            }

            if (aliveCount >= rule.MaxCount)
                continue;

            SystemPopulationRuleTimerState timer =
                RuntimeState.GetOrCreateTimer(starSystem.Id, rule.Id);

            timer.TimerSeconds += deltaTime;

            if (timer.TimerSeconds < rule.SpawnIntervalSeconds)
                continue;

            timer.TimerSeconds = 0f;

            CreateAlly(starSystem, rule);
        }
    }

    private void TickEnemyGroups(StarSystemConfig starSystem, float deltaTime)
    {
        //TODO: Надо сделать, чтобы в захваченной системе создалось максимум врагов к имеющейся группе

        SystemPopulationConfig config = starSystem.SystemPopulation;
        foreach (EnemyGroupSpawnRuleConfig rule in config.EnemyGroupSpawnRules)
        {
            if (rule == null)
                continue;

            int aliveGroupCount = _npcRuntimeService
                .GetAliveEnemyGroupsByRule(starSystem.Id, rule.Id)
                .Count;

            if (aliveGroupCount >= rule.MaxAliveGroupsFromThisRule)
                continue;

            SystemPopulationRuleTimerState timer =
                RuntimeState.GetOrCreateTimer(starSystem.Id, rule.Id);

            timer.TimerSeconds += deltaTime;

            if (timer.TimerSeconds < rule.SpawnIntervalSeconds)
                continue;

            timer.TimerSeconds = 0f;

            CreateEnemyGroup(starSystem, rule);
        }
    }

    private int CountAliveAlliesForRule(string systemId, AllySpawnRuleConfig rule)
    {
        return _npcRuntimeService
            .GetAliveNpcsBySpawnRule(
                systemId,
                rule.Id,
                SystemNpcType.Ally)
            .Count;
    }

    private void CreateAlly(StarSystemConfig starSystem, AllySpawnRuleConfig rule)
    {
        SystemPopulationConfig config = starSystem.SystemPopulation;

        PlanetConfig[] inhabitedPlanets = starSystem.PlanetRefs
            .Where(p => p.IsInhabited)
            .ToArray();

        PlanetConfig randomPlanet = inhabitedPlanets.Length > 0
            ? inhabitedPlanets[UnityEngine.Random.Range(0, inhabitedPlanets.Length)]
            : null;

        SystemNpcRuntimeState ally = SystemNpcRuntimeFactory.CreateAlly(
            rule.AllyConfig,
            starSystem.Id,
            starSystem.Id,
            randomPlanet.Id,
            _orbitalMotionService.GetPlanetCurrentPosition(randomPlanet.PlanetOrbit),
            rule.Id
        );

        // ally.CurrentBehavior = SystemNpcBehaviorSelector.PickBehavior(
        //     rule.BehaviorWeights
        // );

        ally.BehaviorStartedTick = 0;
        ally.CanChangeLocationOnRestore = true;

        _npcRuntimeService.AddNpc(ally);

        LogCustom(
            $"[SystemPopulationService] Ally spawned. " +
            $"System: {starSystem.Id}, Planet: {randomPlanet.Id}, Rule: {rule.Id}, Config: {rule.AllyConfig.Id}, Behavior: {ally.CurrentBehavior}"
        );
    }

    private void CreateEnemyGroup(
        StarSystemConfig starSystem,
        EnemyGroupSpawnRuleConfig rule)
    {
        SystemPopulationConfig config = starSystem.SystemPopulation;
        string groupRuntimeId = Guid.NewGuid().ToString("N");

        foreach (EnemyGroupEntryConfig entry in rule.Enemies)
        {
            if (entry == null || entry.EnemyConfig == null)
                continue;

            int count = UnityEngine.Random.Range(
                entry.MinCount,
                entry.MaxCount + 1
            );

            for (int i = 0; i < count; i++)
            {
                Vector3 position = rule.StartPosition;
                position.x += (UnityEngine.Random.insideUnitCircle * randomPosition).x;
                position.y += (UnityEngine.Random.insideUnitCircle * randomPosition).y;

                SystemNpcRuntimeState enemy =
                    SystemNpcRuntimeFactory.CreateEnemy(
                        entry.EnemyConfig,
                        starSystem.Id,
                        starSystem.Id,
                        position,
                        rule.Id,
                        groupRuntimeId
                    );

                // enemy.CurrentBehavior = SystemNpcBehaviorType.EngageEnemies;
                enemy.CanChangeLocationOnRestore = false;

                _npcRuntimeService.AddNpc(enemy);

                Debug.Log(
                    $"[SystemPopulationService] Enemy spawned. " +
                    $"System: {starSystem.Id}, GroupRule: {rule.Id}, Config: {entry.EnemyConfig.Id}, GroupRuntimeId: {groupRuntimeId}"
                );
            }
        }
    }

    public string CreatePirateGroup(PirateGroupSpawnRuleConfig rule)
    {
        string groupRuntimeId = Guid.NewGuid().ToString("N");

        foreach (PirateGroupEntryConfig entry in rule.Pirates)
        {
            if (entry == null || entry.PirateConfig == null)
                continue;

            int count = UnityEngine.Random.Range(
                entry.MinCount,
                entry.MaxCount + 1
            );

            for (int i = 0; i < count; i++)
            {
                // Vector3 position = rule.StartPosition;
                // position.x += (UnityEngine.Random.insideUnitCircle * randomPosition).x;
                // position.y += (UnityEngine.Random.insideUnitCircle * randomPosition).y;

                StarSystemConfig starSystem = _configService.GetStarSystemConfigById(
                            _gameSessionService.CurrentSave.PlayerProfile.CurrentSystemId);
                PlanetConfig[] inhabitedPlanets = starSystem.PlanetRefs
                    .Where(p => p.IsInhabited)
                    .ToArray();

                PlanetConfig randomPlanet = inhabitedPlanets.Length > 0
                    ? inhabitedPlanets[UnityEngine.Random.Range(0, inhabitedPlanets.Length)]
                    : null;

                SystemNpcRuntimeState pirate =
                    SystemNpcRuntimeFactory.CreatePirate(
                        entry.PirateConfig,
                        _gameSessionService.CurrentSave.PlayerProfile.CurrentSystemId,
                        _gameSessionService.CurrentSave.PlayerProfile.CurrentSystemId,
                        _orbitalMotionService.GetPlanetCurrentPosition(randomPlanet.PlanetOrbit),
                        rule.Id,
                        groupRuntimeId
                    );

                // enemy.CurrentBehavior = SystemNpcBehaviorType.EngageEnemies;
                pirate.CanChangeLocationOnRestore = false;

                _npcRuntimeService.AddNpc(pirate);

                Debug.Log(
                    $"[SystemPopulationService] Enemy spawned. " +
                    $"System: {_gameSessionService.CurrentSave.PlayerProfile.CurrentSystemId}, GroupRule: {rule.Id}, Config: {entry.PirateConfig.Id}, GroupRuntimeId: {groupRuntimeId}"
                );
            }
        }

        return groupRuntimeId;
    }
}