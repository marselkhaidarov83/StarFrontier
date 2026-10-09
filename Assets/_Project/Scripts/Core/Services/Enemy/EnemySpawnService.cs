using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class EnemySpawnService :
    CustomService,
    IEnemySpawnService
{
    private const string UnknownFactionId = "unknown";

    private readonly IGameSessionService _gameSessionService;
    private readonly ISystemNpcRuntimeService _npcRuntimeService;
    private readonly IEnemyFactionService _enemyFactionService;

    public EnemySpawnService()
    {
        _gameSessionService =
            Bootstrapper.Instance.ServiceRegistry
                .Get<IGameSessionService>();

        _npcRuntimeService =
            Bootstrapper.Instance.ServiceRegistry
                .Get<ISystemNpcRuntimeService>();

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _enemyFactionService);
    }

    public bool TrySpawnSystemGuardGroup(
        StarSystemConfig starSystem,
        EnemyGroupSpawnRuleConfig rule,
        out EnemyGroupState enemyGroupState)
    {
        return TrySpawnEnemyGroup(
            starSystem,
            starSystem,
            rule,
            string.Empty,
            out enemyGroupState);
    }

    public bool TrySpawnInvasionGroup(
        StarSystemConfig ownerSystem,
        StarSystemConfig targetSystem,
        EnemyGroupSpawnRuleConfig rule,
        string invasionId,
        out EnemyGroupState enemyGroupState)
    {
        return TrySpawnEnemyGroup(
            ownerSystem,
            targetSystem,
            rule,
            invasionId,
            out enemyGroupState);
    }

    public bool TryGetEnemyGroupState(
        string runtimeGroupId,
        out EnemyGroupState enemyGroupState)
    {
        enemyGroupState = null;

        if (string.IsNullOrWhiteSpace(runtimeGroupId))
            return false;

        GalaxyRuntimeState galaxyState =
            GetGalaxyState();

        if (galaxyState == null ||
            galaxyState.EnemyGroups == null)
        {
            return false;
        }

        enemyGroupState =
            galaxyState.EnemyGroups.FirstOrDefault(
                group =>
                    group != null &&
                    group.RuntimeGroupId == runtimeGroupId);

        return enemyGroupState != null;
    }

    public IReadOnlyList<EnemyGroupState> GetActiveEnemyGroupsInSystem(
        string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId))
            return Array.Empty<EnemyGroupState>();

        GalaxyRuntimeState galaxyState =
            GetGalaxyState();

        if (galaxyState == null ||
            galaxyState.EnemyGroups == null)
        {
            return Array.Empty<EnemyGroupState>();
        }

        return galaxyState.EnemyGroups
            .Where(group =>
                group != null &&
                group.SystemId == systemId &&
                group.IsActive())
            .ToList();
    }

    public void MarkEnemyGroupDestroyed(
        string runtimeGroupId)
    {
        if (!TryGetEnemyGroupState(
                runtimeGroupId,
                out EnemyGroupState enemyGroupState))
        {
            return;
        }

        int currentTick =
            GetCurrentQuantTick();

        enemyGroupState.LifecycleState =
            EnemyGroupLifecycleState.Destroyed;

        enemyGroupState.LastUpdatedTick =
            currentTick;

        enemyGroupState.CleanupAfterTick =
            currentTick + 1;

        if (_enemyFactionService != null)
        {
            _enemyFactionService.UnregisterEnemyGroup(
                enemyGroupState.FactionId,
                enemyGroupState.RuntimeGroupId);
        }
    }

    private bool TrySpawnEnemyGroup(
        StarSystemConfig ownerSystem,
        StarSystemConfig targetSystem,
        EnemyGroupSpawnRuleConfig rule,
        string invasionId,
        out EnemyGroupState enemyGroupState)
    {
        enemyGroupState = null;

        if (ownerSystem == null)
            return false;

        if (targetSystem == null)
            return false;

        if (rule == null)
            return false;

        GalaxyRuntimeState galaxyState =
            GetGalaxyState();

        if (galaxyState == null)
            return false;

        int currentGalaxyLevel =
            GetCurrentGalaxyLevel();

        IReadOnlyList<EnemyGroupEntryConfig> enemies =
            rule.PickEnemiesForGalaxyLevel(currentGalaxyLevel);

        if (enemies == null || enemies.Count == 0)
            return false;

        string runtimeGroupId =
            Guid.NewGuid().ToString("N");

        string factionId =
            ResolveFactionId(enemies);

        int currentTick =
            GetCurrentQuantTick();

        enemyGroupState =
            new EnemyGroupState
            {
                RuntimeGroupId = runtimeGroupId,
                FactionId = factionId,
                SystemId = targetSystem.Id,
                SpawnRuleId = rule.Id,
                InvasionId = string.IsNullOrWhiteSpace(invasionId)
                    ? string.Empty
                    : invasionId,
                Level = currentGalaxyLevel,
                LifecycleState = EnemyGroupLifecycleState.Spawning,
                CreatedAtTick = currentTick,
                LastUpdatedTick = currentTick,
                NextSpawnTick = 0,
                CleanupAfterTick = 0,
                OwnerSystemId = ownerSystem.Id,
                TargetSystemId = targetSystem.Id,
                MemberRuntimeNpcIds = new List<string>(),
                MemberConfigIds = new List<string>()
            };

        galaxyState.EnemyGroups.Add(enemyGroupState);

        Vector3 groupSpawnBasePosition =
            PickEnemyGroupSpawnBasePosition(targetSystem);

        for (int entryIndex = 0; entryIndex < enemies.Count; entryIndex++)
        {
            EnemyGroupEntryConfig entry =
                enemies[entryIndex];

            if (entry == null || !entry.IsValid())
                continue;

            EnemyConfig enemyConfig =
                entry.EnemyConfig;

            if (enemyConfig == null)
                continue;

            if (enemyConfig.Level != currentGalaxyLevel)
                continue;

            int count =
                UnityEngine.Random.Range(
                    entry.MinCount,
                    entry.MaxCount + 1);

            for (int i = 0; i < count; i++)
            {
                Vector3 position =
                    BuildEnemySpawnPosition(
                        targetSystem,
                        groupSpawnBasePosition);

                SystemNpcRuntimeState enemy =
                    SystemNpcRuntimeFactory.CreateEnemy(
                        enemyConfig,
                        ownerSystem.Id,
                        targetSystem.Id,
                        position,
                        rule.Id,
                        runtimeGroupId);

                ApplyInitialFacingToSun(
                    enemy,
                    targetSystem);

                enemy.CanChangeLocationOnRestore = false;

                _npcRuntimeService.AddNpc(enemy);

                enemyGroupState.MemberRuntimeNpcIds.Add(
                    enemy.RuntimeNpcId);

                enemyGroupState.MemberConfigIds.Add(
                    enemyConfig.Id);
            }
        }

        if (enemyGroupState.MemberRuntimeNpcIds.Count == 0)
        {
            galaxyState.EnemyGroups.Remove(enemyGroupState);
            enemyGroupState = null;
            return false;
        }

        enemyGroupState.LifecycleState =
            EnemyGroupLifecycleState.Active;

        enemyGroupState.LastUpdatedTick =
            GetCurrentQuantTick();

        if (_enemyFactionService != null)
        {
            _enemyFactionService.RegisterEnemyGroup(
                factionId,
                runtimeGroupId);

            if (!string.IsNullOrWhiteSpace(invasionId))
            {
                _enemyFactionService.RegisterInvasion(
                    factionId,
                    invasionId);
            }
        }

        LogCustom(
            "[EnemySpawnService] Enemy group spawned. " +
            "System: " + targetSystem.Id +
            ", OwnerSystem: " + ownerSystem.Id +
            ", Rule: " + rule.Id +
            ", Faction: " + factionId +
            ", GroupRuntimeId: " + runtimeGroupId +
            ", Members: " + enemyGroupState.MemberRuntimeNpcIds.Count +
            ", GalaxyLevel: " + currentGalaxyLevel);

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

    private static string ResolveFactionId(
        IReadOnlyList<EnemyGroupEntryConfig> enemies)
    {
        if (enemies == null)
            return UnknownFactionId;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyGroupEntryConfig entry =
                enemies[i];

            if (entry == null ||
                entry.EnemyConfig == null ||
                entry.EnemyConfig.WeaponGroups == null)
            {
                continue;
            }

            IReadOnlyList<WeaponGroupConfig> weaponGroups =
                entry.EnemyConfig.WeaponGroups;

            for (int groupIndex = 0;
                 groupIndex < weaponGroups.Count;
                 groupIndex++)
            {
                WeaponGroupConfig weaponGroup =
                    weaponGroups[groupIndex];

                if (weaponGroup == null)
                    continue;

                switch (weaponGroup.EnemyFaction)
                {
                    case WeaponGroupEnemyFaction.AI:
                        return "ai";

                    case WeaponGroupEnemyFaction.Ancients:
                        return "ancients";

                    case WeaponGroupEnemyFaction.Infected:
                        return "infected";
                }
            }
        }

        return UnknownFactionId;
    }

    private static Vector3 PickEnemyGroupSpawnBasePosition(
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

        return position;
    }

    private static Vector3 BuildEnemySpawnPosition(
        StarSystemConfig starSystem,
        Vector3 groupSpawnBasePosition)
    {
        float scatterRadius = 1.5f;

        if (starSystem != null &&
            starSystem.NpcSpawnPoints != null)
        {
            scatterRadius =
                starSystem.NpcSpawnPoints.EnemyRandomRadius;
        }

        Vector2 radial =
            new Vector2(
                groupSpawnBasePosition.x,
                groupSpawnBasePosition.y);

        if (radial.sqrMagnitude <= 0.0001f)
            radial = Vector2.right;
        else
            radial.Normalize();

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

    private static void ApplyInitialFacingToSun(
        SystemNpcRuntimeState npc,
        StarSystemConfig starSystem)
    {
        if (npc == null)
            return;

        Vector3 direction =
            BuildDirectionToSun(
                npc.CurrentPosition,
                starSystem);

        npc.FacingDirection =
            direction;
    }

    private static Vector3 BuildDirectionToSun(
        Vector3 currentPosition,
        StarSystemConfig starSystem)
    {
        if (starSystem == null ||
            starSystem.Sun == null)
        {
            return Vector3.up;
        }

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
}