using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class Stage2A_S06_01_T08_WarStateEditModeTests
{
    [Test]
    public void EnemyGroupSpawnRule_PicksOnlyEnemiesWithExactGalaxyLevel()
    {
        EnemyGroupSpawnRuleConfig rule =
            ScriptableObject.CreateInstance<EnemyGroupSpawnRuleConfig>();

        EnemyConfig enemyLevel3 =
            CreateEnemyConfigWithLevel(3);

        EnemyConfig enemyLevel4 =
            CreateEnemyConfigWithLevel(4);

        EnemyGroupEntryConfig wrongEntry =
            CreateEnemyGroupEntry(
                enemyLevel3,
                1,
                1);

        EnemyGroupEntryConfig strictEntry =
            CreateEnemyGroupEntry(
                enemyLevel4,
                1,
                1);

        EnemyGroupSpawnOptionConfig wrongGroup =
            CreateEnemyGroupOption(
                100,
                wrongEntry);

        EnemyGroupSpawnOptionConfig strictGroup =
            CreateEnemyGroupOption(
                1,
                strictEntry);

        EnemyGroupSpawnLevelEntryConfig levelEntry =
            CreateEnemyGroupLevelEntry(
                4,
                wrongGroup,
                strictGroup);

        SetPrivateField(
            rule,
            "levelEntries",
            new[]
            {
                levelEntry
            });

        IReadOnlyList<EnemyGroupEntryConfig> pickedEnemies =
            rule.PickEnemiesForGalaxyLevel(4);

        Assert.That(pickedEnemies, Is.Not.Null);
        Assert.That(pickedEnemies.Count, Is.EqualTo(1));
        Assert.That(pickedEnemies[0].EnemyConfig, Is.EqualTo(enemyLevel4));
        Assert.That(pickedEnemies[0].EnemyConfig.Level, Is.EqualTo(4));

        Object.DestroyImmediate(enemyLevel3);
        Object.DestroyImmediate(enemyLevel4);
        Object.DestroyImmediate(rule);
    }

    [Test]
    public void EnemyGroupSpawnRule_ReturnsNoEnemiesWhenExactGalaxyLevelIsMissing()
    {
        EnemyGroupSpawnRuleConfig rule =
            ScriptableObject.CreateInstance<EnemyGroupSpawnRuleConfig>();

        EnemyConfig enemyLevel3 =
            CreateEnemyConfigWithLevel(3);

        EnemyGroupEntryConfig wrongEntry =
            CreateEnemyGroupEntry(
                enemyLevel3,
                1,
                1);

        EnemyGroupSpawnOptionConfig wrongGroup =
            CreateEnemyGroupOption(
                1,
                wrongEntry);

        EnemyGroupSpawnLevelEntryConfig levelEntry =
            CreateEnemyGroupLevelEntry(
                4,
                wrongGroup);

        SetPrivateField(
            rule,
            "levelEntries",
            new[]
            {
                levelEntry
            });

        IReadOnlyList<EnemyGroupEntryConfig> pickedEnemies =
            rule.PickEnemiesForGalaxyLevel(4);

        Assert.That(pickedEnemies, Is.Not.Null);
        Assert.That(pickedEnemies.Count, Is.Zero);
        Assert.That(rule.HasValidEnemiesForGalaxyLevel(4), Is.False);

        Object.DestroyImmediate(enemyLevel3);
        Object.DestroyImmediate(rule);
    }

    [Test]
    public void EnemyGroupState_IsActiveOnlyForSpawningAndActive()
    {
        EnemyGroupState group =
            new EnemyGroupState();

        group.LifecycleState =
            EnemyGroupLifecycleState.None;

        Assert.That(group.IsActive(), Is.False);

        group.LifecycleState =
            EnemyGroupLifecycleState.Spawning;

        Assert.That(group.IsActive(), Is.True);

        group.LifecycleState =
            EnemyGroupLifecycleState.Active;

        Assert.That(group.IsActive(), Is.True);

        group.LifecycleState =
            EnemyGroupLifecycleState.Destroyed;

        Assert.That(group.IsActive(), Is.False);

        group.LifecycleState =
            EnemyGroupLifecycleState.Despawned;

        Assert.That(group.IsActive(), Is.False);
    }

    [Test]
    public void InvasionState_IsActiveOnlyForPreparingAndActive()
    {
        InvasionState invasion =
            new InvasionState();

        invasion.LifecycleState =
            InvasionLifecycleState.None;

        Assert.That(invasion.IsActive(), Is.False);

        invasion.LifecycleState =
            InvasionLifecycleState.Preparing;

        Assert.That(invasion.IsActive(), Is.True);

        invasion.LifecycleState =
            InvasionLifecycleState.Active;

        Assert.That(invasion.IsActive(), Is.True);

        invasion.LifecycleState =
            InvasionLifecycleState.Resolved;

        Assert.That(invasion.IsActive(), Is.False);

        invasion.LifecycleState =
            InvasionLifecycleState.Cancelled;

        Assert.That(invasion.IsActive(), Is.False);
    }

    [Test]
    public void WarState_JsonRoundTrip_PreservesFactionsGroupsInvasionsAndTimers()
    {
        GameRuntimeState source =
            new GameRuntimeState();

        source.Galaxy.EnemyFactions.Add(
            new EnemyFactionState
            {
                FactionId = "ai",
                DisplayName = "AI",
                OwnedSystemIds = new List<string>
                {
                    "system_source"
                },
                TerritorySystemIds = new List<string>
                {
                    "system_target"
                },
                ActiveGroupRuntimeIds = new List<string>
                {
                    "group_01"
                },
                ActiveInvasionIds = new List<string>
                {
                    "invasion_01"
                }
            });

        source.Galaxy.EnemyGroups.Add(
            new EnemyGroupState
            {
                RuntimeGroupId = "group_01",
                FactionId = "ai",
                SystemId = "system_target",
                SpawnRuleId = "enemy_group_spawn_ai_01",
                InvasionId = "invasion_01",
                Level = 4,
                LifecycleState = EnemyGroupLifecycleState.Active,
                MemberRuntimeNpcIds = new List<string>
                {
                    "npc_enemy_01",
                    "npc_enemy_02"
                },
                MemberConfigIds = new List<string>
                {
                    "enemy_ai_L04_01"
                },
                CreatedAtTick = 10,
                LastUpdatedTick = 12,
                NextSpawnTick = 20,
                CleanupAfterTick = 0,
                OwnerSystemId = "system_source",
                TargetSystemId = "system_target"
            });

        source.Galaxy.Invasions.Add(
            new InvasionState
            {
                InvasionId = "invasion_01",
                FactionId = "ai",
                SourceSystemId = "system_source",
                TargetSystemId = "system_target",
                Level = 4,
                LifecycleState = InvasionLifecycleState.Active,
                EnemyGroupRuntimeIds = new List<string>
                {
                    "group_01"
                },
                StartedAtTick = 10,
                LastUpdatedTick = 12,
                NextUpdateTick = 13,
                ResolveAtTick = 20,
                CleanupAfterTick = 0
            });

        string json =
            JsonUtility.ToJson(source);

        GameRuntimeState restored =
            JsonUtility.FromJson<GameRuntimeState>(json);

        Assert.That(restored.Galaxy.EnemyFactions.Count, Is.EqualTo(1));
        Assert.That(restored.Galaxy.EnemyGroups.Count, Is.EqualTo(1));
        Assert.That(restored.Galaxy.Invasions.Count, Is.EqualTo(1));

        Assert.That(restored.Galaxy.EnemyFactions[0].FactionId, Is.EqualTo("ai"));
        Assert.That(restored.Galaxy.EnemyFactions[0].ActiveInvasionIds[0], Is.EqualTo("invasion_01"));

        Assert.That(restored.Galaxy.EnemyGroups[0].RuntimeGroupId, Is.EqualTo("group_01"));
        Assert.That(restored.Galaxy.EnemyGroups[0].Level, Is.EqualTo(4));
        Assert.That(restored.Galaxy.EnemyGroups[0].LifecycleState, Is.EqualTo(EnemyGroupLifecycleState.Active));
        Assert.That(restored.Galaxy.EnemyGroups[0].MemberRuntimeNpcIds.Count, Is.EqualTo(2));

        Assert.That(restored.Galaxy.Invasions[0].InvasionId, Is.EqualTo("invasion_01"));
        Assert.That(restored.Galaxy.Invasions[0].NextUpdateTick, Is.EqualTo(13));
        Assert.That(restored.Galaxy.Invasions[0].ResolveAtTick, Is.EqualTo(20));
        Assert.That(restored.Galaxy.Invasions[0].IsActive(), Is.True);
    }

    [Test]
    public void SaveMigration_SystemNpcPersistentState_AddsWarStateCollections()
    {
        GameRuntimeState state =
            new GameRuntimeState();

        state.Meta.SaveDataVersion =
            SaveDataVersions.SystemNpcPersistentState;

        state.Galaxy.EnemyFactions = null;
        state.Galaxy.EnemyGroups = null;
        state.Galaxy.Invasions = null;

        bool changed =
            SaveMigrationService.Migrate(state);

        Assert.That(changed, Is.True);
        Assert.That(state.Meta.SaveDataVersion, Is.EqualTo(SaveDataVersions.Current));
        Assert.That(state.Galaxy.EnemyFactions, Is.Not.Null);
        Assert.That(state.Galaxy.EnemyGroups, Is.Not.Null);
        Assert.That(state.Galaxy.Invasions, Is.Not.Null);
    }

    [Test]
    public void SaveValidation_CancelsDuplicateActiveInvasionsForSameTarget()
    {
        GameRuntimeState state =
            new GameRuntimeState();

        state.Galaxy.Invasions.Add(
            new InvasionState
            {
                InvasionId = "invasion_first",
                FactionId = "ai",
                SourceSystemId = "system_source_a",
                TargetSystemId = "system_target",
                LifecycleState = InvasionLifecycleState.Active,
                Level = 4,
                EnemyGroupRuntimeIds = new List<string>
                {
                    "group_first"
                }
            });

        state.Galaxy.Invasions.Add(
            new InvasionState
            {
                InvasionId = "invasion_duplicate",
                FactionId = "ai",
                SourceSystemId = "system_source_b",
                TargetSystemId = "system_target",
                LifecycleState = InvasionLifecycleState.Active,
                Level = 4,
                EnemyGroupRuntimeIds = new List<string>
                {
                    "group_duplicate"
                }
            });

        SaveValidationResult result =
            new SaveValidationStage()
                .ValidateAndNormalize(state);

        Assert.That(result.IsValid, Is.True);
        Assert.That(state.Galaxy.Invasions.Count, Is.EqualTo(2));
        Assert.That(state.Galaxy.Invasions[0].LifecycleState, Is.EqualTo(InvasionLifecycleState.Active));
        Assert.That(state.Galaxy.Invasions[1].LifecycleState, Is.EqualTo(InvasionLifecycleState.Cancelled));
    }

    [Test]
    public void EnemyServices_ExposeRequiredStage2AContracts()
    {
        Assert.That(typeof(IEnemyFactionService).GetMethod("GetOrCreateFaction"), Is.Not.Null);
        Assert.That(typeof(IEnemyFactionService).GetMethod("AddOwnedSystem"), Is.Not.Null);
        Assert.That(typeof(IEnemyFactionService).GetMethod("RegisterEnemyGroup"), Is.Not.Null);
        Assert.That(typeof(IEnemyFactionService).GetMethod("RegisterInvasion"), Is.Not.Null);

        Assert.That(typeof(IEnemySpawnService).GetMethod("TrySpawnSystemGuardGroup"), Is.Not.Null);
        Assert.That(typeof(IEnemySpawnService).GetMethod("TrySpawnInvasionGroup"), Is.Not.Null);
        Assert.That(typeof(IEnemySpawnService).GetMethod("MarkEnemyGroupDestroyed"), Is.Not.Null);

        Assert.That(typeof(IInvasionService).GetMethod("TryStartInvasion"), Is.Not.Null);
        Assert.That(typeof(IInvasionService).GetMethod("UpdateInvasions"), Is.Not.Null);
        Assert.That(typeof(IInvasionService).GetMethod("ResolveInvasion"), Is.Not.Null);
        Assert.That(typeof(IInvasionService).GetMethod("CancelInvasion"), Is.Not.Null);
        Assert.That(typeof(IInvasionService).GetMethod("CleanupInvasion"), Is.Not.Null);
    }

    private static EnemyConfig CreateEnemyConfigWithLevel(
        int level)
    {
        EnemyConfig enemy =
            ScriptableObject.CreateInstance<EnemyConfig>();

        SetPrivateField(
            enemy,
            "id",
            "enemy_test_L" + level.ToString("00"));

        SetPrivateField(
            enemy,
            "level",
            level);

        return enemy;
    }

    private static EnemyGroupEntryConfig CreateEnemyGroupEntry(
        EnemyConfig enemy,
        int minCount,
        int maxCount)
    {
        EnemyGroupEntryConfig entry =
            new EnemyGroupEntryConfig();

        SetPrivateField(
            entry,
            "enemyConfig",
            enemy);

        SetPrivateField(
            entry,
            "minCount",
            minCount);

        SetPrivateField(
            entry,
            "maxCount",
            maxCount);

        SetPrivateField(
            entry,
            "weight",
            1);

        return entry;
    }

    private static EnemyGroupSpawnOptionConfig CreateEnemyGroupOption(
        int weight,
        params EnemyGroupEntryConfig[] enemies)
    {
        EnemyGroupSpawnOptionConfig option =
            new EnemyGroupSpawnOptionConfig();

        SetPrivateField(
            option,
            "weight",
            weight);

        SetPrivateField(
            option,
            "enemies",
            enemies);

        return option;
    }

    private static EnemyGroupSpawnLevelEntryConfig CreateEnemyGroupLevelEntry(
        int galaxyLevel,
        params EnemyGroupSpawnOptionConfig[] enemyGroups)
    {
        EnemyGroupSpawnLevelEntryConfig entry =
            new EnemyGroupSpawnLevelEntryConfig();

        SetPrivateField(
            entry,
            "galaxyLevel",
            galaxyLevel);

        SetPrivateField(
            entry,
            "spawnIntervalSeconds",
            1f);

        SetPrivateField(
            entry,
            "maxAliveGroupsFromThisRule",
            1);

        SetPrivateField(
            entry,
            "enemyGroups",
            enemyGroups);

        return entry;
    }

    private static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        System.Type currentType =
            target.GetType();

        FieldInfo field = null;

        while (currentType != null &&
               field == null)
        {
            field =
                currentType.GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

            currentType =
                currentType.BaseType;
        }

        Assert.That(
            field,
            Is.Not.Null,
            "Missing private field: " + fieldName);

        field.SetValue(
            target,
            value);
    }
}