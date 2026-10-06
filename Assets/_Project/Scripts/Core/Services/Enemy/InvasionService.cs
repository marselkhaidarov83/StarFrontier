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

        _eventBus.Subscribe<SystemEncounterResolvedEvent>(
            OnSystemEncounterResolved);

        _eventBus.Subscribe<SystemEncounterDefeatedEvent>(
            OnSystemEncounterDefeated);
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

        if (captureAllowed)
        {
            captureApplied =
                _systemSecurityService.CaptureSystem(
                    invasionState.TargetSystemId);
        }
        else
        {
            liberationApplied =
                _systemSecurityService.LiberateSystemByPlayer(
                    invasionState.TargetSystemId);

            if (liberationApplied)
            {
                _systemSecurityService.MarkRecoveryHookPending(
                    invasionState.TargetSystemId,
                    currentTick,
                    "player_liberation_after_invasion");
            }

            RemoveEnemyOwnershipForSystem(
                invasionState.TargetSystemId);
        }

        if (captureApplied &&
            _enemyFactionService != null)
        {
            _enemyFactionService.AddOwnedSystem(
                invasionState.FactionId,
                invasionState.TargetSystemId);
        }

        StarSystemStatus targetStatus =
            StarSystemStatus.Stable;

        _systemSecurityService.TryGetSystemStatus(
            invasionState.TargetSystemId,
            out targetStatus);

        LogCustom(
            "[InvasionService] Invasion resolved. " +
            "InvasionId: " + invasionId +
            ", CapturedByEnemy: " + capturedByEnemy +
            ", CaptureAllowed: " + captureAllowed +
            ", CaptureApplied: " + captureApplied +
            ", LiberationApplied: " + liberationApplied +
            ", TargetStatus: " + targetStatus);

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
        if (string.IsNullOrWhiteSpace(systemId))
            return false;

        if (!TryGetActiveInvasionForTarget(
                systemId,
                out InvasionState invasionState))
        {
            return false;
        }

        bool capturedByEnemy =
            !playerVictory;

        return ResolveInvasion(
            invasionState.InvasionId,
            capturedByEnemy);
    }

    private void OnSystemEncounterResolved(
    SystemEncounterResolvedEvent evt)
    {
        ResolveInvasionFromCombatOutcome(
            evt.SystemId,
            true);
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
}