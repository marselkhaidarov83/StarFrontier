using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class InvasionService :
    CustomService,
    IInvasionService
{
    private readonly IGameSessionService _gameSessionService;
    private readonly IEnemySpawnService _enemySpawnService;
    private readonly ISystemSecurityService _systemSecurityService;
    private readonly ISystemNpcRuntimeService _npcRuntimeService;
    private readonly IEnemyFactionService _enemyFactionService;
    private readonly SimpleEventBus _eventBus;
    private const int MaxActiveInvasions = 3;

    public InvasionService()
    {
        _gameSessionService =
            Bootstrapper.Instance.ServiceRegistry
                .Get<IGameSessionService>();

        _enemySpawnService =
            Bootstrapper.Instance.ServiceRegistry
                .Get<IEnemySpawnService>();

        _systemSecurityService =
            Bootstrapper.Instance.ServiceRegistry
                .Get<ISystemSecurityService>();

        _eventBus =
            Bootstrapper.Instance.ServiceRegistry
                .Get<SimpleEventBus>();

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _enemyFactionService);

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _npcRuntimeService);

        _eventBus.Subscribe<SystemEncounterResolvedEvent>(
            OnSystemEncounterResolved);

        _eventBus.Subscribe<SystemEncounterDefeatedEvent>(
            OnSystemEncounterDefeated);

        _eventBus.Subscribe<SystemNpcDestroyedEvent>(
            OnSystemNpcDestroyed);
    }

    private void OnSystemNpcDestroyed(
        SystemNpcDestroyedEvent evt)
    {
        if (evt.NpcType != SystemNpcType.Enemy)
            return;

        if (string.IsNullOrWhiteSpace(evt.SystemId))
            return;

        LogWarStateDebug(
            "SYSTEM_NPC_DESTROYED_EVENT_RECEIVED" +
            " | RuntimeNpcId=" + evt.RuntimeNpcId +
            " | RuntimeNpcGroupId=" + evt.RuntimeNpcGroupId +
            " | NpcType=" + evt.NpcType +
            " | SystemId=" + evt.SystemId +
            " | WasKilledByPlayer=" + evt.WasKilledByPlayer +
            BuildWarStateSnapshot(evt.SystemId));

        if (!TryGetActiveInvasionForTarget(
                evt.SystemId,
                out InvasionState invasionState))
        {
            LogWarStateDebug(
                "SYSTEM_NPC_DESTROYED_EVENT_IGNORED" +
                " | Reason=NoActiveInvasionForTarget" +
                " | RuntimeNpcId=" + evt.RuntimeNpcId +
                " | SystemId=" + evt.SystemId +
                BuildWarStateSnapshot(evt.SystemId));

            return;
        }

        if (!IsNpcFromInvasion(
                evt.RuntimeNpcGroupId,
                invasionState))
        {
            LogWarStateDebug(
                "SYSTEM_NPC_DESTROYED_EVENT_IGNORED" +
                " | Reason=NpcGroupDoesNotBelongToInvasion" +
                " | RuntimeNpcId=" + evt.RuntimeNpcId +
                " | RuntimeNpcGroupId=" + evt.RuntimeNpcGroupId +
                " | InvasionId=" + invasionState.InvasionId +
                " | SystemId=" + evt.SystemId +
                BuildWarStateSnapshot(evt.SystemId));

            return;
        }

        int aliveEnemyCount =
            CountAliveEnemyNpcsForInvasion(invasionState);

        LogWarStateDebug(
            "SYSTEM_NPC_DESTROYED_EVENT_CHECKED" +
            " | RuntimeNpcId=" + evt.RuntimeNpcId +
            " | RuntimeNpcGroupId=" + evt.RuntimeNpcGroupId +
            " | InvasionId=" + invasionState.InvasionId +
            " | SystemId=" + evt.SystemId +
            " | AliveEnemyCount=" + aliveEnemyCount +
            BuildWarStateSnapshot(evt.SystemId));

        if (aliveEnemyCount > 0)
            return;

        bool resolved =
            ResolveInvasion(
                invasionState.InvasionId,
                false);

        LogWarStateDebug(
            "SYSTEM_NPC_DESTROYED_EVENT_RESOLVE_ATTEMPT" +
            " | RuntimeNpcId=" + evt.RuntimeNpcId +
            " | RuntimeNpcGroupId=" + evt.RuntimeNpcGroupId +
            " | InvasionId=" + invasionState.InvasionId +
            " | SystemId=" + evt.SystemId +
            " | AliveEnemyCount=" + aliveEnemyCount +
            " | ResolveInvasionResult=" + resolved +
            BuildWarStateSnapshot(evt.SystemId));
    }

    private bool IsNpcFromInvasion(
        string runtimeNpcGroupId,
        InvasionState invasionState)
    {
        if (invasionState == null)
            return false;

        if (invasionState.EnemyGroupRuntimeIds == null ||
            invasionState.EnemyGroupRuntimeIds.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(runtimeNpcGroupId))
            return false;

        for (int i = 0; i < invasionState.EnemyGroupRuntimeIds.Count; i++)
        {
            string invasionGroupRuntimeId =
                invasionState.EnemyGroupRuntimeIds[i];

            if (invasionGroupRuntimeId == runtimeNpcGroupId)
                return true;
        }

        return false;
    }

    private int CountAliveEnemyNpcsForInvasion(
        InvasionState invasionState)
    {
        if (invasionState == null)
            return 0;

        if (_npcRuntimeService == null)
        {
            LogWarStateDebug(
                "COUNT_ALIVE_INVASION_ENEMIES_FAILED" +
                " | Reason=NpcRuntimeServiceNull" +
                " | InvasionId=" + invasionState.InvasionId +
                " | TargetSystemId=" + invasionState.TargetSystemId +
                BuildWarStateSnapshot(invasionState.TargetSystemId));

            return 0;
        }

        IReadOnlyList<SystemNpcRuntimeState> aliveEnemies =
            _npcRuntimeService.GetAliveNpcsInSystemByType(
                invasionState.TargetSystemId,
                SystemNpcType.Enemy);

        if (aliveEnemies == null ||
            aliveEnemies.Count == 0)
        {
            return 0;
        }

        if (invasionState.EnemyGroupRuntimeIds == null ||
            invasionState.EnemyGroupRuntimeIds.Count == 0)
        {
            return aliveEnemies.Count;
        }

        int count = 0;

        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            SystemNpcRuntimeState npc =
                aliveEnemies[i];

            if (npc == null)
                continue;

            if (string.IsNullOrWhiteSpace(npc.GroupRuntimeId))
                continue;

            if (!invasionState.EnemyGroupRuntimeIds.Contains(
                    npc.GroupRuntimeId))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    public IReadOnlyList<InvasionState> GetActiveInvasions()
    {
        GalaxyRuntimeState galaxyState =
            GetGalaxyState();

        if (galaxyState == null ||
            galaxyState.Invasions == null)
        {
            return Array.Empty<InvasionState>();
        }

        return galaxyState.Invasions
            .Where(invasion =>
                invasion != null &&
                invasion.IsActive())
            .ToList();
    }

    public bool TryGetInvasion(
        string invasionId,
        out InvasionState invasionState)
    {
        invasionState = null;

        if (string.IsNullOrWhiteSpace(invasionId))
            return false;

        GalaxyRuntimeState galaxyState =
            GetGalaxyState();

        if (galaxyState == null ||
            galaxyState.Invasions == null)
        {
            return false;
        }

        invasionState =
            galaxyState.Invasions.FirstOrDefault(
                invasion =>
                    invasion != null &&
                    invasion.InvasionId == invasionId);

        return invasionState != null;
    }

    public bool HasActiveInvasionForTarget(
    string targetSystemId)
    {
        GalaxyRuntimeState galaxyState =
            GetGalaxyState();

        if (galaxyState == null)
            return false;

        return galaxyState.HasActiveInvasionForTarget(
            targetSystemId);
    }

    public int GetActiveInvasionCount()
    {
        GalaxyRuntimeState galaxyState =
            GetGalaxyState();

        if (galaxyState == null)
            return 0;

        return galaxyState.CountActiveInvasions();
    }

    public bool CanStartInvasion(
    string targetSystemId)
    {
        GalaxyRuntimeState galaxyState =
            GetGalaxyState();

        if (galaxyState == null)
            return false;

        if (_systemSecurityService == null ||
            !_systemSecurityService.CanSystemBeTargetedByInvasion(targetSystemId))
        {
            return false;
        }

        return galaxyState.CanStartAdditionalInvasion(
            targetSystemId,
            MaxActiveInvasions);
    }

    public bool TryStartInvasion(
        StarSystemConfig sourceSystem,
        StarSystemConfig targetSystem,
        EnemyGroupSpawnRuleConfig invasionSpawnRule,
        string factionId,
        int resolveAfterTicks,
        out InvasionState invasionState)
    {
        invasionState = null;

        if (sourceSystem == null)
            return false;

        if (targetSystem == null)
            return false;

        if (invasionSpawnRule == null)
            return false;

        if (string.IsNullOrWhiteSpace(sourceSystem.Id) ||
            string.IsNullOrWhiteSpace(targetSystem.Id))
        {
            return false;
        }

        if (sourceSystem.Id == targetSystem.Id)
            return false;

        if (!CanStartInvasion(targetSystem.Id))
            return false;

        GalaxyRuntimeState galaxyState =
            GetGalaxyState();

        if (galaxyState == null)
            return false;

        StarSystemStatus previousTargetStatus =
            StarSystemStatus.Stable;

        _systemSecurityService.TryGetSystemStatus(
            targetSystem.Id,
            out previousTargetStatus);

        if (!MarkSystemThreat(targetSystem.Id))
            return false;

        string normalizedFactionId =
            NormalizeFactionId(factionId);

        if (string.IsNullOrWhiteSpace(normalizedFactionId))
            normalizedFactionId = "unknown";

        int currentTick =
            GetCurrentQuantTick();

        string invasionId =
            Guid.NewGuid().ToString("N");

        int currentGalaxyLevel = GetCurrentGalaxyLevel();

        invasionState =
            new InvasionState
            {
                InvasionId = invasionId,
                FactionId = normalizedFactionId,
                SourceSystemId = sourceSystem.Id,
                TargetSystemId = targetSystem.Id,
                Level = currentGalaxyLevel,
                LifecycleState = InvasionLifecycleState.Preparing,
                EnemyGroupRuntimeIds = new List<string>(),
                StartedAtTick = currentTick,
                LastUpdatedTick = currentTick,
                NextUpdateTick = currentTick + 1,
                ResolveAtTick = currentTick + Mathf.Max(1, resolveAfterTicks),
                CleanupAfterTick = 0
            };

        invasionState.ApplyEscalation(
            normalizedFactionId,
            currentGalaxyLevel);

        galaxyState.Invasions.Add(invasionState);

        bool spawned =
            _enemySpawnService.TrySpawnInvasionGroup(
                sourceSystem,
                targetSystem,
                invasionSpawnRule,
                invasionId,
                out EnemyGroupState enemyGroupState);

        if (!spawned ||
            enemyGroupState == null ||
            string.IsNullOrWhiteSpace(enemyGroupState.RuntimeGroupId))
        {
            galaxyState.Invasions.Remove(invasionState);

            _systemSecurityService.SetSystemStatus(
                targetSystem.Id,
                previousTargetStatus);

            invasionState = null;
            return false;
        }

        invasionState.EnemyGroupRuntimeIds.Add(
            enemyGroupState.RuntimeGroupId);

        invasionState.LifecycleState =
            InvasionLifecycleState.Active;

        invasionState.LastUpdatedTick =
            GetCurrentQuantTick();

        _systemSecurityService.SetSystemStatus(
            targetSystem.Id,
            StarSystemStatus.Invasion);

        if (_enemyFactionService != null)
        {
            _enemyFactionService.RegisterInvasion(
                normalizedFactionId,
                invasionId);

            _enemyFactionService.AddTerritorySystem(
                normalizedFactionId,
                targetSystem.Id);
        }

        LogCustom(
            "[InvasionService] Invasion started. " +
            "InvasionId: " + invasionId +
            ", Faction: " + normalizedFactionId +
            ", Source: " + sourceSystem.Id +
            ", Target: " + targetSystem.Id +
            ", Rule: " + invasionSpawnRule.Id);

        PublishWarNews(
            WarNewsEventKind.InvasionStarted,
            targetSystem.Id,
            normalizedFactionId,
            "Вторжение началось",
            "Система " + targetSystem.DisplayName +
            " атакована фракцией " +
            GetWarNewsFactionDisplayName(normalizedFactionId) + ".",
            currentTick,
            100);


        return true;
    }

    public void UpdateInvasions()
    {
        GalaxyRuntimeState galaxyState =
            GetGalaxyState();

        if (galaxyState == null ||
            galaxyState.Invasions == null)
        {
            return;
        }

        int currentTick =
            GetCurrentQuantTick();

        for (int i = 0; i < galaxyState.Invasions.Count; i++)
        {
            InvasionState invasionState =
                galaxyState.Invasions[i];

            if (invasionState == null)
                continue;

            if (!invasionState.IsActive())
                continue;

            if (invasionState.NextUpdateTick > currentTick)
                continue;

            invasionState.LastUpdatedTick =
                currentTick;

            invasionState.NextUpdateTick =
                currentTick + 1;

            if (invasionState.ResolveAtTick > 0 &&
                currentTick >= invasionState.ResolveAtTick)
            {
                ResolveInvasion(
                    invasionState.InvasionId,
                    true);
            }
        }
    }

    public bool ResolveInvasion(
        string invasionId,
        bool capturedByEnemy)
    {
        LogWarStateDebug(
            "RESOLVE_INVASION_ENTER" +
            " | InvasionId=" + (invasionId ?? string.Empty) +
            " | CapturedByEnemy=" + capturedByEnemy);

        if (!TryGetInvasion(
                invasionId,
                out InvasionState invasionState))
        {
            LogWarStateDebug(
                "RESOLVE_INVASION_FAILED" +
                " | Reason=InvasionNotFound" +
                " | InvasionId=" + (invasionId ?? string.Empty));

            return false;
        }

        LogWarStateDebug(
            "RESOLVE_INVASION_FOUND" +
            " | InvasionId=" + invasionState.InvasionId +
            " | TargetSystemId=" + invasionState.TargetSystemId +
            " | SourceSystemId=" + invasionState.SourceSystemId +
            " | FactionId=" + invasionState.FactionId +
            " | LifecycleStateBefore=" + invasionState.LifecycleState +
            " | CapturedByEnemy=" + capturedByEnemy +
            BuildWarStateSnapshot(invasionState.TargetSystemId));

        if (invasionState.LifecycleState ==
            InvasionLifecycleState.CleanedUp)
        {
            LogWarStateDebug(
                "RESOLVE_INVASION_FAILED" +
                " | Reason=AlreadyCleanedUp" +
                " | InvasionId=" + invasionState.InvasionId +
                BuildWarStateSnapshot(invasionState.TargetSystemId));

            return false;
        }

        int currentTick =
            GetCurrentQuantTick();

        StarSystemStatus statusBeforeResolve =
            StarSystemStatus.Stable;

        bool hadStatusBeforeResolve =
            _systemSecurityService.TryGetSystemStatus(
                invasionState.TargetSystemId,
                out statusBeforeResolve);

        invasionState.LifecycleState =
            InvasionLifecycleState.Resolved;

        invasionState.LastUpdatedTick =
            currentTick;

        invasionState.CleanupAfterTick =
            currentTick + 1;

        bool captureAllowed =
            capturedByEnemy &&
            CanCaptureSystemWithoutHopelessCollapse(
                invasionState.TargetSystemId);

        bool captureApplied = false;
        bool liberationApplied = false;

        LogWarStateDebug(
            "RESOLVE_INVASION_STATUS_BEFORE_APPLY" +
            " | InvasionId=" + invasionState.InvasionId +
            " | TargetSystemId=" + invasionState.TargetSystemId +
            " | HadStatusBeforeResolve=" + hadStatusBeforeResolve +
            " | StatusBeforeResolve=" + statusBeforeResolve +
            " | CapturedByEnemy=" + capturedByEnemy +
            " | CaptureAllowed=" + captureAllowed +
            " | CurrentTick=" + currentTick +
            BuildWarStateSnapshot(invasionState.TargetSystemId));

        if (captureAllowed)
        {
            captureApplied =
                _systemSecurityService.CaptureSystem(
                    invasionState.TargetSystemId);

            LogWarStateDebug(
                "RESOLVE_INVASION_CAPTURE_BRANCH" +
                " | InvasionId=" + invasionState.InvasionId +
                " | TargetSystemId=" + invasionState.TargetSystemId +
                " | CaptureApplied=" + captureApplied +
                BuildWarStateSnapshot(invasionState.TargetSystemId));
        }
        else
        {
            liberationApplied =
                _systemSecurityService.LiberateSystemByPlayer(
                    invasionState.TargetSystemId);

            LogWarStateDebug(
                "RESOLVE_INVASION_LIBERATION_BRANCH" +
                " | InvasionId=" + invasionState.InvasionId +
                " | TargetSystemId=" + invasionState.TargetSystemId +
                " | LiberationApplied=" + liberationApplied +
                BuildWarStateSnapshot(invasionState.TargetSystemId));

            if (liberationApplied)
            {
                bool recoveryHookApplied =
                    _systemSecurityService.MarkRecoveryHookPending(
                        invasionState.TargetSystemId,
                        currentTick,
                        "player_liberation_after_invasion");

                LogWarStateDebug(
                    "RESOLVE_INVASION_RECOVERY_HOOK" +
                    " | InvasionId=" + invasionState.InvasionId +
                    " | TargetSystemId=" + invasionState.TargetSystemId +
                    " | RecoveryHookApplied=" + recoveryHookApplied +
                    " | CurrentTick=" + currentTick +
                    BuildWarStateSnapshot(invasionState.TargetSystemId));
            }

            RemoveEnemyOwnershipForSystem(
                invasionState.TargetSystemId);

            LogWarStateDebug(
                "RESOLVE_INVASION_ENEMY_OWNERSHIP_REMOVED" +
                " | InvasionId=" + invasionState.InvasionId +
                " | TargetSystemId=" + invasionState.TargetSystemId +
                " | FactionId=" + invasionState.FactionId +
                BuildWarStateSnapshot(invasionState.TargetSystemId));
        }

        if (captureApplied &&
            _enemyFactionService != null)
        {
            _enemyFactionService.AddOwnedSystem(
                invasionState.FactionId,
                invasionState.TargetSystemId);

            LogWarStateDebug(
                "RESOLVE_INVASION_ENEMY_OWNERSHIP_ADDED" +
                " | InvasionId=" + invasionState.InvasionId +
                " | TargetSystemId=" + invasionState.TargetSystemId +
                " | FactionId=" + invasionState.FactionId +
                BuildWarStateSnapshot(invasionState.TargetSystemId));
        }

        StarSystemStatus targetStatus =
            StarSystemStatus.Stable;

        bool hasTargetStatus =
            _systemSecurityService.TryGetSystemStatus(
                invasionState.TargetSystemId,
                out targetStatus);

        LogWarStateDebug(
            "RESOLVE_INVASION_EXIT" +
            " | InvasionId=" + invasionId +
            " | TargetSystemId=" + invasionState.TargetSystemId +
            " | CapturedByEnemy=" + capturedByEnemy +
            " | CaptureAllowed=" + captureAllowed +
            " | CaptureApplied=" + captureApplied +
            " | LiberationApplied=" + liberationApplied +
            " | HasTargetStatus=" + hasTargetStatus +
            " | TargetStatus=" + targetStatus +
            BuildWarStateSnapshot(invasionState.TargetSystemId));

        LogCustom(
    "[InvasionService] Invasion resolved. " +
    "InvasionId: " + invasionId +
    ", CapturedByEnemy: " + capturedByEnemy +
    ", CaptureAllowed: " + captureAllowed +
    ", CaptureApplied: " + captureApplied +
    ", LiberationApplied: " + liberationApplied +
    ", TargetStatus: " + targetStatus);

        if (captureApplied)
        {
            PublishWarNews(
                WarNewsEventKind.SystemCaptured,
                invasionState.TargetSystemId,
                invasionState.FactionId,
                "Система захвачена",
                "Враг закрепился в системе " +
                invasionState.TargetSystemId +
                ". Фронт войны сместился.",
                currentTick,
                90);
        }
        else if (liberationApplied)
        {
            PublishWarNews(
                WarNewsEventKind.SystemLiberated,
                invasionState.TargetSystemId,
                invasionState.FactionId,
                "Система освобождена",
                "Контроль над системой " +
                invasionState.TargetSystemId +
                " возвращён цивилизации.",
                currentTick,
                80);
        }

        return true;
    }

    private void RemoveEnemyOwnershipForSystem(
        string systemId)
    {
        if (_enemyFactionService == null ||
            string.IsNullOrWhiteSpace(systemId))
        {
            return;
        }

        IReadOnlyList<EnemyFactionState> factions =
            _enemyFactionService.GetFactions();

        if (factions == null)
            return;

        for (int i = 0; i < factions.Count; i++)
        {
            EnemyFactionState faction =
                factions[i];

            if (faction == null ||
                string.IsNullOrWhiteSpace(faction.FactionId))
            {
                continue;
            }

            _enemyFactionService.RemoveOwnedSystem(
                faction.FactionId,
                systemId);

            _enemyFactionService.RemoveTerritorySystem(
                faction.FactionId,
                systemId);
        }
    }

    public bool CancelInvasion(
        string invasionId)
    {
        if (!TryGetInvasion(
                invasionId,
                out InvasionState invasionState))
        {
            return false;
        }

        if (invasionState.LifecycleState ==
            InvasionLifecycleState.CleanedUp)
        {
            return false;
        }

        int currentTick =
            GetCurrentQuantTick();

        invasionState.LifecycleState =
            InvasionLifecycleState.Cancelled;

        invasionState.LastUpdatedTick =
            currentTick;

        invasionState.CleanupAfterTick =
            currentTick + 1;

        _systemSecurityService.SetSystemStatus(
            invasionState.TargetSystemId,
            StarSystemStatus.Stable);

        if (_enemyFactionService != null)
        {
            _enemyFactionService.UnregisterInvasion(
                invasionState.FactionId,
                invasionState.InvasionId);
        }

        LogCustom(
            "[InvasionService] Invasion cancelled. " +
            "InvasionId: " + invasionId);

        PublishWarNews(
            WarNewsEventKind.InvasionCancelled,
            invasionState.TargetSystemId,
            invasionState.FactionId,
            "Вторжение сорвано",
            "Атака на систему " +
            invasionState.TargetSystemId +
            " прекращена.",
            currentTick,
            60);

        return true;
    }

    public bool CleanupInvasion(
        string invasionId)
    {
        if (!TryGetInvasion(
                invasionId,
                out InvasionState invasionState))
        {
            return false;
        }

        if (invasionState.LifecycleState ==
            InvasionLifecycleState.CleanedUp)
        {
            return true;
        }

        int currentTick =
            GetCurrentQuantTick();

        if (invasionState.CleanupAfterTick > 0 &&
            currentTick < invasionState.CleanupAfterTick)
        {
            return false;
        }

        if (invasionState.EnemyGroupRuntimeIds != null)
        {
            for (int i = 0;
                 i < invasionState.EnemyGroupRuntimeIds.Count;
                 i++)
            {
                string groupRuntimeId =
                    invasionState.EnemyGroupRuntimeIds[i];

                _enemySpawnService.MarkEnemyGroupDestroyed(
                    groupRuntimeId);
            }
        }

        if (_enemyFactionService != null)
        {
            _enemyFactionService.UnregisterInvasion(
                invasionState.FactionId,
                invasionState.InvasionId);
        }

        invasionState.LifecycleState =
            InvasionLifecycleState.CleanedUp;

        invasionState.LastUpdatedTick =
            currentTick;

        LogCustom(
            "[InvasionService] Invasion cleaned up. " +
            "InvasionId: " + invasionId);

        return true;
    }

    private GalaxyRuntimeState GetGalaxyState()
    {
        if (_gameSessionService == null ||
            _gameSessionService.State == null ||
            _gameSessionService.State.Galaxy == null)
        {
            return null;
        }

        GalaxyRuntimeState galaxyState =
            _gameSessionService.State.Galaxy;

        galaxyState.EnsureWarStateCollections();

        return galaxyState;
    }

    private int GetCurrentQuantTick()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return 1;
        }

        if (Bootstrapper.Instance.ServiceRegistry.TryGet(
                out IGameTimeService gameTimeService))
        {
            return Mathf.Max(
                1,
                gameTimeService.CurrentQuantTick);
        }

        return 1;
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

    private static string NormalizeFactionId(
        string factionId)
    {
        return string.IsNullOrWhiteSpace(factionId)
            ? string.Empty
            : factionId.Trim().ToLowerInvariant();
    }

    public bool MarkSystemThreat(
    string targetSystemId)
    {
        if (string.IsNullOrWhiteSpace(targetSystemId))
            return false;

        if (_systemSecurityService == null)
            return false;

        if (!_systemSecurityService.CanSystemBeTargetedByInvasion(targetSystemId))
            return false;

        return _systemSecurityService.SetSystemStatus(
            targetSystemId,
            StarSystemStatus.Threat);
    }

    public bool CanCaptureSystemWithoutHopelessCollapse(
    string targetSystemId)
    {
        GalaxyRuntimeState galaxyState =
            GetGalaxyState();

        if (galaxyState == null)
            return false;

        return galaxyState.CanCaptureSystemWithoutHopelessCollapse(
            targetSystemId);
    }

    public int ProcessOfflineWarCatchUp(
    GameRuntimeState state,
    int targetQuantTick)
    {
        if (state == null ||
            state.Galaxy == null)
        {
            return 0;
        }

        return state.Galaxy.ProcessOfflineWarCatchUp(
            targetQuantTick);
    }

    public bool ResolveInvasionFromCombatOutcome(
        string systemId,
        bool playerVictory)
    {
        LogWarStateDebug(
            "RESOLVE_FROM_COMBAT_OUTCOME_ENTER" +
            " | SystemId=" + (systemId ?? string.Empty) +
            " | PlayerVictory=" + playerVictory +
            BuildWarStateSnapshot(systemId));

        if (string.IsNullOrWhiteSpace(systemId))
        {
            LogWarStateDebug(
                "RESOLVE_FROM_COMBAT_OUTCOME_FAILED" +
                " | Reason=SystemIdEmpty");
            return false;
        }

        if (!TryGetActiveInvasionForTarget(
                systemId,
                out InvasionState invasionState))
        {
            LogWarStateDebug(
                "RESOLVE_FROM_COMBAT_OUTCOME_FAILED" +
                " | Reason=NoActiveInvasionForTarget" +
                " | SystemId=" + systemId +
                BuildWarStateSnapshot(systemId));
            return false;
        }

        bool capturedByEnemy =
            !playerVictory;

        LogWarStateDebug(
            "RESOLVE_FROM_COMBAT_OUTCOME_FOUND_INVASION" +
            " | SystemId=" + systemId +
            " | InvasionId=" + invasionState.InvasionId +
            " | LifecycleState=" + invasionState.LifecycleState +
            " | CapturedByEnemy=" + capturedByEnemy +
            BuildWarStateSnapshot(systemId));

        bool resolved =
            ResolveInvasion(
                invasionState.InvasionId,
                capturedByEnemy);

        LogWarStateDebug(
            "RESOLVE_FROM_COMBAT_OUTCOME_EXIT" +
            " | SystemId=" + systemId +
            " | InvasionId=" + invasionState.InvasionId +
            " | Result=" + resolved +
            BuildWarStateSnapshot(systemId));

        return resolved;
    }

    private void LogWarStateDebug(string message)
    {
        if (Bootstrapper.Instance == null)
            return;

        if (!Bootstrapper.Instance.IsDebugLogEnabled(DebugLogChannel.Combat))
            return;

        Bootstrapper.Instance.LogDebug(
            DebugLogChannel.Combat,
            "[InvasionService][WarState] " + message);
    }

    private void OnSystemEncounterResolved(
        SystemEncounterResolvedEvent evt)
    {
        LogWarStateDebug(
            "SYSTEM_ENCOUNTER_RESOLVED_EVENT_RECEIVED" +
            " | EncounterId=" + evt.EncounterId +
            " | SystemId=" + evt.SystemId +
            BuildWarStateSnapshot(evt.SystemId));

        bool resolved =
            ResolveInvasionFromCombatOutcome(
                evt.SystemId,
                true);

        LogWarStateDebug(
            "SYSTEM_ENCOUNTER_RESOLVED_EVENT_HANDLED" +
            " | EncounterId=" + evt.EncounterId +
            " | SystemId=" + evt.SystemId +
            " | ResolveInvasionResult=" + resolved +
            BuildWarStateSnapshot(evt.SystemId));
    }

    private string BuildWarStateSnapshot(string systemId)
    {
        GalaxyRuntimeState galaxyState =
            GetGalaxyState();

        StarSystemStatus systemStatus =
            StarSystemStatus.Stable;

        bool hasSystemStatus =
            _systemSecurityService != null &&
            _systemSecurityService.TryGetSystemStatus(
                systemId,
                out systemStatus);

        int activeInvasionsForSystem = 0;
        string activeInvasionIds = string.Empty;
        string activeInvasionStates = string.Empty;

        if (galaxyState != null &&
            galaxyState.Invasions != null)
        {
            for (int i = 0; i < galaxyState.Invasions.Count; i++)
            {
                InvasionState invasion =
                    galaxyState.Invasions[i];

                if (invasion == null)
                    continue;

                if (invasion.TargetSystemId != systemId)
                    continue;

                if (!invasion.IsActive())
                    continue;

                activeInvasionsForSystem++;

                if (!string.IsNullOrWhiteSpace(activeInvasionIds))
                    activeInvasionIds += ",";

                if (!string.IsNullOrWhiteSpace(activeInvasionStates))
                    activeInvasionStates += ",";

                activeInvasionIds += invasion.InvasionId;
                activeInvasionStates += invasion.LifecycleState.ToString();
            }
        }

        return
            " | HasSystemStatus=" + hasSystemStatus +
            " | SystemStatus=" + systemStatus +
            " | ActiveInvasionsForSystem=" + activeInvasionsForSystem +
            " | ActiveInvasionIds=" + activeInvasionIds +
            " | ActiveInvasionStates=" + activeInvasionStates;
    }

    private void OnSystemEncounterDefeated(
        SystemEncounterDefeatedEvent evt)
    {
        ResolveInvasionFromCombatOutcome(
            evt.SystemId,
            false);
    }

    private bool TryGetActiveInvasionForTarget(
        string targetSystemId,
        out InvasionState invasionState)
    {
        invasionState = null;

        if (string.IsNullOrWhiteSpace(targetSystemId))
            return false;

        GalaxyRuntimeState galaxyState =
            GetGalaxyState();

        if (galaxyState == null ||
            galaxyState.Invasions == null)
        {
            return false;
        }

        invasionState =
            galaxyState.Invasions.FirstOrDefault(
                invasion =>
                    invasion != null &&
                    invasion.TargetSystemId == targetSystemId &&
                    invasion.IsActive());

        return invasionState != null;
    }

    private void PublishWarNews(
    WarNewsEventKind kind,
    string systemId,
    string factionId,
    string title,
    string body,
    int createdAtTick,
    int priority)
    {
        _eventBus?.Publish(
            new WarNewsItemCreatedEvent(
                kind,
                systemId,
                factionId,
                title,
                body,
                createdAtTick,
                priority));
    }

    private string GetWarNewsFactionDisplayName(
        string factionId)
    {
        string normalizedFactionId =
            NormalizeFactionId(factionId);

        switch (normalizedFactionId)
        {
            case "ancients":
                return "Древние";

            case "ai":
                return "Враждебный ИИ";

            case "infected":
                return "Заражённые";

            default:
                return string.IsNullOrWhiteSpace(factionId)
                    ? "неизвестная фракция"
                    : factionId;
        }
    }
}