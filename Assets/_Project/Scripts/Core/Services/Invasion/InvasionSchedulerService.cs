using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class InvasionSchedulerService :
    CustomService,
    IInvasionSchedulerService
{
    private const int DefaultResolveAfterTicks = 3;

    private readonly IConfigService _configService;
    private readonly IGameSessionService _gameSessionService;
    private readonly IInvasionService _invasionService;
    private readonly ISystemSecurityService _systemSecurityService;
    private readonly IEnemyFactionService _enemyFactionService;

    private float _elapsedSeconds;
    private bool _missingConfigLogged;
    private bool _missingLevelEntryLogged;
    private bool _automaticInvasionsStoppedLogged;

    public InvasionSchedulerService()
    {
        _configService =
            Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();

        _gameSessionService =
            Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();

        _invasionService =
            Bootstrapper.Instance.ServiceRegistry.Get<IInvasionService>();

        _systemSecurityService =
            Bootstrapper.Instance.ServiceRegistry.Get<ISystemSecurityService>();

        _enemyFactionService =
            Bootstrapper.Instance.ServiceRegistry.Get<IEnemyFactionService>();
    }

    public void Tick(float deltaTime, int currentQuantTick)
    {
        _invasionService.UpdateInvasions();

        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.StopAutomaticInvasions)
        {
            _elapsedSeconds = 0f;

            LogOnce(
                ref _automaticInvasionsStoppedLogged,
                "Scheduled invasion skipped: automatic invasions are stopped by DebugSettings.");

            return;
        }

        _automaticInvasionsStoppedLogged = false;

        if (deltaTime <= 0f)
            return;

        if (_gameSessionService == null ||
            !_gameSessionService.HasActiveSession ||
            _gameSessionService.State == null ||
            _gameSessionService.State.Galaxy == null)
        {
            return;
        }

        InvasionScheduleConfig scheduleConfig =
            _configService.InvasionScheduleConfig;

        if (scheduleConfig == null)
        {
            LogOnce(
                ref _missingConfigLogged,
                "Scheduled invasion skipped: InvasionScheduleConfig is not assigned.");

            return;
        }

        int galaxyLevel =
            GetCurrentGalaxyLevel();

        InvasionScheduleLevelEntry levelEntry =
            scheduleConfig.GetEntryForGalaxyLevel(galaxyLevel);

        if (levelEntry == null)
        {
            LogOnce(
                ref _missingLevelEntryLogged,
                "Scheduled invasion skipped: no level entry. GalaxyLevel=" +
                galaxyLevel);

            return;
        }

        _elapsedSeconds += deltaTime;

        float periodSeconds =
            GetInvasionPeriodSeconds(levelEntry);

        if (_elapsedSeconds < periodSeconds)
            return;

        _elapsedSeconds %= periodSeconds;

        TryCreateScheduledInvasion(
            galaxyLevel,
            levelEntry,
            currentQuantTick);
    }

    private bool TryCreateScheduledInvasion(
        int galaxyLevel,
        InvasionScheduleLevelEntry levelEntry,
        int currentQuantTick)
    {
        int maxActiveInvasions =
            GetMaxActiveInvasions(levelEntry);

        int activeInvasionCount =
            _invasionService.GetActiveInvasionCount();

        if (activeInvasionCount >= maxActiveInvasions)
        {
            Log(
                "Scheduled invasion skipped: active invasion limit reached. " +
                "GalaxyLevel=" + galaxyLevel +
                ", Active=" + activeInvasionCount +
                ", Max=" + maxActiveInvasions +
                ", DebugOverride=" + IsDebugInvasionScheduleOverrideEnabled() +
                ", Tick=" + currentQuantTick);

            return false;
        }

        if (!TryPickTargetSystem(
            maxActiveInvasions,
            galaxyLevel,
            out StarSystemConfig targetSystem))
        {
            Log(
                "Scheduled invasion skipped: no valid NPC-owned target system. " +
                "GalaxyLevel=" + galaxyLevel +
                ", Active=" + activeInvasionCount +
                ", Max=" + maxActiveInvasions +
                ", DebugOverride=" + IsDebugInvasionScheduleOverrideEnabled() +
                ", Tick=" + currentQuantTick);

            return false;
        }

        if (!TryPickSourceSystemAndFaction(
                targetSystem,
                out StarSystemConfig sourceSystem,
                out string factionId))
        {
            Log(
                "Scheduled invasion skipped: no enemy-owned source system. " +
                "Target=" + targetSystem.Id +
                ", GalaxyLevel=" + galaxyLevel +
                ", Tick=" + currentQuantTick);

            return false;
        }

        if (!TryPickInvasionRule(
                targetSystem,
                galaxyLevel,
                factionId,
                out EnemyGroupSpawnRuleConfig invasionRule))
        {
            Log(
                "Scheduled invasion skipped: no valid invasion rule. " +
                "Target=" + targetSystem.Id +
                ", Source=" + sourceSystem.Id +
                ", Faction=" + factionId +
                ", GalaxyLevel=" + galaxyLevel +
                ", Tick=" + currentQuantTick);

            return false;
        }

        bool started =
            _invasionService.TryStartInvasion(
                sourceSystem,
                targetSystem,
                invasionRule,
                factionId,
                DefaultResolveAfterTicks,
                maxActiveInvasions,
                out InvasionState invasionState);

        Log(
            "Scheduled invasion attempt completed. " +
            "Started=" + started +
            ", InvasionId=" +
            (invasionState != null ? invasionState.InvasionId : string.Empty) +
            ", Source=" + sourceSystem.Id +
            ", Target=" + targetSystem.Id +
            ", Faction=" + factionId +
            ", Rule=" + invasionRule.Id +
            ", GalaxyLevel=" + galaxyLevel +
            ", ActiveBefore=" + activeInvasionCount +
            ", Max=" + maxActiveInvasions +
            ", PeriodSeconds=" + GetInvasionPeriodSeconds(levelEntry).ToString("F2") +
            ", DebugOverride=" + IsDebugInvasionScheduleOverrideEnabled() +
            ", Tick=" + currentQuantTick);

        return started;
    }

    private float GetInvasionPeriodSeconds(
        InvasionScheduleLevelEntry levelEntry)
    {
        if (IsDebugInvasionScheduleOverrideEnabled())
        {
            return Mathf.Max(
                1f,
                Bootstrapper.Instance.DebugAutomaticInvasionPeriodSeconds);
        }

        if (levelEntry == null)
            return 1f;

        return Mathf.Max(
            1f,
            levelEntry.InvasionPeriodSeconds);
    }

    private int GetMaxActiveInvasions(
        InvasionScheduleLevelEntry levelEntry)
    {
        if (IsDebugInvasionScheduleOverrideEnabled())
        {
            return Mathf.Max(
                0,
                Bootstrapper.Instance.DebugMaxActiveInvasions);
        }

        if (levelEntry == null)
            return 0;

        return Mathf.Max(
            0,
            levelEntry.MaxActiveInvasions);
    }

    private static bool IsDebugInvasionScheduleOverrideEnabled()
    {
        return Bootstrapper.Instance != null &&
               Bootstrapper.Instance.OverrideAutomaticInvasionSchedule;
    }

    private bool TryPickTargetSystem(
        int maxActiveInvasions,
        int galaxyLevel,
        out StarSystemConfig targetSystem)
    {
        targetSystem = null;

        IReadOnlyList<StarSystemConfig> systems =
            _configService.GetAllStarSystems();

        if (systems == null || systems.Count == 0)
            return false;

        List<StarSystemConfig> candidates = new();

        for (int i = 0; i < systems.Count; i++)
        {
            StarSystemConfig system =
                systems[i];

            if (system == null ||
                string.IsNullOrWhiteSpace(system.Id))
            {
                continue;
            }

            if (!_invasionService.CanStartInvasion(
                    system.Id,
                    maxActiveInvasions))
            {
                continue;
            }

            if (!HasAnyValidInvasionRuleForEnemySource(
                    system,
                    galaxyLevel))
            {
                continue;
            }

            candidates.Add(system);
        }

        if (candidates.Count == 0)
            return false;

        targetSystem =
            candidates[Random.Range(0, candidates.Count)];

        return targetSystem != null;
    }

    private bool TryPickSourceSystemAndFaction(
        StarSystemConfig targetSystem,
        out StarSystemConfig sourceSystem,
        out string factionId)
    {
        sourceSystem = null;
        factionId = string.Empty;

        List<InvasionSourceCandidate> linkedCandidates = new();
        List<InvasionSourceCandidate> fallbackCandidates = new();

        IReadOnlyList<EnemyFactionState> factions =
            _enemyFactionService.GetFactions();

        if (factions == null || factions.Count == 0)
            return false;

        for (int factionIndex = 0; factionIndex < factions.Count; factionIndex++)
        {
            EnemyFactionState faction =
                factions[factionIndex];

            if (faction == null ||
                string.IsNullOrWhiteSpace(faction.FactionId))
            {
                continue;
            }

            faction.EnsureWarTerritoryLists();

            for (int systemIndex = 0;
                 systemIndex < faction.OwnedSystemIds.Count;
                 systemIndex++)
            {
                string ownedSystemId =
                    faction.OwnedSystemIds[systemIndex];

                if (string.IsNullOrWhiteSpace(ownedSystemId))
                    continue;

                if (!_configService.TryGetStarSystem(
                        ownedSystemId,
                        out StarSystemConfig ownedSystem))
                {
                    continue;
                }

                if (!IsCapturedSystem(ownedSystem.Id))
                    continue;

                InvasionSourceCandidate candidate =
                    new InvasionSourceCandidate
                    {
                        System = ownedSystem,
                        FactionId = faction.FactionId
                    };

                fallbackCandidates.Add(candidate);

                if (IsLinkedToTarget(ownedSystem, targetSystem))
                    linkedCandidates.Add(candidate);
            }
        }

        List<InvasionSourceCandidate> candidates =
            linkedCandidates.Count > 0
                ? linkedCandidates
                : fallbackCandidates;

        if (candidates.Count == 0)
            return false;

        InvasionSourceCandidate selected =
            candidates[Random.Range(0, candidates.Count)];

        sourceSystem = selected.System;
        factionId = selected.FactionId;

        return sourceSystem != null &&
               !string.IsNullOrWhiteSpace(factionId);
    }

    private bool TryPickInvasionRule(
        StarSystemConfig targetSystem,
        int galaxyLevel,
        string factionId,
        out EnemyGroupSpawnRuleConfig selectedRule)
    {
        selectedRule = null;

        if (targetSystem == null ||
            targetSystem.SystemPopulationRule == null ||
            targetSystem.SystemPopulationRule.EnemyGroupSpawnRuleEntries == null)
        {
            return false;
        }

        if (!TryConvertFactionId(
                factionId,
                out EnemyFaction2A enemyFaction))
        {
            return false;
        }

        List<SystemPopulationEnemyGroupRuleEntry> candidates = new();
        int totalWeight = 0;

        SystemPopulationEnemyGroupRuleEntry[] entries =
            targetSystem.SystemPopulationRule.EnemyGroupSpawnRuleEntries;

        for (int i = 0; i < entries.Length; i++)
        {
            SystemPopulationEnemyGroupRuleEntry entry =
                entries[i];

            if (entry == null || !entry.IsValid())
                continue;

            EnemyGroupSpawnRuleConfig rule =
                entry.EnemyGroupSpawnRule;

            if (rule == null)
                continue;

            IReadOnlyList<EnemyGroupEntryConfig> enemies =
                rule.PickEnemiesForGalaxyLevel(
                    galaxyLevel,
                    enemyFaction);

            if (enemies == null || enemies.Count == 0)
                continue;

            candidates.Add(entry);
            totalWeight += Mathf.Max(1, entry.Weight);
        }

        if (candidates.Count == 0 || totalWeight <= 0)
            return false;

        int roll =
            Random.Range(0, totalWeight);

        int accumulatedWeight = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            SystemPopulationEnemyGroupRuleEntry candidate =
                candidates[i];

            accumulatedWeight += Mathf.Max(1, candidate.Weight);

            if (roll >= accumulatedWeight)
                continue;

            selectedRule = candidate.EnemyGroupSpawnRule;
            return selectedRule != null;
        }

        selectedRule =
            candidates[candidates.Count - 1].EnemyGroupSpawnRule;

        return selectedRule != null;
    }

    private bool IsCapturedSystem(string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId))
            return false;

        if (!_systemSecurityService.TryGetSystemStatus(
                systemId,
                out StarSystemStatus status))
        {
            return false;
        }

        return status == StarSystemStatus.Captured;
    }

    private static bool IsLinkedToTarget(
        StarSystemConfig sourceSystem,
        StarSystemConfig targetSystem)
    {
        if (sourceSystem == null ||
            targetSystem == null ||
            sourceSystem.LinkedSystems == null)
        {
            return false;
        }

        for (int i = 0; i < sourceSystem.LinkedSystems.Length; i++)
        {
            StarSystemLink link =
                sourceSystem.LinkedSystems[i];

            if (link == null ||
                link.LinkedSystem == null)
            {
                continue;
            }

            if (link.LinkedSystem.Id == targetSystem.Id)
                return true;
        }

        return false;
    }

    private int GetCurrentGalaxyLevel()
    {
        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.OverrideNpcGalaxyLevel)
        {
            return Bootstrapper.Instance.DebugNpcGalaxyLevel;
        }

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

    private static bool TryConvertFactionId(
        string factionId,
        out EnemyFaction2A enemyFaction)
    {
        enemyFaction = EnemyFaction2A.AI;

        string normalized =
            string.IsNullOrWhiteSpace(factionId)
                ? string.Empty
                : factionId.Trim().ToLowerInvariant();

        switch (normalized)
        {
            case "ai":
                enemyFaction = EnemyFaction2A.AI;
                return true;

            case "ancients":
                enemyFaction = EnemyFaction2A.Ancients;
                return true;

            case "infected":
                enemyFaction = EnemyFaction2A.Infected;
                return true;

            default:
                return false;
        }
    }

    private void LogOnce(ref bool flag, string message)
    {
        if (flag)
            return;

        flag = true;
        Log(message);
    }

    private void Log(string message)
    {
        if (Bootstrapper.Instance == null)
            return;

        if (!Bootstrapper.Instance.IsDebugLogEnabled(DebugLogChannel.Population))
            return;

        Bootstrapper.Instance.LogDebug(
            DebugLogChannel.Population,
            "[InvasionSchedulerService] " + message);
    }

    private sealed class InvasionSourceCandidate
    {
        public StarSystemConfig System;
        public string FactionId;
    }

    private bool HasAnyValidInvasionRuleForEnemySource(
    StarSystemConfig targetSystem,
    int galaxyLevel)
    {
        if (targetSystem == null)
            return false;

        if (targetSystem.SystemPopulationRule == null ||
            targetSystem.SystemPopulationRule.EnemyGroupSpawnRuleEntries == null)
        {
            return false;
        }

        SystemPopulationEnemyGroupRuleEntry[] entries =
            targetSystem.SystemPopulationRule.EnemyGroupSpawnRuleEntries;

        if (entries.Length == 0)
            return false;

        IReadOnlyList<EnemyFactionState> factions =
            _enemyFactionService.GetFactions();

        if (factions == null || factions.Count == 0)
            return false;

        for (int factionIndex = 0; factionIndex < factions.Count; factionIndex++)
        {
            EnemyFactionState faction =
                factions[factionIndex];

            if (faction == null ||
                string.IsNullOrWhiteSpace(faction.FactionId))
            {
                continue;
            }

            if (!TryConvertFactionId(
                    faction.FactionId,
                    out EnemyFaction2A enemyFaction))
            {
                continue;
            }

            if (!HasEnemyOwnedSourceForFaction(
                    faction,
                    targetSystem))
            {
                continue;
            }

            for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
            {
                SystemPopulationEnemyGroupRuleEntry entry =
                    entries[entryIndex];

                if (entry == null || !entry.IsValid())
                    continue;

                EnemyGroupSpawnRuleConfig rule =
                    entry.EnemyGroupSpawnRule;

                if (rule == null)
                    continue;

                IReadOnlyList<EnemyGroupEntryConfig> enemies =
                    rule.PickEnemiesForGalaxyLevel(
                        galaxyLevel,
                        enemyFaction);

                if (enemies != null && enemies.Count > 0)
                    return true;
            }
        }

        return false;
    }

    private bool HasEnemyOwnedSourceForFaction(
        EnemyFactionState faction,
        StarSystemConfig targetSystem)
    {
        if (faction == null ||
            targetSystem == null ||
            faction.OwnedSystemIds == null ||
            faction.OwnedSystemIds.Count == 0)
        {
            return false;
        }

        faction.EnsureWarTerritoryLists();

        for (int i = 0; i < faction.OwnedSystemIds.Count; i++)
        {
            string ownedSystemId =
                faction.OwnedSystemIds[i];

            if (string.IsNullOrWhiteSpace(ownedSystemId))
                continue;

            if (!_configService.TryGetStarSystem(
                    ownedSystemId,
                    out StarSystemConfig ownedSystem))
            {
                continue;
            }

            if (!IsCapturedSystem(ownedSystem.Id))
                continue;

            if (IsLinkedToTarget(ownedSystem, targetSystem))
                return true;
        }

        for (int i = 0; i < faction.OwnedSystemIds.Count; i++)
        {
            string ownedSystemId =
                faction.OwnedSystemIds[i];

            if (string.IsNullOrWhiteSpace(ownedSystemId))
                continue;

            if (!_configService.TryGetStarSystem(
                    ownedSystemId,
                    out StarSystemConfig ownedSystem))
            {
                continue;
            }

            if (IsCapturedSystem(ownedSystem.Id))
                return true;
        }

        return false;
    }
}