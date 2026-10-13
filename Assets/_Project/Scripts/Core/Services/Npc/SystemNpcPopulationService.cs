using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class SystemNpcPopulationService : CustomService, ISystemNpcPopulationService
{
    private readonly Dictionary<string, float> _lastEnemyPopulationDecisionLogBySystem =
    new Dictionary<string, float>();
    private readonly Dictionary<string, float> _lastAllyPopulationDecisionLogBySystem =
    new Dictionary<string, float>();

    private readonly ISystemNpcRuntimeService _npcRuntimeService;
    private readonly IOrbitalMotionService _orbitalMotionService;
    private readonly IGameSessionService _gameSessionService;
    private readonly IConfigService _configService;

    private float randomPosition = 200f;

    private bool OverrideAutomaticSpawnInterval =>
    Bootstrapper.Instance != null &&
    Bootstrapper.Instance.OverrideAutomaticSpawnInterval;

    private float DebugAutomaticSpawnIntervalSeconds =>
        Bootstrapper.Instance != null
            ? Bootstrapper.Instance.DebugAutomaticSpawnIntervalSeconds
            : 5f;

    public SystemPopulationRuntimeState RuntimeState { get; } = new();

    private bool StopAutomaticAllySpawns =>
        Bootstrapper.Instance != null &&
        Bootstrapper.Instance.StopAutomaticAllySpawns;

    private bool StopAutomaticEnemySpawns =>
        Bootstrapper.Instance != null &&
        Bootstrapper.Instance.StopAutomaticEnemySpawns;

    private bool OverrideAutomaticAllySpawnInterval =>
        Bootstrapper.Instance != null &&
        Bootstrapper.Instance.OverrideAutomaticAllySpawnInterval;

    private float DebugAutomaticAllySpawnIntervalSeconds =>
        Bootstrapper.Instance != null
            ? Bootstrapper.Instance.DebugAutomaticAllySpawnIntervalSeconds
            : 5f;

    private bool OverrideNpcGalaxyLevel =>
        Bootstrapper.Instance != null &&
        Bootstrapper.Instance.OverrideNpcGalaxyLevel;

    private int DebugNpcGalaxyLevel =>
        Bootstrapper.Instance != null
            ? Bootstrapper.Instance.DebugNpcGalaxyLevel
            : 1;

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

        if (deltaTime <= 0f)
            return;

        bool isEnemySystem =
            IsEnemySystem(starSystem);

        if (isEnemySystem)
        {
            if (!StopAutomaticEnemySpawns)
                TickEnemySystemPopulation(starSystem, deltaTime);

            return;
        }

        if (GetCurrentPopulationRule(starSystem) == null)
            return;

        if (!StopAutomaticAllySpawns)
            TickAllies(starSystem, deltaTime);

        if (!StopAutomaticEnemySpawns)
            TickEnemyGroups(starSystem, deltaTime);
    }

    private void TickEnemySystemPopulation(
        StarSystemConfig starSystem,
        float deltaTime)
    {
        if (starSystem == null)
            return;

        EnemySpawnRuleSetConfig ruleSet =
            starSystem.EnemySpawnRuleSet;

        if (ruleSet == null)
        {
            LogEnemyPopulationDecision(
                starSystem,
                "SKIP_NO_ENEMY_RULE_SET");
            return;
        }

        EnemyFaction2A? faction =
            ResolveEnemyFactionForSystem(starSystem);

        if (!faction.HasValue)
        {
            LogEnemyPopulationDecision(
                starSystem,
                "SKIP_NO_OWNER_FACTION",
                "EnemyRuleSet=" + ruleSet.Id);
            return;
        }

        EnemySpawnRuleConfig rule =
            ruleSet.GetRuleForFaction(faction.Value);

        if (rule == null)
        {
            LogEnemyPopulationDecision(
                starSystem,
                "SKIP_NO_RULE_FOR_FACTION",
                "EnemyRuleSet=" + ruleSet.Id +
                ", Faction=" + faction.Value);
            return;
        }

        int currentGalaxyLevel =
            GetCurrentGalaxyLevel();

        if (!rule.HasValidEnemiesForGalaxyLevel(currentGalaxyLevel))
        {
            LogEnemyPopulationDecision(
                starSystem,
                "SKIP_NO_VALID_ENEMIES_FOR_LEVEL",
                "EnemyRuleSet=" + ruleSet.Id +
                ", Rule=" + rule.Id +
                ", Faction=" + faction.Value +
                ", GalaxyLevel=" + currentGalaxyLevel);
            return;
        }

        IReadOnlyList<EnemyGroupEntryConfig> enemies =
            rule.GetEnemiesForGalaxyLevel(currentGalaxyLevel);

        if (enemies == null)
        {
            LogEnemyPopulationDecision(
                starSystem,
                "SKIP_NO_ENEMY_ENTRIES",
                "EnemyRuleSet=" + ruleSet.Id +
                ", Rule=" + rule.Id +
                ", Faction=" + faction.Value +
                ", GalaxyLevel=" + currentGalaxyLevel);
            return;
        }

        float spawnIntervalSeconds =
            GetPopulationSpawnIntervalSeconds(
                rule.GetSpawnIntervalSeconds(currentGalaxyLevel));

        TickPopulationEntries(
            starSystem,
            deltaTime,
            enemies,
            currentGalaxyLevel,
            spawnIntervalSeconds,
            rule.Id,
            entry => entry != null && entry.IsValid(),
            entry => entry.EnemyConfig,
            entry => entry.MinCount,
            entry => entry.MaxCount,
            entry => entry.Weight,
            enemyConfig => CountAliveEnemiesForRule(
                starSystem.Id,
                rule,
                enemyConfig),
            enemyConfig => BuildEnemyTimerKey(
                rule,
                enemyConfig),
            enemyConfig => CreateEnemyPopulationEnemy(
                starSystem,
                rule,
                enemyConfig),
            (reason, details) => LogEnemyPopulationDecision(
                starSystem,
                reason,
                details),
            (entry, enemyConfig, aliveCount) =>
                "EnemyRuleSet=" + ruleSet.Id +
                ", Rule=" + rule.Id +
                ", Faction=" + faction.Value +
                ", Config=" + enemyConfig.Id +
                ", GalaxyLevel=" + currentGalaxyLevel +
                ", Alive=" + aliveCount +
                ", Min=" + entry.MinCount +
                ", Max=" + entry.MaxCount);
    }

    private int CountAliveEnemiesForRule(
        string systemId,
        EnemySpawnRuleConfig rule,
        EnemyConfig enemyConfig)
    {
        if (rule == null)
            return 0;

        if (enemyConfig == null)
            return 0;

        var aliveEnemies =
            _npcRuntimeService.GetAliveNpcsBySpawnRule(
                systemId,
                rule.Id,
                SystemNpcType.Enemy);

        int count = 0;

        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            SystemNpcRuntimeState npc =
                aliveEnemies[i];

            if (npc == null)
                continue;

            if (npc.ConfigId != enemyConfig.Id)
                continue;

            count++;
        }

        return count;
    }

    private void CreateEnemyPopulationEnemy(
        StarSystemConfig starSystem,
        EnemySpawnRuleConfig rule,
        EnemyConfig enemyConfig)
    {
        if (starSystem == null)
            return;

        if (rule == null)
            return;

        if (enemyConfig == null)
            return;

        string groupRuntimeId =
            Guid.NewGuid().ToString("N");

        int currentGalaxyLevel =
            GetCurrentGalaxyLevel();

        Vector3 groupSpawnBasePosition =
            PickEnemyGroupSpawnBasePosition(starSystem);

        Vector3 position =
            BuildEnemySpawnPosition(
                starSystem,
                groupSpawnBasePosition);

        SystemNpcRuntimeState enemy =
            SystemNpcRuntimeFactory.CreateEnemy(
                enemyConfig,
                starSystem.Id,
                starSystem.Id,
                position,
                rule.Id,
                groupRuntimeId);

        ApplyInitialFacingToSun(enemy, starSystem);

        enemy.CanChangeLocationOnRestore = false;

        _npcRuntimeService.AddNpc(enemy);

        Debug.Log(
            "[SystemPopulationService] Enemy system population spawned. " +
            "System: " + starSystem.Id + ", " +
            "Rule: " + rule.Id + ", " +
            "Faction: " + rule.Faction + ", " +
            "Config: " + enemyConfig.Id + ", " +
            "GroupRuntimeId: " + groupRuntimeId + ", " +
            "GalaxyLevel: " + currentGalaxyLevel);
    }

    private string BuildEnemyTimerKey(
        EnemySpawnRuleConfig rule,
        EnemyConfig enemyConfig)
    {
        string ruleId =
            rule != null
                ? rule.Id
                : "unknown_rule";

        string enemyConfigId =
            enemyConfig != null
                ? enemyConfig.Id
                : "unknown_enemy";

        return ruleId + "_" + enemyConfigId;
    }

    private void LogEnemyPopulationDecision(
        StarSystemConfig starSystem,
        string reason,
        string details = null)
    {
        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsDebugLogEnabled(DebugLogChannel.Population))
        {
            return;
        }

        string systemId =
            starSystem != null
                ? starSystem.Id
                : string.Empty;

        if (string.IsNullOrWhiteSpace(systemId))
            systemId = "null";

        string key =
            systemId + "|" + reason + "|" + details;

        float now =
            Time.unscaledTime;

        if (_lastEnemyPopulationDecisionLogBySystem.TryGetValue(
                key,
                out float lastLogTime) &&
            now - lastLogTime < 2f)
        {
            return;
        }

        _lastEnemyPopulationDecisionLogBySystem[key] = now;

        Bootstrapper.Instance.LogDebug(
            DebugLogChannel.Population,
            "[EnemySystemPopulation] " +
            "Reason=" + reason +
            ", System=" + systemId +
            (string.IsNullOrWhiteSpace(details)
                ? string.Empty
                : ", " + details));
    }

    private void CreateEnemySpawnRuleGroup(
        StarSystemConfig starSystem,
        EnemySpawnRuleConfig rule)
    {
        if (starSystem == null)
            return;

        if (rule == null)
            return;

        string groupRuntimeId =
            Guid.NewGuid().ToString("N");

        int currentGalaxyLevel =
            GetCurrentGalaxyLevel();

        IReadOnlyList<EnemyGroupEntryConfig> enemies =
            rule.GetEnemiesForGalaxyLevel(currentGalaxyLevel);

        if (enemies == null || enemies.Count == 0)
            return;

        Vector3 groupSpawnBasePosition =
            PickEnemyGroupSpawnBasePosition(starSystem);

        for (int entryIndex = 0; entryIndex < enemies.Count; entryIndex++)
        {
            EnemyGroupEntryConfig entry =
                enemies[entryIndex];

            if (entry == null || !entry.IsValid())
                continue;

            if (entry.EnemyConfig == null)
                continue;

            int count =
                UnityEngine.Random.Range(
                    entry.MinCount,
                    entry.MaxCount + 1);

            for (int i = 0; i < count; i++)
            {
                Vector3 position =
                    BuildEnemySpawnPosition(
                        starSystem,
                        groupSpawnBasePosition);

                SystemNpcRuntimeState enemy =
                    SystemNpcRuntimeFactory.CreateEnemy(
                        entry.EnemyConfig,
                        starSystem.Id,
                        starSystem.Id,
                        position,
                        rule.Id,
                        groupRuntimeId);

                ApplyInitialFacingToSun(enemy, starSystem);

                enemy.CanChangeLocationOnRestore = false;

                _npcRuntimeService.AddNpc(enemy);

                Debug.Log(
                    "[SystemPopulationService] Enemy system population spawned. " +
                    "System: " + starSystem.Id + ", " +
                    "Rule: " + rule.Id + ", " +
                    "Faction: " + rule.Faction + ", " +
                    "Config: " + entry.EnemyConfig.Id + ", " +
                    "GroupRuntimeId: " + groupRuntimeId + ", " +
                    "GalaxyLevel: " + currentGalaxyLevel);
            }
        }
    }


    public void ClearRuntimeState()
    {
        RuntimeState.Clear();
    }

    public void ProcessOfflinePopulation(
        IReadOnlyList<StarSystemConfig> starSystems,
        double offlineHours)
    {
        if (starSystems == null)
            return;

        if (offlineHours <= 0d)
            return;

        if (StopAutomaticAllySpawns)
            return;

        int currentGalaxyLevel =
            GetCurrentGalaxyLevel();

        for (int systemIndex = 0;
             systemIndex < starSystems.Count;
             systemIndex++)
        {
            StarSystemConfig starSystem =
                starSystems[systemIndex];

            if (starSystem == null)
                continue;

            SystemPopulationRule populationRule =
                GetCurrentPopulationRule(starSystem);

            if (populationRule == null)
                continue;

            if (populationRule.AllySpawnRules == null)
                continue;

            for (int ruleIndex = 0;
                 ruleIndex < populationRule.AllySpawnRules.Length;
                 ruleIndex++)
            {
                AllySpawnRuleConfig rule =
                    populationRule.AllySpawnRules[ruleIndex];

                if (rule == null)
                    continue;

                AllySpawnLevelEntryConfig levelEntry =
                    rule.GetEntryForGalaxyLevel(currentGalaxyLevel);

                if (levelEntry == null)
                    continue;

                if (!levelEntry.HasValidAllies())
                    continue;

                if (levelEntry.OfflineSpawnIntervalHours <= 0f)
                    continue;

                int offlineSpawnCycles =
                    Mathf.FloorToInt(
                        (float)(offlineHours / levelEntry.OfflineSpawnIntervalHours));

                if (offlineSpawnCycles <= 0)
                    continue;

                IReadOnlyList<AllyGroupEntryConfig> allies =
                    levelEntry.Allies;

                if (allies == null)
                    continue;

                for (int allyIndex = 0;
                     allyIndex < allies.Count;
                     allyIndex++)
                {
                    AllyGroupEntryConfig entry =
                        allies[allyIndex];

                    if (entry == null)
                        continue;

                    if (!entry.IsValid())
                        continue;

                    AllyConfig allyConfig =
                        entry.AllyConfig;

                    if (allyConfig == null)
                        continue;

                    if (allyConfig.Level != currentGalaxyLevel)
                        continue;

                    int aliveCount =
                        CountAliveAlliesForRule(
                            starSystem.Id,
                            rule,
                            allyConfig);

                    int freeSlots =
                        entry.MaxCount - aliveCount;

                    if (freeSlots <= 0)
                        continue;

                    int countToSpawn =
                        Mathf.Min(
                            freeSlots,
                            offlineSpawnCycles);

                    for (int spawnIndex = 0;
                         spawnIndex < countToSpawn;
                         spawnIndex++)
                    {
                        CreateAlly(
                            starSystem,
                            rule,
                            allyConfig);
                    }
                }
            }
        }
    }

    public int ScheduleRespawn(
        SystemNpcRuntimeState npc,
        int destroyedAtTick)
    {
        if (npc == null)
            return 0;

        if (npc.IsAlly && StopAutomaticAllySpawns)
            return 0;

        if (npc.IsEnemy && StopAutomaticEnemySpawns)
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
        SystemPopulationRule populationRule =
            GetCurrentPopulationRule(starSystem);

        if (populationRule == null)
            return;

        if (populationRule.AllySpawnRules == null)
            return;

        int currentGalaxyLevel =
            GetCurrentGalaxyLevel();

        foreach (AllySpawnRuleConfig rule in populationRule.AllySpawnRules)
        {
            if (rule == null)
                continue;

            if (!rule.HasValidAlliesForGalaxyLevel(currentGalaxyLevel))
                continue;

            IReadOnlyList<AllyGroupEntryConfig> allies =
                rule.GetAlliesForGalaxyLevel(currentGalaxyLevel);

            if (allies == null)
                continue;

            float spawnIntervalSeconds =
                GetPopulationSpawnIntervalSeconds(
                    rule.GetSpawnIntervalSeconds(currentGalaxyLevel));

            TickPopulationEntries(
                starSystem,
                deltaTime,
                allies,
                currentGalaxyLevel,
                spawnIntervalSeconds,
                rule.Id,
                entry => entry != null && entry.IsValid(),
                entry => entry.AllyConfig,
                entry => entry.MinCount,
                entry => entry.MaxCount,
                entry => 1,
                allyConfig => CountAliveAlliesForRule(
                    starSystem.Id,
                    rule,
                    allyConfig),
                allyConfig => BuildAllyTimerKey(
                    rule,
                    allyConfig),
                allyConfig => CreateAlly(
                    starSystem,
                    rule,
                    allyConfig),
                (reason, details) => LogAllyPopulationDecision(
                    starSystem,
                    reason,
                    details),
                (entry, allyConfig, aliveCount) =>
                    "PopulationRule=" + populationRule.Id +
                    ", Rule=" + rule.Id +
                    ", Config=" + allyConfig.Id +
                    ", GalaxyLevel=" + currentGalaxyLevel +
                    ", Alive=" + aliveCount +
                    ", Min=" + entry.MinCount +
                    ", Max=" + entry.MaxCount);
        }
    }

    private void TickPopulationEntries<TEntry, TConfig>(
        StarSystemConfig starSystem,
        float deltaTime,
        IReadOnlyList<TEntry> entries,
        int currentGalaxyLevel,
        float spawnIntervalSeconds,
        string populationTimerKey,
        Func<TEntry, bool> isEntryValid,
        Func<TEntry, TConfig> getConfig,
        Func<TEntry, int> getMinCount,
        Func<TEntry, int> getMaxCount,
        Func<TEntry, int> getWeight,
        Func<TConfig, int> countAlive,
        Func<TConfig, string> buildRespawnTimerKey,
        Action<TConfig> createNpc,
        Action<string, string> logDecision,
        Func<TEntry, TConfig, int, string> buildLogDetails)
        where TEntry : class
        where TConfig : class
    {
        if (starSystem == null)
            return;

        if (entries == null)
            return;

        List<TEntry> intervalCandidates =
            new List<TEntry>();

        List<TConfig> intervalCandidateConfigs =
            new List<TConfig>();

        List<int> intervalCandidateAliveCounts =
            new List<int>();

        for (int i = 0; i < entries.Count; i++)
        {
            TEntry entry =
                entries[i];

            if (entry == null)
                continue;

            if (isEntryValid == null || !isEntryValid(entry))
                continue;

            TConfig config =
                getConfig != null
                    ? getConfig(entry)
                    : null;

            if (config == null)
                continue;

            int aliveCount =
                countAlive != null
                    ? countAlive(config)
                    : 0;

            int minCount =
                getMinCount != null
                    ? getMinCount(entry)
                    : 0;

            int maxCount =
                getMaxCount != null
                    ? getMaxCount(entry)
                    : 0;

            string respawnTimerKey =
                buildRespawnTimerKey != null
                    ? buildRespawnTimerKey(config)
                    : string.Empty;

            if (string.IsNullOrWhiteSpace(respawnTimerKey))
                continue;

            SystemPopulationRuleTimerState respawnTimer =
                RuntimeState.GetOrCreateTimer(
                    starSystem.Id,
                    respawnTimerKey);

            bool respawnIsDue =
                ConsumeDueRespawn(respawnTimer);

            string baseLogDetails =
                buildLogDetails != null
                    ? buildLogDetails(entry, config, aliveCount)
                    : string.Empty;

            if (respawnTimer.NextSpawnTick > 0)
            {
                logDecision?.Invoke(
                    "SKIP_WAITING_RESPAWN_TICK",
                    baseLogDetails +
                    ", NextSpawnTick=" + respawnTimer.NextSpawnTick);
                continue;
            }

            if (aliveCount < minCount)
            {
                int missing =
                    minCount - aliveCount;

                logDecision?.Invoke(
                    "SPAWN_MINIMUM_MISSING",
                    baseLogDetails +
                    ", Missing=" + missing +
                    ", IntervalSeconds=" + spawnIntervalSeconds.ToString("F2"));

                for (int c = 0; c < missing; c++)
                    createNpc?.Invoke(config);

                continue;
            }

            if (aliveCount >= maxCount)
            {
                logDecision?.Invoke(
                    "SKIP_MAX_ALIVE",
                    baseLogDetails +
                    ", IntervalSeconds=" + spawnIntervalSeconds.ToString("F2"));
                continue;
            }

            if (respawnIsDue)
            {
                logDecision?.Invoke(
                    "SPAWN_DUE_RESPAWN",
                    baseLogDetails +
                    ", IntervalSeconds=" + spawnIntervalSeconds.ToString("F2"));

                createNpc?.Invoke(config);

                continue;
            }

            intervalCandidates.Add(entry);
            intervalCandidateConfigs.Add(config);
            intervalCandidateAliveCounts.Add(aliveCount);
        }

        if (intervalCandidates.Count == 0)
            return;

        string intervalTimerKey =
            populationTimerKey + "_interval";

        SystemPopulationRuleTimerState intervalTimer =
            RuntimeState.GetOrCreateTimer(
                starSystem.Id,
                intervalTimerKey);

        intervalTimer.TimerSeconds += deltaTime;

        if (intervalTimer.TimerSeconds < spawnIntervalSeconds)
        {
            for (int i = 0; i < intervalCandidates.Count; i++)
            {
                string details =
                    buildLogDetails != null
                        ? buildLogDetails(
                            intervalCandidates[i],
                            intervalCandidateConfigs[i],
                            intervalCandidateAliveCounts[i])
                        : string.Empty;

                logDecision?.Invoke(
                    "SKIP_WAITING_INTERVAL",
                    details +
                    ", TimerSeconds=" + intervalTimer.TimerSeconds.ToString("F2") +
                    ", IntervalSeconds=" + spawnIntervalSeconds.ToString("F2"));
            }

            return;
        }

        intervalTimer.TimerSeconds = 0f;

        int selectedIndex =
            PickWeightedPopulationEntryIndex(
                intervalCandidates,
                getWeight);

        if (selectedIndex < 0 ||
            selectedIndex >= intervalCandidates.Count)
        {
            return;
        }

        TEntry selectedEntry =
            intervalCandidates[selectedIndex];

        TConfig selectedConfig =
            intervalCandidateConfigs[selectedIndex];

        int selectedAliveCount =
            intervalCandidateAliveCounts[selectedIndex];

        string selectedDetails =
            buildLogDetails != null
                ? buildLogDetails(
                    selectedEntry,
                    selectedConfig,
                    selectedAliveCount)
                : string.Empty;

        logDecision?.Invoke(
            "SPAWN_INTERVAL_READY",
            selectedDetails +
            ", IntervalSeconds=" + spawnIntervalSeconds.ToString("F2"));

        createNpc?.Invoke(selectedConfig);
    }

    private int PickWeightedPopulationEntryIndex<TEntry>(
        IReadOnlyList<TEntry> entries,
        Func<TEntry, int> getWeight)
        where TEntry : class
    {
        if (entries == null || entries.Count == 0)
            return -1;

        int totalWeight = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            TEntry entry =
                entries[i];

            if (entry == null)
                continue;

            int weight =
                getWeight != null
                    ? getWeight(entry)
                    : 1;

            totalWeight += Mathf.Max(1, weight);
        }

        if (totalWeight <= 0)
            return UnityEngine.Random.Range(0, entries.Count);

        int roll =
            UnityEngine.Random.Range(0, totalWeight);

        int cumulative = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            TEntry entry =
                entries[i];

            if (entry == null)
                continue;

            int weight =
                getWeight != null
                    ? getWeight(entry)
                    : 1;

            cumulative += Mathf.Max(1, weight);

            if (roll < cumulative)
                return i;
        }

        return entries.Count - 1;
    }

    private void TickEnemyGroups(
        StarSystemConfig starSystem,
        float deltaTime)
    {
        SystemPopulationRule populationRule =
            GetCurrentPopulationRule(starSystem);

        if (populationRule == null)
            return;

        if (populationRule.EnemyGroupSpawnRuleEntries == null)
            return;

        int currentGalaxyLevel =
            GetCurrentGalaxyLevel();

        List<SystemPopulationEnemyGroupRuleEntry> readyEntries =
            new List<SystemPopulationEnemyGroupRuleEntry>();

        for (int i = 0; i < populationRule.EnemyGroupSpawnRuleEntries.Length; i++)
        {
            SystemPopulationEnemyGroupRuleEntry entry =
                populationRule.EnemyGroupSpawnRuleEntries[i];

            if (entry == null || !entry.IsValid())
                continue;

            EnemyGroupSpawnRuleConfig rule =
                entry.EnemyGroupSpawnRule;

            if (rule == null)
                continue;

            if (!rule.HasValidEnemiesForGalaxyLevel(currentGalaxyLevel))
                continue;

            int aliveGroupCount =
                _npcRuntimeService
                    .GetAliveEnemyGroupsByRule(
                        starSystem.Id,
                        rule.Id)
                    .Count;

            if (aliveGroupCount >=
                rule.GetMaxAliveGroupsForGalaxyLevel(currentGalaxyLevel))
            {
                continue;
            }

            SystemPopulationRuleTimerState timer =
                RuntimeState.GetOrCreateTimer(
                    starSystem.Id,
                    rule.Id);

            if (ConsumeDueRespawn(timer))
            {
                readyEntries.Add(entry);
                continue;
            }

            if (timer.NextSpawnTick > 0)
                continue;

            timer.TimerSeconds += deltaTime;

            if (timer.TimerSeconds <
                rule.GetSpawnIntervalSeconds(currentGalaxyLevel))
            {
                continue;
            }

            readyEntries.Add(entry);
        }

        SystemPopulationEnemyGroupRuleEntry selectedEntry =
            PickWeightedEnemyGroupRuleEntry(readyEntries);

        if (selectedEntry == null)
            return;

        EnemyGroupSpawnRuleConfig selectedRule =
            selectedEntry.EnemyGroupSpawnRule;

        if (selectedRule == null)
            return;

        SystemPopulationRuleTimerState selectedTimer =
            RuntimeState.GetOrCreateTimer(
                starSystem.Id,
                selectedRule.Id);

        selectedTimer.TimerSeconds = 0f;

        CreateEnemyGroup(
            starSystem,
            selectedRule);
    }

    private SystemPopulationEnemyGroupRuleEntry PickWeightedEnemyGroupRuleEntry(
        List<SystemPopulationEnemyGroupRuleEntry> entries)
    {
        if (entries == null || entries.Count == 0)
            return null;

        int totalWeight = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            SystemPopulationEnemyGroupRuleEntry entry =
                entries[i];

            if (entry == null || !entry.IsValid())
                continue;

            totalWeight += Mathf.Max(1, entry.Weight);
        }

        if (totalWeight <= 0)
            return null;

        int roll =
            UnityEngine.Random.Range(0, totalWeight);

        int cumulative = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            SystemPopulationEnemyGroupRuleEntry entry =
                entries[i];

            if (entry == null || !entry.IsValid())
                continue;

            cumulative += Mathf.Max(1, entry.Weight);

            if (roll < cumulative)
                return entry;
        }

        return entries[entries.Count - 1];
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

        string groupRuntimeId =
            Guid.NewGuid().ToString("N");

        int currentGalaxyLevel =
            GetCurrentGalaxyLevel();

        EnemyFaction2A? enemyFaction =
            ResolveEnemyFactionForSystem(starSystem);

        IReadOnlyList<EnemyGroupEntryConfig> enemies =
            rule.PickEnemiesForGalaxyLevel(
                currentGalaxyLevel,
                enemyFaction);

        if (enemies == null || enemies.Count == 0)
            return;

        Vector3 groupSpawnBasePosition =
            PickEnemyGroupSpawnBasePosition(starSystem);

        for (int entryIndex = 0; entryIndex < enemies.Count; entryIndex++)
        {
            EnemyGroupEntryConfig entry =
                enemies[entryIndex];

            if (entry == null || !entry.IsValid())
                continue;

            if (entry.EnemyConfig == null)
                continue;

            int count =
                UnityEngine.Random.Range(
                    entry.MinCount,
                    entry.MaxCount + 1);

            for (int i = 0; i < count; i++)
            {
                Vector3 position =
                    BuildEnemySpawnPosition(
                        starSystem,
                        groupSpawnBasePosition);

                SystemNpcRuntimeState enemy =
                    SystemNpcRuntimeFactory.CreateEnemy(
                        entry.EnemyConfig,
                        starSystem.Id,
                        starSystem.Id,
                        position,
                        rule.Id,
                        groupRuntimeId);

                ApplyInitialFacingToSun(enemy, starSystem);

                enemy.CanChangeLocationOnRestore = false;

                _npcRuntimeService.AddNpc(enemy);

                Debug.Log(
                    "[SystemPopulationService] Enemy spawned. " +
                    "System: " + starSystem.Id + ", " +
                    "GroupRule: " + rule.Id + ", " +
                    "Faction: " + (enemyFaction.HasValue ? enemyFaction.Value.ToString() : "Any") + ", " +
                    "Config: " + entry.EnemyConfig.Id + ", " +
                    "GroupRuntimeId: " + groupRuntimeId + ", " +
                    "GalaxyLevel: " + currentGalaxyLevel);
            }
        }
    }

    private void ApplyInitialFacingToSun(
    SystemNpcRuntimeState npc,
    StarSystemConfig starSystem)
    {
        if (npc == null)
            return;

        Vector3 directionToSun =
            ResolveDirectionToSun(
                starSystem,
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
    StarSystemConfig starSystem,
    Vector3 currentPosition)
    {
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

    public bool DebugSpawnEnemyAttackGroupInCurrentSystem()
    {
        StarSystemConfig starSystem =
            GetCurrentPlayerStarSystem();

        if (starSystem == null)
        {
            Debug.LogWarning(
                "[SystemPopulationService] Debug enemy spawn failed. " +
                "Current player StarSystemConfig was not resolved.");

            return false;
        }

        SystemPopulationRule populationRule =
            GetCurrentPopulationRule(starSystem);

        if (populationRule == null)
        {
            Debug.LogWarning(
                "[SystemPopulationService] Debug enemy spawn failed. " +
                "System has no SystemPopulationRule. System: " +
                starSystem.Id);

            return false;
        }

        if (populationRule.EnemyGroupSpawnRuleEntries == null ||
            populationRule.EnemyGroupSpawnRuleEntries.Length == 0)
        {
            Debug.LogWarning(
                "[SystemPopulationService] Debug enemy spawn failed. " +
                "SystemPopulationRule has no EnemyGroupSpawnRuleEntries. Rule: " +
                populationRule.Id);

            return false;
        }

        int currentGalaxyLevel =
            GetCurrentGalaxyLevel();

        List<SystemPopulationEnemyGroupRuleEntry> readyEntries =
            new List<SystemPopulationEnemyGroupRuleEntry>();

        for (int i = 0; i < populationRule.EnemyGroupSpawnRuleEntries.Length; i++)
        {
            SystemPopulationEnemyGroupRuleEntry entry =
                populationRule.EnemyGroupSpawnRuleEntries[i];

            if (entry == null || !entry.IsValid())
                continue;

            EnemyGroupSpawnRuleConfig rule =
                entry.EnemyGroupSpawnRule;

            if (rule == null)
                continue;

            if (!rule.HasValidEnemiesForGalaxyLevel(currentGalaxyLevel))
                continue;

            int aliveGroupCount =
                _npcRuntimeService
                    .GetAliveEnemyGroupsByRule(
                        starSystem.Id,
                        rule.Id)
                    .Count;

            if (aliveGroupCount >=
                rule.GetMaxAliveGroupsForGalaxyLevel(currentGalaxyLevel))
            {
                continue;
            }

            readyEntries.Add(entry);
        }

        SystemPopulationEnemyGroupRuleEntry selectedEntry =
            PickWeightedEnemyGroupRuleEntry(readyEntries);

        if (selectedEntry == null ||
            selectedEntry.EnemyGroupSpawnRule == null)
        {
            Debug.LogWarning(
                "[SystemPopulationService] Debug enemy spawn failed. " +
                "No eligible enemy group rule for system: " +
                starSystem.Id +
                ", GalaxyLevel: " +
                currentGalaxyLevel);

            return false;
        }

        EnemyGroupSpawnRuleConfig selectedRule =
            selectedEntry.EnemyGroupSpawnRule;

        SystemPopulationRuleTimerState selectedTimer =
            RuntimeState.GetOrCreateTimer(
                starSystem.Id,
                selectedRule.Id);

        selectedTimer.TimerSeconds = 0f;
        selectedTimer.NextSpawnTick = 0;

        CreateEnemyGroup(
            starSystem,
            selectedRule);

        Debug.Log(
            "[SystemPopulationService] Debug enemy attack group spawned. " +
            "System: " +
            starSystem.Id +
            ", Rule: " +
            selectedRule.Id +
            ", RuleWeight: " +
            selectedEntry.Weight +
            ", GalaxyLevel: " +
            currentGalaxyLevel);

        return true;
    }

    public bool DebugSpawnAllyInCurrentSystem(
        AllyRole2A role)
    {
        StarSystemConfig starSystem =
            GetCurrentPlayerStarSystem();

        if (starSystem == null)
        {
            Debug.LogWarning(
                "[SystemPopulationService] Debug ally spawn failed. " +
                "Current player StarSystemConfig was not resolved.");

            return false;
        }

        SystemPopulationRule populationRule =
            GetCurrentPopulationRule(starSystem);

        if (populationRule == null)
        {
            Debug.LogWarning(
                "[SystemPopulationService] Debug ally spawn failed. " +
                "System has no SystemPopulationRule. System: " +
                starSystem.Id);

            return false;
        }

        if (populationRule.AllySpawnRules == null ||
            populationRule.AllySpawnRules.Length == 0)
        {
            Debug.LogWarning(
                "[SystemPopulationService] Debug ally spawn failed. " +
                "SystemPopulationRule has no AllySpawnRules. Rule: " +
                populationRule.Id);

            return false;
        }

        int currentGalaxyLevel =
            GetCurrentGalaxyLevel();

        for (int ruleIndex = 0;
             ruleIndex < populationRule.AllySpawnRules.Length;
             ruleIndex++)
        {
            AllySpawnRuleConfig rule =
                populationRule.AllySpawnRules[ruleIndex];

            if (rule == null)
                continue;

            IReadOnlyList<AllyGroupEntryConfig> allies =
                rule.GetAlliesForGalaxyLevel(currentGalaxyLevel);

            if (allies == null)
                continue;

            for (int allyIndex = 0;
                 allyIndex < allies.Count;
                 allyIndex++)
            {
                AllyGroupEntryConfig entry =
                    allies[allyIndex];

                if (entry == null || !entry.IsValid())
                    continue;

                AllyConfig allyConfig =
                    entry.AllyConfig;

                if (allyConfig == null)
                    continue;

                if (allyConfig.Role != role)
                    continue;

                if (allyConfig.Level != currentGalaxyLevel)
                    continue;

                CreateAlly(
                    starSystem,
                    rule,
                    allyConfig);

                Debug.Log(
                    "[SystemPopulationService] Debug ally spawned. " +
                    "System: " +
                    starSystem.Id +
                    ", Rule: " +
                    rule.Id +
                    ", Role: " +
                    role +
                    ", GalaxyLevel: " +
                    currentGalaxyLevel);

                return true;
            }
        }

        Debug.LogWarning(
            "[SystemPopulationService] Debug ally spawn failed. " +
            "No eligible ally for system: " +
            starSystem.Id +
            ", Role: " +
            role +
            ", GalaxyLevel: " +
            currentGalaxyLevel);

        return false;
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
        {
            return ClampNpcSpawnPositionToBoundaryProtectionRadius(
                starSystem,
                starSystem.MapPosition);
        }

        return Vector3.zero;
    }

    private Vector3 PickEnemyGroupSpawnBasePosition(
        StarSystemConfig starSystem)
    {
        Vector3 position =
            new Vector3(6f, 0f, 0f);

        if (starSystem != null &&
            starSystem.NpcSpawnPoints != null)
        {
            position =
                starSystem.NpcSpawnPoints.PickEnemySpawnBasePosition();
        }

        return ClampNpcSpawnPositionToBoundaryProtectionRadius(
            starSystem,
            position);
    }

    private Vector3 BuildEnemySpawnPosition(
        StarSystemConfig starSystem)
    {
        Vector3 groupSpawnBasePosition =
            PickEnemyGroupSpawnBasePosition(starSystem);

        return BuildEnemySpawnPosition(
            starSystem,
            groupSpawnBasePosition);
    }

    private Vector3 BuildEnemySpawnPosition(
        StarSystemConfig starSystem,
        Vector3 groupSpawnBasePosition)
    {
        float scatterRadius = randomPosition;

        if (starSystem != null &&
            starSystem.NpcSpawnPoints != null)
        {
            scatterRadius = starSystem.NpcSpawnPoints.EnemyRandomRadius;
        }

        Vector3 position =
            BuildTangentialEnemySpawnPosition(
                groupSpawnBasePosition,
                scatterRadius);

        return ClampNpcSpawnPositionToBoundaryProtectionRadius(
            starSystem,
            position);
    }

    private Vector3 BuildTangentialEnemySpawnPosition(
    Vector3 groupSpawnBasePosition,
    float scatterRadius)
    {
        Vector2 radial =
            new Vector2(
                groupSpawnBasePosition.x,
                groupSpawnBasePosition.y);

        if (radial.sqrMagnitude <= 0.0001f)
        {
            radial = Vector2.right;
        }
        else
        {
            radial.Normalize();
        }

        Vector2 tangent =
            new Vector2(
                -radial.y,
                radial.x);

        float tangentOffset =
            UnityEngine.Random.Range(
                -scatterRadius,
                scatterRadius);

        float outwardOffset =
            UnityEngine.Random.Range(
                0f,
                scatterRadius * 0.25f);

        Vector2 scatteredPosition =
            new Vector2(
                groupSpawnBasePosition.x,
                groupSpawnBasePosition.y) +
            tangent * tangentOffset +
            radial * outwardOffset;

        return new Vector3(
            scatteredPosition.x,
            scatteredPosition.y,
            0f);
    }

    private Vector3 ClampNpcSpawnPositionToBoundaryProtectionRadius(
        StarSystemConfig starSystem,
        Vector3 position)
    {
        float protectionRadius =
            GetNpcSpawnBoundaryProtectionRadius();

        if (protectionRadius <= 0f)
            return position;

        Vector2 center =
            ResolveBoundaryProtectionCenter(starSystem);

        Vector2 point =
            new Vector2(
                position.x,
                position.y);

        Vector2 fromCenter =
            point - center;

        float distance =
            fromCenter.magnitude;

        if (distance <= protectionRadius)
            return position;

        if (distance <= 0.0001f)
            return position;

        Vector2 clampedPoint =
            center +
            fromCenter / distance * protectionRadius;

        return new Vector3(
            clampedPoint.x,
            clampedPoint.y,
            position.z);
    }

    private float GetNpcSpawnBoundaryProtectionRadius()
    {
        if (_configService == null ||
            _configService.ShipMovementConfig == null)
        {
            return 0f;
        }

        return _configService
            .ShipMovementConfig
            .BoundaryProtectionRadiusWorld;
    }

    private static Vector2 ResolveBoundaryProtectionCenter(
        StarSystemConfig starSystem)
    {
        if (starSystem != null &&
            starSystem.Sun != null)
        {
            return new Vector2(
                starSystem.Sun.LocalOffset.x,
                starSystem.Sun.LocalOffset.y);
        }

        return Vector2.zero;
    }

    private StarSystemConfig GetCurrentPlayerStarSystem()
    {
        if (_gameSessionService == null ||
            !_gameSessionService.HasActiveSession ||
            _gameSessionService.State == null ||
            _gameSessionService.State.Player == null)
        {
            return null;
        }

        string currentSystemId =
            _gameSessionService.State.Player.CurrentSystemId;

        if (string.IsNullOrWhiteSpace(currentSystemId))
            return null;

        return _configService.GetStarSystemConfigById(currentSystemId);
    }

    private SystemPopulationRule GetCurrentPopulationRule(
        StarSystemConfig starSystem)
    {
        if (starSystem == null)
            return null;

        return starSystem.SystemPopulationRule;
    }

    private bool IsEnemySystem(
        StarSystemConfig starSystem)
    {
        if (starSystem == null ||
            string.IsNullOrWhiteSpace(starSystem.Id))
        {
            return false;
        }

        if (_gameSessionService == null ||
            _gameSessionService.State == null ||
            _gameSessionService.State.Galaxy == null ||
            _gameSessionService.State.Galaxy.Systems == null)
        {
            return false;
        }

        for (int systemIndex = 0;
             systemIndex < _gameSessionService.State.Galaxy.Systems.Count;
             systemIndex++)
        {
            StarSystemRuntimeState systemState =
                _gameSessionService.State.Galaxy.Systems[systemIndex];

            if (systemState == null)
                continue;

            if (systemState.SystemId != starSystem.Id)
                continue;

            return systemState.SystemStatus == StarSystemStatus.Captured;
        }

        return false;
    }

    private EnemyFaction2A? ResolveEnemyFactionForSystem(
        StarSystemConfig starSystem)
    {
        if (starSystem == null ||
            string.IsNullOrWhiteSpace(starSystem.Id))
        {
            return null;
        }

        if (_gameSessionService == null ||
            _gameSessionService.State == null ||
            _gameSessionService.State.Galaxy == null ||
            _gameSessionService.State.Galaxy.EnemyFactions == null)
        {
            return null;
        }

        GalaxyRuntimeState galaxyState =
            _gameSessionService.State.Galaxy;

        for (int factionIndex = 0;
             factionIndex < galaxyState.EnemyFactions.Count;
             factionIndex++)
        {
            EnemyFactionState factionState =
                galaxyState.EnemyFactions[factionIndex];

            if (factionState == null)
                continue;

            factionState.EnsureWarTerritoryLists();

            if (!factionState.OwnedSystemIds.Contains(starSystem.Id))
                continue;

            return ParseEnemyFaction(
                factionState.FactionId);
        }

        return null;
    }

    private static EnemyFaction2A ParseEnemyFaction(
        string factionId)
    {
        if (string.IsNullOrWhiteSpace(factionId))
            return EnemyFaction2A.AI;

        switch (factionId.Trim().ToLowerInvariant())
        {
            case "ancients":
                return EnemyFaction2A.Ancients;
            case "infected":
                return EnemyFaction2A.Infected;
            default:
                return EnemyFaction2A.AI;
        }
    }

    private float GetAllySpawnIntervalSeconds(
    AllySpawnRuleConfig rule,
    int galaxyLevel)
    {
        if (rule == null)
            return 0f;

        return GetPopulationSpawnIntervalSeconds(
            rule.GetSpawnIntervalSeconds(galaxyLevel));
    }

    private int GetCurrentGalaxyLevel()
    {
        if (OverrideNpcGalaxyLevel)
            return DebugNpcGalaxyLevel;

        if (_gameSessionService == null ||
            !_gameSessionService.HasActiveSession ||
            _gameSessionService.State == null ||
            _gameSessionService.State.Galaxy == null ||
            _gameSessionService.State.Galaxy.Sectors == null)
        {
            return 1;
        }

        int unlockedSectorCount =
            _gameSessionService.State.Galaxy.Sectors.Count(
                sector => sector != null && sector.IsUnlocked);

        return Mathf.Clamp(
            Mathf.Max(1, unlockedSectorCount),
            1,
            10);
    }

    private static bool IsEnemyConfigAllowedForGalaxyLevel(
        EnemyConfig enemyConfig,
        int galaxyLevel)
    {
        if (enemyConfig == null)
            return false;

        int normalizedLevel =
            Mathf.Clamp(galaxyLevel, 1, 10);

        int enemyLevel =
            Mathf.Clamp(enemyConfig.Level, 1, 10);

        return enemyLevel <= normalizedLevel &&
               enemyLevel >= Mathf.Max(1, normalizedLevel - 2);
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

        if (npc == null)
            return false;

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

            intervalSeconds = GetPopulationSpawnIntervalSeconds(
                rule.GetSpawnIntervalSeconds(GetCurrentGalaxyLevel()));

            return true;
        }

        if (!npc.IsEnemy)
            return false;

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(systemId);

        if (starSystem != null &&
            IsEnemySystem(starSystem) &&
            starSystem.EnemySpawnRuleSet != null)
        {
            EnemyFaction2A? faction =
                ResolveEnemyFactionForSystem(starSystem);

            if (faction.HasValue)
            {
                EnemySpawnRuleConfig enemyPopulationRule =
                    starSystem.EnemySpawnRuleSet.GetRuleForFaction(
                        faction.Value);

                if (enemyPopulationRule != null &&
                    enemyPopulationRule.Id == npc.SpawnRuleId)
                {
                    timerRuleId = BuildEnemyTimerKey(
                        enemyPopulationRule,
                        _configService.GetEnemyConfigById(npc.ConfigId));

                    intervalSeconds = GetPopulationSpawnIntervalSeconds(
                        enemyPopulationRule.GetSpawnIntervalSeconds(
                            GetCurrentGalaxyLevel()));

                    return true;
                }
            }
        }

        SystemPopulationRule populationRule =
            GetCurrentPopulationRule(starSystem);

        if (populationRule == null ||
            populationRule.EnemyGroupSpawnRuleEntries == null)
        {
            return false;
        }

        for (int i = 0;
             i < populationRule.EnemyGroupSpawnRuleEntries.Length;
             i++)
        {
            SystemPopulationEnemyGroupRuleEntry entry =
                populationRule.EnemyGroupSpawnRuleEntries[i];

            if (entry == null || !entry.IsValid())
                continue;

            EnemyGroupSpawnRuleConfig rule =
                entry.EnemyGroupSpawnRule;

            if (rule == null)
                continue;

            if (rule.Id != npc.SpawnRuleId)
                continue;

            timerRuleId = rule.Id;
            intervalSeconds = GetPopulationSpawnIntervalSeconds(
                rule.GetSpawnIntervalSeconds(GetCurrentGalaxyLevel()));

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

    public void EnsureMinimumAlliesInSystem(StarSystemConfig starSystem)
    {
        if (starSystem == null)
            return;

        if (StopAutomaticAllySpawns)
            return;

        if (IsEnemySystem(starSystem))
            return;

        if (GetCurrentPopulationRule(starSystem) == null)
            return;

        if (_npcRuntimeService.GetAliveEnemyGroupsInSystem(starSystem.Id).Count > 0)
            return;

        TickAllies(
            starSystem,
            0.01f);
    }

    public bool DebugSpawnEnemyInCurrentSystem()
    {
        StarSystemConfig starSystem =
            GetCurrentPlayerStarSystem();

        if (starSystem == null)
        {
            Debug.LogWarning(
                "[SystemPopulationService] Debug enemy spawn failed. " +
                "Current player StarSystemConfig was not resolved.");

            return false;
        }

        SystemPopulationRule populationRule =
            GetCurrentPopulationRule(starSystem);

        if (populationRule == null ||
            populationRule.EnemyGroupSpawnRuleEntries == null ||
            populationRule.EnemyGroupSpawnRuleEntries.Length == 0)
        {
            Debug.LogWarning(
                "[SystemPopulationService] Debug enemy spawn failed. " +
                "System has no enemy spawn rules. System: " +
                starSystem.Id);

            return false;
        }

        int currentGalaxyLevel =
            GetCurrentGalaxyLevel();

        for (int ruleIndex = 0;
             ruleIndex < populationRule.EnemyGroupSpawnRuleEntries.Length;
             ruleIndex++)
        {
            SystemPopulationEnemyGroupRuleEntry ruleEntry =
                populationRule.EnemyGroupSpawnRuleEntries[ruleIndex];

            if (ruleEntry == null || !ruleEntry.IsValid())
                continue;

            EnemyGroupSpawnRuleConfig rule =
                ruleEntry.EnemyGroupSpawnRule;

            if (rule == null)
                continue;

            IReadOnlyList<EnemyGroupEntryConfig> enemies =
                rule.PickEnemiesForGalaxyLevel(currentGalaxyLevel);

            if (enemies == null || enemies.Count == 0)
                continue;

            for (int enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
            {
                EnemyGroupEntryConfig entry =
                    enemies[enemyIndex];

                if (entry == null ||
                    !entry.IsValid() ||
                    entry.EnemyConfig == null)
                {
                    continue;
                }

                string groupRuntimeId =
                    Guid.NewGuid().ToString("N");

                Vector3 position =
                    BuildEnemySpawnPosition(starSystem);

                SystemNpcRuntimeState enemy =
                    SystemNpcRuntimeFactory.CreateEnemy(
                        entry.EnemyConfig,
                        starSystem.Id,
                        starSystem.Id,
                        position,
                        rule.Id,
                        groupRuntimeId);

                ApplyInitialFacingToSun(enemy, starSystem);

                enemy.CanChangeLocationOnRestore = false;

                _npcRuntimeService.AddNpc(enemy);

                Debug.Log(
                    "[SystemPopulationService] Debug single enemy spawned. " +
                    "System: " + starSystem.Id +
                    ", Rule: " + rule.Id +
                    ", Config: " + entry.EnemyConfig.Id +
                    ", GroupRuntimeId: " + groupRuntimeId +
                    ", GalaxyLevel: " + currentGalaxyLevel);

                return true;
            }
        }

        Debug.LogWarning(
            "[SystemPopulationService] Debug enemy spawn failed. " +
            "No eligible enemy config for system: " +
            starSystem.Id +
            ", GalaxyLevel: " +
            currentGalaxyLevel);

        return false;
    }

    public bool DebugSpawnAllyInOtherSystem(
    AllyRole2A role)
    {
        if (!TryPickOtherSystemAllySpawn(
                role,
                out StarSystemConfig starSystem,
                out AllySpawnRuleConfig rule,
                out AllyConfig allyConfig))
        {
            Debug.LogWarning(
                "[SystemPopulationService] Debug other-system ally spawn failed. " +
                "No eligible other system ally. Role: " +
                role +
                ", GalaxyLevel: " +
                GetCurrentGalaxyLevel());

            return false;
        }

        CreateAlly(
            starSystem,
            rule,
            allyConfig);

        Debug.Log(
            "[SystemPopulationService] Debug other-system ally spawned. " +
            "System: " +
            starSystem.Id +
            ", Rule: " +
            rule.Id +
            ", Role: " +
            role +
            ", GalaxyLevel: " +
            GetCurrentGalaxyLevel());

        return true;
    }

    private bool TryPickOtherSystemAllySpawn(
    AllyRole2A role,
    out StarSystemConfig selectedSystem,
    out AllySpawnRuleConfig selectedRule,
    out AllyConfig selectedAllyConfig)
    {
        selectedSystem = null;
        selectedRule = null;
        selectedAllyConfig = null;

        IReadOnlyList<StarSystemConfig> systems =
            _configService.GetAllStarSystems();

        if (systems == null || systems.Count == 0)
            return false;

        string currentSystemId =
            _gameSessionService != null &&
            _gameSessionService.State != null &&
            _gameSessionService.State.Player != null
                ? _gameSessionService.State.Player.CurrentSystemId
                : string.Empty;

        int currentGalaxyLevel =
            GetCurrentGalaxyLevel();

        List<(StarSystemConfig System, AllySpawnRuleConfig Rule, AllyConfig Ally)> candidates =
            new();

        for (int systemIndex = 0; systemIndex < systems.Count; systemIndex++)
        {
            StarSystemConfig system =
                systems[systemIndex];

            if (system == null)
                continue;

            if (!string.IsNullOrWhiteSpace(currentSystemId) &&
                system.Id == currentSystemId)
                continue;

            SystemPopulationRule populationRule =
                GetCurrentPopulationRule(system);

            if (populationRule == null ||
                populationRule.AllySpawnRules == null ||
                populationRule.AllySpawnRules.Length == 0)
            {
                continue;
            }

            for (int ruleIndex = 0;
                 ruleIndex < populationRule.AllySpawnRules.Length;
                 ruleIndex++)
            {
                AllySpawnRuleConfig rule =
                    populationRule.AllySpawnRules[ruleIndex];

                if (rule == null)
                    continue;

                IReadOnlyList<AllyGroupEntryConfig> allies =
                    rule.GetAlliesForGalaxyLevel(currentGalaxyLevel);

                if (allies == null)
                    continue;

                for (int allyIndex = 0; allyIndex < allies.Count; allyIndex++)
                {
                    AllyGroupEntryConfig entry =
                        allies[allyIndex];

                    if (entry == null || !entry.IsValid())
                        continue;

                    AllyConfig allyConfig =
                        entry.AllyConfig;

                    if (allyConfig == null)
                        continue;

                    if (allyConfig.Role != role)
                        continue;

                    if (allyConfig.Level != currentGalaxyLevel)
                        continue;

                    candidates.Add((system, rule, allyConfig));
                }
            }
        }

        if (candidates.Count == 0)
            return false;

        var selected =
            candidates[UnityEngine.Random.Range(0, candidates.Count)];

        selectedSystem = selected.System;
        selectedRule = selected.Rule;
        selectedAllyConfig = selected.Ally;

        return selectedSystem != null &&
               selectedRule != null &&
               selectedAllyConfig != null;
    }

    private void LogAllyPopulationDecision(
    StarSystemConfig starSystem,
    string reason,
    string details = null)
    {
        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsDebugLogEnabled(DebugLogChannel.Population))
        {
            return;
        }

        string systemId =
            starSystem != null
                ? starSystem.Id
                : string.Empty;

        if (string.IsNullOrWhiteSpace(systemId))
            systemId = "null";

        string key =
            systemId + "|" + reason + "|" + details;

        float now =
            Time.unscaledTime;

        if (_lastAllyPopulationDecisionLogBySystem.TryGetValue(
                key,
                out float lastLogTime) &&
            now - lastLogTime < 2f)
        {
            return;
        }

        _lastAllyPopulationDecisionLogBySystem[key] = now;

        Bootstrapper.Instance.LogDebug(
            DebugLogChannel.Population,
            "[AllyPopulation] " +
            "Reason=" + reason +
            ", System=" + systemId +
            (string.IsNullOrWhiteSpace(details)
                ? string.Empty
                : ", " + details));
    }

    private float GetPopulationSpawnIntervalSeconds(
    float configuredIntervalSeconds)
    {
        if (OverrideAutomaticSpawnInterval)
            return DebugAutomaticSpawnIntervalSeconds;

        return Mathf.Max(0f, configuredIntervalSeconds);
    }
}
