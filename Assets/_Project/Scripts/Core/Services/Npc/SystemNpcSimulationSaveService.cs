using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SystemNpcSimulationSaveService : CustomService, ISystemNpcSimulationSaveService
{
    private const int MinRestoredStayDays = 1;
    private const int MaxRestoredStayDays = 3;

    private readonly ISystemNpcRuntimeService _npcRuntimeService;
    private readonly ISystemNpcPopulationService _populationService;
    private readonly IConfigService _configService;
    private readonly IOrbitalMotionService _orbitalMotionService;

    public SystemNpcSimulationSaveService()
    {
        _debugStop = true;
        _npcRuntimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _populationService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcPopulationService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _orbitalMotionService = Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();
    }

    public SystemNpcSimulationSaveData Capture()
    {
        var saveData = new SystemNpcSimulationSaveData();
        var aggregates = new Dictionary<string, NpcPopulationCaptureAggregate>();

        int runtimeNpcs = 0;
        int aliveNpcs = 0;
        int skippedDeadNpcs = 0;
        int skippedInvalidNpcs = 0;

        if (_npcRuntimeService != null &&
            _npcRuntimeService.Npcs != null)
        {
            foreach (SystemNpcRuntimeState npc in _npcRuntimeService.Npcs)
            {
                runtimeNpcs++;

                if (npc == null)
                {
                    skippedInvalidNpcs++;
                    continue;
                }

                if (!npc.IsAlive ||
                    npc.LifeState != SystemNpcLifeState.Alive)
                {
                    skippedDeadNpcs++;
                    continue;
                }

                string systemId =
                    !string.IsNullOrWhiteSpace(npc.CurrentSystemId)
                        ? npc.CurrentSystemId
                        : npc.OriginSystemId;

                if (string.IsNullOrWhiteSpace(systemId) ||
                    string.IsNullOrWhiteSpace(npc.ConfigId))
                {
                    skippedInvalidNpcs++;
                    continue;
                }

                aliveNpcs++;
                AddNpcToSnapshotAggregate(aggregates, npc, systemId);
            }
        }

        foreach (NpcPopulationCaptureAggregate aggregate in aggregates.Values)
        {
            saveData.PopulationEntries.Add(aggregate.ToSaveData());
        }

        CopyPopulationTimers(saveData);

        int snapshotNpcCount =
            CountSnapshotNpcs(saveData.PopulationEntries);

        Debug.Log(
            "[NPC_COMPACT_SAVE_CAPTURE]" +
            " RuntimeNpcs=" + runtimeNpcs +
            " | AliveNpcs=" + aliveNpcs +
            " | SnapshotEntries=" + saveData.PopulationEntries.Count +
            " | SnapshotNpcCount=" + snapshotNpcCount +
            " | LegacyNpcsWritten=0" +
            " | SkippedDeadNpcs=" + skippedDeadNpcs +
            " | SkippedInvalidNpcs=" + skippedInvalidNpcs +
            " | Timers=" + saveData.PopulationTimers.Count);

        return saveData;
    }

    public void Restore(SystemNpcSimulationSaveData saveData)
    {
        _npcRuntimeService.ClearAll();
        _populationService.ClearRuntimeState();

        if (saveData == null)
        {
            Debug.Log("[NPC_COMPACT_SAVE_RESTORE] Result=Skipped | Reason=SaveDataNull");
            return;
        }

        RestorePopulationTimers(saveData);

        IReadOnlyList<SystemNpcPopulationSnapshotEntrySaveData> entries =
            ResolveRestoreEntries(saveData, out string source);

        int requestedNpcs = CountSnapshotNpcs(entries);
        int restoredNpcs = 0;
        int restoredAllies = 0;
        int restoredEnemies = 0;
        int restoredPirates = 0;
        int skippedMissingSystem = 0;
        int skippedMissingPlanet = 0;
        int skippedMissingConfig = 0;
        int skippedInvalidEntry = 0;

        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            SystemNpcPopulationSnapshotEntrySaveData entry =
                entries[entryIndex];

            if (entry == null ||
                entry.Count <= 0 ||
                string.IsNullOrWhiteSpace(entry.SystemId) ||
                string.IsNullOrWhiteSpace(entry.ConfigId))
            {
                skippedInvalidEntry++;
                continue;
            }

            StarSystemConfig starSystem =
                _configService.GetStarSystemConfigById(entry.SystemId);

            if (starSystem == null)
            {
                skippedMissingSystem += Mathf.Max(1, entry.Count);
                continue;
            }

            PlanetConfig planet =
                PickRestoredNpcPlanet(starSystem);

            if (planet == null)
            {
                skippedMissingPlanet += Mathf.Max(1, entry.Count);
                continue;
            }

            int groupCount =
                Mathf.Clamp(
                    entry.GroupCount > 0 ? entry.GroupCount : entry.Count,
                    1,
                    Mathf.Max(1, entry.Count));

            string[] restoredGroupIds =
                BuildRestoredGroupIds(entry.NpcType, groupCount);

            for (int countIndex = 0; countIndex < entry.Count; countIndex++)
            {
                SystemNpcRuntimeState npc =
                    CreateRestoredNpcOnPlanet(
                        entry,
                        starSystem,
                        planet,
                        restoredGroupIds,
                        countIndex);

                if (npc == null)
                {
                    skippedMissingConfig++;
                    continue;
                }

                _npcRuntimeService.AddNpc(npc);
                restoredNpcs++;

                if (npc.IsAlly)
                    restoredAllies++;
                else if (npc.IsEnemy)
                    restoredEnemies++;
                else if (npc.IsPirate)
                    restoredPirates++;
            }
        }

        Debug.Log(
            "[NPC_COMPACT_SAVE_RESTORE]" +
            " Source=" + source +
            " | Entries=" + entries.Count +
            " | RequestedNpcs=" + requestedNpcs +
            " | RestoredNpcs=" + restoredNpcs +
            " | RestoredAllies=" + restoredAllies +
            " | RestoredEnemies=" + restoredEnemies +
            " | RestoredPirates=" + restoredPirates +
            " | SkippedInvalidEntry=" + skippedInvalidEntry +
            " | SkippedMissingSystem=" + skippedMissingSystem +
            " | SkippedMissingPlanet=" + skippedMissingPlanet +
            " | SkippedMissingConfig=" + skippedMissingConfig +
            " | Timers=" + (saveData.PopulationTimers != null ? saveData.PopulationTimers.Count : 0));
    }

    private void AddNpcToSnapshotAggregate(
        Dictionary<string, NpcPopulationCaptureAggregate> aggregates,
        SystemNpcRuntimeState npc,
        string systemId)
    {
        string key =
            BuildSnapshotKey(
                systemId,
                npc.NpcType,
                npc.ConfigId,
                npc.SpawnRuleId,
                npc.AllyRole);

        if (!aggregates.TryGetValue(key, out NpcPopulationCaptureAggregate aggregate))
        {
            aggregate = new NpcPopulationCaptureAggregate
            {
                SystemId = systemId,
                NpcType = npc.NpcType,
                ConfigId = npc.ConfigId,
                SpawnRuleId = npc.SpawnRuleId,
                AllyRole = npc.AllyRole
            };

            aggregates.Add(key, aggregate);
        }

        aggregate.Count++;

        if (npc.IsEnemy &&
            !string.IsNullOrWhiteSpace(npc.GroupRuntimeId))
        {
            aggregate.GroupRuntimeIds.Add(npc.GroupRuntimeId);
        }
    }

    private IReadOnlyList<SystemNpcPopulationSnapshotEntrySaveData> ResolveRestoreEntries(
        SystemNpcSimulationSaveData saveData,
        out string source)
    {
        if (saveData.PopulationEntries != null &&
            saveData.PopulationEntries.Count > 0)
        {
            source = "Compact";
            return saveData.PopulationEntries;
        }

        source = "LegacyNpcs";
        return BuildSnapshotEntriesFromLegacyNpcs(saveData.Npcs);
    }

    private List<SystemNpcPopulationSnapshotEntrySaveData> BuildSnapshotEntriesFromLegacyNpcs(
        List<SystemNpcSaveData> legacyNpcs)
    {
        var aggregates = new Dictionary<string, NpcPopulationCaptureAggregate>();

        if (legacyNpcs == null)
            return new List<SystemNpcPopulationSnapshotEntrySaveData>();

        foreach (SystemNpcSaveData npc in legacyNpcs)
        {
            if (npc == null)
                continue;

            if (!npc.IsAlive ||
                npc.LifeState != SystemNpcLifeState.Alive)
            {
                continue;
            }

            string systemId =
                !string.IsNullOrWhiteSpace(npc.CurrentSystemId)
                    ? npc.CurrentSystemId
                    : npc.OriginSystemId;

            if (string.IsNullOrWhiteSpace(systemId) ||
                string.IsNullOrWhiteSpace(npc.ConfigId))
            {
                continue;
            }

            string key =
                BuildSnapshotKey(
                    systemId,
                    npc.NpcType,
                    npc.ConfigId,
                    npc.SpawnRuleId,
                    npc.AllyRole);

            if (!aggregates.TryGetValue(key, out NpcPopulationCaptureAggregate aggregate))
            {
                aggregate = new NpcPopulationCaptureAggregate
                {
                    SystemId = systemId,
                    NpcType = npc.NpcType,
                    ConfigId = npc.ConfigId,
                    SpawnRuleId = npc.SpawnRuleId,
                    AllyRole = npc.AllyRole
                };

                aggregates.Add(key, aggregate);
            }

            aggregate.Count++;

            if (npc.NpcType == SystemNpcType.Enemy &&
                !string.IsNullOrWhiteSpace(npc.GroupRuntimeId))
            {
                aggregate.GroupRuntimeIds.Add(npc.GroupRuntimeId);
            }
        }

        var entries = new List<SystemNpcPopulationSnapshotEntrySaveData>();

        foreach (NpcPopulationCaptureAggregate aggregate in aggregates.Values)
            entries.Add(aggregate.ToSaveData());

        return entries;
    }

    private SystemNpcRuntimeState CreateRestoredNpcOnPlanet(
        SystemNpcPopulationSnapshotEntrySaveData entry,
        StarSystemConfig starSystem,
        PlanetConfig planet,
        string[] restoredGroupIds,
        int countIndex)
    {
        Vector3 position =
            ResolveRestoredPlanetPosition(starSystem, planet);

        if (entry.NpcType == SystemNpcType.Ally)
        {
            AllyConfig config =
                _configService.GetAllyConfigById(entry.ConfigId);

            if (config == null)
                return null;

            SystemNpcRuntimeState ally =
                SystemNpcRuntimeFactory.CreateAlly(
                    config,
                    starSystem.Id,
                    starSystem.Id,
                    planet.Id,
                    position,
                    entry.SpawnRuleId);

            ForceNpcOnPlanetAfterCompactRestore(
                ally,
                starSystem,
                planet,
                position);

            return ally;
        }

        if (entry.NpcType == SystemNpcType.Enemy)
        {
            EnemyConfig config =
                _configService.GetEnemyConfigById(entry.ConfigId);

            if (config == null)
                return null;

            string groupRuntimeId =
                ResolveRestoredGroupId(
                    restoredGroupIds,
                    countIndex);

            SystemNpcRuntimeState enemy =
                SystemNpcRuntimeFactory.CreateEnemy(
                    config,
                    starSystem.Id,
                    starSystem.Id,
                    position,
                    entry.SpawnRuleId,
                    groupRuntimeId);

            ForceNpcOnPlanetAfterCompactRestore(
                enemy,
                starSystem,
                planet,
                position);

            return enemy;
        }

        if (entry.NpcType == SystemNpcType.Pirate)
        {
            PirateConfig config =
                _configService.GetPirateConfigById(entry.ConfigId);

            if (config == null)
                return null;

            string groupRuntimeId =
                ResolveRestoredGroupId(
                    restoredGroupIds,
                    countIndex);

            SystemNpcRuntimeState pirate =
                SystemNpcRuntimeFactory.CreatePirate(
                    config,
                    starSystem.Id,
                    starSystem.Id,
                    position,
                    entry.SpawnRuleId,
                    groupRuntimeId);

            ForceNpcOnPlanetAfterCompactRestore(
                pirate,
                starSystem,
                planet,
                position);

            return pirate;
        }

        return null;
    }

    private void ForceNpcOnPlanetAfterCompactRestore(
        SystemNpcRuntimeState npc,
        StarSystemConfig starSystem,
        PlanetConfig planet,
        Vector3 position)
    {
        if (npc == null)
            return;

        int currentTick =
            GetCurrentQuantTick();

        int stayDays =
            UnityEngine.Random.Range(
                MinRestoredStayDays,
                MaxRestoredStayDays + 1);

        npc.OriginSystemId = starSystem.Id;
        npc.CurrentSystemId = starSystem.Id;
        npc.TargetSystemId = null;

        npc.CurrentPlanetId = planet.Id;
        npc.TargetPlanetId = null;
        npc.IsOnPlanet = true;

        npc.CurrentPosition = position;
        npc.StartPosition = position;
        npc.TargetPosition = position;
        npc.CurrentMovementTargetPosition = position;
        npc.TickMovementTargetPosition = position;
        npc.FacingDirection = Vector3.up;
        npc.TickMovementDirection = Vector3.up;
        npc.TickMovementDirectionTick = -1;
        npc.TickMovementArrived = true;

        npc.TravelState = SystemNpcTravelState.OnPlanet;
        npc.TravelProgress01 = 1f;
        npc.TravelStartTick = 0;
        npc.TravelEndTick = 0;
        npc.IsWaitingForInitialRouteBuild = false;
        npc.ReleaseFromPlanetAfterInitialRouteBuild = false;
        npc.InitialRouteBuildPlanetId = null;

        npc.PrevBehavior = SystemNpcBehaviorType.None;
        npc.CurrentBehavior = SystemNpcBehaviorType.StayOnPlanetForDays;
        npc.HasActiveBehavior = true;
        npc.BehaviorStartedTick = currentTick;
        npc.DaysToStayOnPlanet = stayDays;
        npc.DaysStayedOnPlanet = 0;
        npc.BehaviorEndsTick = currentTick + stayDays;
        npc.BehaviorTargetRuntimeNpcId = null;

        npc.CombatState = SystemNpcCombatState.None;
        npc.CurrentTargetRuntimeNpcId = null;
        npc.IsFighting = false;

        npc.LifeState = SystemNpcLifeState.Alive;
        npc.IsAlive = true;
        npc.DestroyedAtTick = 0;
        npc.NextRespawnTick = 0;
    }

    private PlanetConfig PickRestoredNpcPlanet(
        StarSystemConfig starSystem)
    {
        if (starSystem == null ||
            starSystem.PlanetRefs == null ||
            starSystem.PlanetRefs.Length == 0)
        {
            return null;
        }

        var inhabitedPlanets = new List<PlanetConfig>();

        for (int i = 0; i < starSystem.PlanetRefs.Length; i++)
        {
            PlanetConfig planet =
                starSystem.PlanetRefs[i];

            if (planet != null &&
                planet.IsInhabited)
            {
                inhabitedPlanets.Add(planet);
            }
        }

        if (inhabitedPlanets.Count > 0)
        {
            return inhabitedPlanets[
                UnityEngine.Random.Range(0, inhabitedPlanets.Count)];
        }

        var anyPlanets = new List<PlanetConfig>();

        for (int i = 0; i < starSystem.PlanetRefs.Length; i++)
        {
            if (starSystem.PlanetRefs[i] != null)
                anyPlanets.Add(starSystem.PlanetRefs[i]);
        }

        if (anyPlanets.Count == 0)
            return null;

        return anyPlanets[
            UnityEngine.Random.Range(0, anyPlanets.Count)];
    }

    private Vector3 ResolveRestoredPlanetPosition(
        StarSystemConfig starSystem,
        PlanetConfig planet)
    {
        if (planet != null &&
            planet.PlanetOrbit != null &&
            _orbitalMotionService != null)
        {
            Vector3 position =
                _orbitalMotionService.GetPlanetCurrentPosition(
                    planet.PlanetOrbit);

            position.z = 0f;
            return position;
        }

        if (planet != null &&
            planet.PlanetOrbit != null)
        {
            float angleRadians =
                planet.PlanetOrbit.StartAngleDeg * Mathf.Deg2Rad;

            Vector3 offset =
                new Vector3(
                    Mathf.Cos(angleRadians),
                    Mathf.Sin(angleRadians),
                    0f) * planet.PlanetOrbit.OrbitRadius;

            Vector3 position =
                planet.PlanetOrbit.OrbitCenterOffset + offset;

            position.z = 0f;
            return position;
        }

        if (starSystem != null)
            return starSystem.MapPosition;

        return Vector3.zero;
    }

    private void CopyPopulationTimers(
        SystemNpcSimulationSaveData saveData)
    {
        if (_populationService == null ||
            _populationService.RuntimeState == null ||
            _populationService.RuntimeState.Timers == null)
        {
            return;
        }

        foreach (SystemPopulationRuleTimerState timer in _populationService.RuntimeState.Timers)
        {
            if (timer == null)
                continue;

            saveData.PopulationTimers.Add(new SystemPopulationRuleTimerState(
                timer.SystemId,
                timer.RuleId)
            {
                TimerSeconds = timer.TimerSeconds,
                NextSpawnTick = timer.NextSpawnTick
            });
        }
    }

    private void RestorePopulationTimers(
        SystemNpcSimulationSaveData saveData)
    {
        if (saveData.PopulationTimers == null)
            return;

        foreach (SystemPopulationRuleTimerState timer in saveData.PopulationTimers)
        {
            if (timer == null)
                continue;

            SystemPopulationRuleTimerState restoredTimer =
                _populationService.RuntimeState.GetOrCreateTimer(
                    timer.SystemId,
                    timer.RuleId);

            restoredTimer.TimerSeconds = timer.TimerSeconds;
            restoredTimer.NextSpawnTick = timer.NextSpawnTick;
        }
    }

    private static int CountSnapshotNpcs(
        IReadOnlyList<SystemNpcPopulationSnapshotEntrySaveData> entries)
    {
        if (entries == null)
            return 0;

        int count = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null)
                count += Mathf.Max(0, entries[i].Count);
        }

        return count;
    }

    private static string BuildSnapshotKey(
        string systemId,
        SystemNpcType npcType,
        string configId,
        string spawnRuleId,
        AllyRole2A allyRole)
    {
        return systemId + "|" +
               npcType + "|" +
               configId + "|" +
               spawnRuleId + "|" +
               allyRole;
    }

    private static string[] BuildRestoredGroupIds(
        SystemNpcType npcType,
        int groupCount)
    {
        if (npcType != SystemNpcType.Enemy &&
            npcType != SystemNpcType.Pirate)
        {
            return Array.Empty<string>();
        }

        int normalizedGroupCount =
            Mathf.Max(1, groupCount);

        string[] groupIds =
            new string[normalizedGroupCount];

        for (int i = 0; i < groupIds.Length; i++)
            groupIds[i] = Guid.NewGuid().ToString("N");

        return groupIds;
    }

    private static string ResolveRestoredGroupId(
        string[] restoredGroupIds,
        int countIndex)
    {
        if (restoredGroupIds == null ||
            restoredGroupIds.Length == 0)
        {
            return null;
        }

        int index =
            Mathf.Abs(countIndex) % restoredGroupIds.Length;

        return restoredGroupIds[index];
    }

    private int GetCurrentQuantTick()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return 1;
        }

        if (Bootstrapper.Instance.ServiceRegistry.TryGet<IGameTimeService>(
                out IGameTimeService gameTimeService) &&
            gameTimeService != null)
        {
            return Mathf.Max(1, gameTimeService.CurrentQuantTick);
        }

        return 1;
    }

    private sealed class NpcPopulationCaptureAggregate
    {
        public string SystemId;
        public SystemNpcType NpcType;
        public string ConfigId;
        public string SpawnRuleId;
        public AllyRole2A AllyRole;
        public int Count;
        public HashSet<string> GroupRuntimeIds = new();

        public SystemNpcPopulationSnapshotEntrySaveData ToSaveData()
        {
            return new SystemNpcPopulationSnapshotEntrySaveData
            {
                SystemId = SystemId,
                NpcType = NpcType,
                ConfigId = ConfigId,
                SpawnRuleId = SpawnRuleId,
                AllyRole = AllyRole,
                Count = Count,
                GroupCount = GroupRuntimeIds.Count
            };
        }
    }

    public SystemNpcSimulationCaptureSession BeginIncrementalCapture()
    {
        var session = new SystemNpcSimulationCaptureSession();

        CopyPopulationTimers(session.SaveData);

        return session;
    }

    public bool ContinueIncrementalCapture(
        SystemNpcSimulationCaptureSession session,
        float budgetMs,
        int maxNpcItemsPerStep)
    {
        if (session == null)
            return true;

        long startedAt =
            System.Diagnostics.Stopwatch.GetTimestamp();

        float normalizedBudgetMs =
            Mathf.Max(0.25f, budgetMs);

        int normalizedMaxItems =
            Mathf.Max(1, maxNpcItemsPerStep);

        if (!session.RuntimeScanComplete)
        {
            ContinueRuntimeNpcScan(
                session,
                normalizedBudgetMs,
                normalizedMaxItems,
                startedAt);

            if (!session.RuntimeScanComplete)
                return false;
        }

        if (!session.EntriesBuildComplete)
        {
            ContinueSnapshotEntryBuild(
                session,
                normalizedBudgetMs,
                normalizedMaxItems,
                startedAt);
        }

        return session.IsComplete;
    }

    private void ContinueRuntimeNpcScan(
        SystemNpcSimulationCaptureSession session,
        float budgetMs,
        int maxNpcItemsPerStep,
        long startedAt)
    {
        if (_npcRuntimeService == null ||
            _npcRuntimeService.Npcs == null)
        {
            session.RuntimeScanComplete = true;
            session.AggregateList =
                new List<SystemNpcPopulationCaptureAggregateData>(
                    session.Aggregates.Values);
            return;
        }

        IReadOnlyList<SystemNpcRuntimeState> npcs =
            _npcRuntimeService.Npcs;

        int processedThisStep = 0;

        while (session.NextRuntimeNpcIndex < npcs.Count)
        {
            if (processedThisStep >= maxNpcItemsPerStep)
                break;

            if (ElapsedMs(startedAt) >= budgetMs)
                break;

            SystemNpcRuntimeState npc =
                npcs[session.NextRuntimeNpcIndex];

            session.NextRuntimeNpcIndex++;
            processedThisStep++;
            session.RuntimeNpcs++;

            if (npc == null)
            {
                session.SkippedInvalidNpcs++;
                continue;
            }

            if (!npc.IsAlive ||
                npc.LifeState != SystemNpcLifeState.Alive)
            {
                session.SkippedDeadNpcs++;
                continue;
            }

            string systemId =
                !string.IsNullOrWhiteSpace(npc.CurrentSystemId)
                    ? npc.CurrentSystemId
                    : npc.OriginSystemId;

            if (string.IsNullOrWhiteSpace(systemId) ||
                string.IsNullOrWhiteSpace(npc.ConfigId))
            {
                session.SkippedInvalidNpcs++;
                continue;
            }

            session.AliveNpcs++;
            AddNpcToIncrementalAggregate(session, npc, systemId);
        }

        if (session.NextRuntimeNpcIndex >= npcs.Count)
        {
            session.RuntimeScanComplete = true;
            session.AggregateList =
                new List<SystemNpcPopulationCaptureAggregateData>(
                    session.Aggregates.Values);
        }
    }

    private void ContinueSnapshotEntryBuild(
        SystemNpcSimulationCaptureSession session,
        float budgetMs,
        int maxEntriesPerStep,
        long startedAt)
    {
        if (session.AggregateList == null)
        {
            session.AggregateList =
                new List<SystemNpcPopulationCaptureAggregateData>(
                    session.Aggregates.Values);
        }

        int processedThisStep = 0;

        while (session.NextAggregateIndex < session.AggregateList.Count)
        {
            if (processedThisStep >= maxEntriesPerStep)
                break;

            if (ElapsedMs(startedAt) >= budgetMs)
                break;

            SystemNpcPopulationCaptureAggregateData aggregate =
                session.AggregateList[session.NextAggregateIndex];

            session.NextAggregateIndex++;
            processedThisStep++;

            if (aggregate == null ||
                aggregate.Count <= 0)
            {
                continue;
            }

            SystemNpcPopulationSnapshotEntrySaveData entry =
                aggregate.ToSaveData();

            session.SaveData.PopulationEntries.Add(entry);
            session.SnapshotNpcCount += Mathf.Max(0, entry.Count);
        }

        if (session.NextAggregateIndex >= session.AggregateList.Count)
        {
            session.EntriesBuildComplete = true;

            Debug.Log(
                "[NPC_COMPACT_SAVE_CAPTURE_INCREMENTAL]" +
                " RuntimeNpcs=" + session.RuntimeNpcs +
                " | AliveNpcs=" + session.AliveNpcs +
                " | SnapshotEntries=" + session.SaveData.PopulationEntries.Count +
                " | SnapshotNpcCount=" + session.SnapshotNpcCount +
                " | LegacyNpcsWritten=0" +
                " | SkippedDeadNpcs=" + session.SkippedDeadNpcs +
                " | SkippedInvalidNpcs=" + session.SkippedInvalidNpcs +
                " | Timers=" + session.SaveData.PopulationTimers.Count);
        }
    }

    private void AddNpcToIncrementalAggregate(
        SystemNpcSimulationCaptureSession session,
        SystemNpcRuntimeState npc,
        string systemId)
    {
        string key =
            BuildSnapshotKey(
                systemId,
                npc.NpcType,
                npc.ConfigId,
                npc.SpawnRuleId,
                npc.AllyRole);

        if (!session.Aggregates.TryGetValue(
                key,
                out SystemNpcPopulationCaptureAggregateData aggregate))
        {
            aggregate = new SystemNpcPopulationCaptureAggregateData
            {
                SystemId = systemId,
                NpcType = npc.NpcType,
                ConfigId = npc.ConfigId,
                SpawnRuleId = npc.SpawnRuleId,
                AllyRole = npc.AllyRole
            };

            session.Aggregates.Add(key, aggregate);
        }

        aggregate.Count++;

        if ((npc.IsEnemy || npc.IsPirate) &&
            !string.IsNullOrWhiteSpace(npc.GroupRuntimeId))
        {
            aggregate.GroupRuntimeIds.Add(npc.GroupRuntimeId);
        }
    }

    private static double ElapsedMs(long startedAt)
    {
        long elapsedTicks =
            System.Diagnostics.Stopwatch.GetTimestamp() - startedAt;

        return elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }
}