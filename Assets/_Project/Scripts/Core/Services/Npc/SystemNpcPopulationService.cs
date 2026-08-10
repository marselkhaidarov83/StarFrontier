using System;
using System.Collections.Generic;
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

        if (GetCurrentPopulationRule(starSystem) == null)
            return;

        if (deltaTime <= 0f)
            return;

        if (!StopAutomaticAllySpawns)
            TickAllies(starSystem, deltaTime);

        if (!StopAutomaticEnemySpawns)
            TickEnemyGroups(starSystem, deltaTime);
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
                GetAllySpawnIntervalSeconds(
                    rule,
                    currentGalaxyLevel);

            for (int i = 0; i < allies.Count; i++)
            {
                AllyGroupEntryConfig entry =
                    allies[i];

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

                if (timer.TimerSeconds < spawnIntervalSeconds)
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

        int currentGalaxyLevel =
            GetCurrentGalaxyLevel();

        EnemyGroupSpawnLevelEntryConfig levelEntry =
            rule.GetEntryForGalaxyLevel(currentGalaxyLevel);

        if (levelEntry == null)
            return;

        if (levelEntry.Enemies == null)
            return;

        EnemyGroupEntryConfig selectedEntry =
            PickWeightedEnemyEntry(
                levelEntry.Enemies,
                currentGalaxyLevel);

        if (selectedEntry == null)
            return;

        string groupRuntimeId =
            Guid.NewGuid().ToString("N");

        int count =
            UnityEngine.Random.Range(
                selectedEntry.MinCount,
                selectedEntry.MaxCount + 1);

        Vector3 groupSpawnBasePosition =
            PickEnemyGroupSpawnBasePosition(starSystem);

        for (int c = 0; c < count; c++)
        {
            Vector3 position =
                BuildEnemySpawnPosition(
                    starSystem,
                    groupSpawnBasePosition);

            SystemNpcRuntimeState enemy =
                SystemNpcRuntimeFactory.CreateEnemy(
                    selectedEntry.EnemyConfig,
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
                "Config: " + selectedEntry.EnemyConfig.Id + ", " +
                "Weight: " + selectedEntry.Weight + ", " +
                "GroupRuntimeId: " + groupRuntimeId);
        }
    }

    private EnemyGroupEntryConfig PickWeightedEnemyEntry(
        IReadOnlyList<EnemyGroupEntryConfig> entries,
        int currentGalaxyLevel)
    {
        if (entries == null || entries.Count == 0)
            return null;

        List<EnemyGroupEntryConfig> validEntries =
            new List<EnemyGroupEntryConfig>();

        int totalWeight = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            EnemyGroupEntryConfig entry =
                entries[i];

            if (entry == null)
                continue;

            if (entry.EnemyConfig == null)
                continue;

            if (!entry.IsValid())
                continue;

            if (!IsEnemyConfigAllowedForGalaxyLevel(
                    entry.EnemyConfig,
                    currentGalaxyLevel))
            {
                LogCustom(
                    "[SystemPopulationService] Enemy entry skipped because " +
                    "its level does not equal the current GalaxyLevel. " +
                    "Config: " + entry.EnemyConfig.Id +
                    ", ConfigLevel: " + entry.EnemyConfig.Level +
                    ", GalaxyLevel: " + currentGalaxyLevel);
                continue;
            }

            validEntries.Add(entry);
            totalWeight += Mathf.Max(1, entry.Weight);
        }

        if (validEntries.Count == 0 || totalWeight <= 0)
            return null;

        int roll =
            UnityEngine.Random.Range(0, totalWeight);

        int cumulative = 0;

        for (int i = 0; i < validEntries.Count; i++)
        {
            EnemyGroupEntryConfig entry =
                validEntries[i];

            cumulative += Mathf.Max(1, entry.Weight);

            if (roll < cumulative)
                return entry;
        }

        return validEntries[validEntries.Count - 1];
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

    private Vector3 PickEnemyGroupSpawnBasePosition(
        StarSystemConfig starSystem)
    {
        if (starSystem != null &&
            starSystem.NpcSpawnPoints != null)
        {
            return starSystem.NpcSpawnPoints.PickEnemySpawnBasePosition();
        }

        return new Vector3(6f, 0f, 0f);
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

        return BuildTangentialEnemySpawnPosition(
            groupSpawnBasePosition,
            scatterRadius);
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

    private float GetAllySpawnIntervalSeconds(
        AllySpawnRuleConfig rule,
        int galaxyLevel)
    {
        if (OverrideAutomaticAllySpawnInterval)
            return DebugAutomaticAllySpawnIntervalSeconds;

        if (rule == null)
            return 0f;

        return rule.GetSpawnIntervalSeconds(galaxyLevel);
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

        return enemyConfig.Level == normalizedLevel;
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
            intervalSeconds = GetAllySpawnIntervalSeconds(
                rule,
                GetCurrentGalaxyLevel());
            return true;
        }

        if (!npc.IsEnemy)
            return false;

        StarSystemConfig starSystem =
            _configService.GetStarSystemConfigById(systemId);

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

            if (entry == null)
                continue;

            EnemyGroupSpawnRuleConfig rule =
                entry.EnemyGroupSpawnRule;

            if (rule == null || rule.Id != npc.SpawnRuleId)
                continue;

            timerRuleId = rule.Id;
            intervalSeconds = rule.GetSpawnIntervalSeconds(GetCurrentGalaxyLevel());
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
