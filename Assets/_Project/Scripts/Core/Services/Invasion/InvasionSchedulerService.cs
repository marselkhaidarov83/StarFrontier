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

        string factionId = PickRandomFactionId();
        if (!TryPickTargetSystem(
            maxActiveInvasions,
            galaxyLevel,
            factionId,
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

        if (!TryPickInvasionRule(
                targetSystem,
                galaxyLevel,
                factionId,
                out EnemyInvasionGroupSpawnRuleConfig invasionRule))
        {
            Log(
                "Scheduled invasion skipped: no valid invasion rule. " +
                "Target=" + targetSystem.Id +
                    ", Faction=" + factionId +
                ", GalaxyLevel=" + galaxyLevel +
                ", Tick=" + currentQuantTick);

            return false;
        }

        bool started =
            _invasionService.TryStartInvasion(
                null,
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
        string factionId,
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

            if (!HasValidInvasionRuleForFaction(
                    system,
                    galaxyLevel,
                    factionId))
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

    // Exactly three equiprobable factions; territories and routes do not participate.
    public static string PickRandomFactionId() => Random.Range(0, 3) switch
    {
        0 => "ai",
        1 => "ancients",
        _ => "infected"
    };

    private bool TryPickInvasionRule(
        StarSystemConfig targetSystem,
        int galaxyLevel,
        string factionId,
        out EnemyInvasionGroupSpawnRuleConfig selectedRule)
    {
        selectedRule = null;

        if (targetSystem == null ||
            targetSystem.InvasionSpawnRuleSet == null ||
            targetSystem.InvasionSpawnRuleSet.Entries == null)
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
            targetSystem.InvasionSpawnRuleSet.Entries;

        for (int i = 0; i < entries.Length; i++)
        {
            SystemPopulationEnemyGroupRuleEntry entry =
                entries[i];

            if (entry == null || !entry.IsValid())
                continue;

            EnemyInvasionGroupSpawnRuleConfig rule =
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





    private int GetCurrentGalaxyLevel()
    {
        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.OverrideEnemyGalaxyLevel)
        {
            return Bootstrapper.Instance.DebugEnemyGalaxyLevel;
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



    private static bool HasValidInvasionRuleForFaction(StarSystemConfig target, int level, string factionId)
    {
        if (!TryConvertFactionId(factionId, out EnemyFaction2A faction) || target?.InvasionSpawnRuleSet?.Entries == null)
            return false;
        foreach (var entry in target.InvasionSpawnRuleSet.Entries)
        {
            if (entry == null || !entry.IsValid()) continue;
            var groups = entry.EnemyGroupSpawnRule?.GetEntryForGalaxyLevel(level)?.EnemyGroups;
            if (groups != null && groups.Any(group => group != null && group.Faction == faction &&
                group.Enemies != null && group.Enemies.Count > 0 && group.Enemies.All(member => member != null && member.IsValid())))
                return true;
        }
        return false;
    }


}