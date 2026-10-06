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
        Assert.That(typeof(IInvasionService).GetMethod("ProcessOfflineWarCatchUp"), Is.Not.Null);

        Assert.That(typeof(IInvasionService).GetMethod("CanCaptureSystemWithoutHopelessCollapse"), Is.Not.Null);

        Assert.That(typeof(IEnemyFactionService).GetMethod("RefreshFactionFrontline"), Is.Not.Null);
        Assert.That(typeof(IEnemyFactionService).GetMethod("IsFrontlineSystem"), Is.Not.Null);
        Assert.That(typeof(IEnemyFactionService).GetMethod("IsFrontierSystem"), Is.Not.Null);

        Assert.That(typeof(IInvasionService).GetMethod("GetActiveInvasionCount"), Is.Not.Null);
        Assert.That(typeof(IInvasionService).GetMethod("CanStartInvasion"), Is.Not.Null);

        Assert.That(typeof(IInvasionService).GetMethod("MarkSystemThreat"), Is.Not.Null);

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

    [Test]
    public void StarSystemStatus_ThreatenedAlias_PreservesLegacyThreatValue()
    {
        Assert.That(
            (int)StarSystemStatus.Threatened,
            Is.EqualTo((int)StarSystemStatus.Threat));
    }

    [Test]
    public void StarSystemRuntimeState_WarStatusHelpers_CoverThreatInvasionCapturedAndRecoveryReady()
    {
        StarSystemRuntimeState systemState =
            new StarSystemRuntimeState
            {
                SystemId = "system_target",
                SystemStatus = StarSystemStatus.Stable
            };

        Assert.That(systemState.IsUnderWarPressure(), Is.False);
        Assert.That(systemState.IsRecoveryReady(), Is.False);
        Assert.That(systemState.IsSecured(0), Is.True);

        systemState.MarkThreat();

        Assert.That(systemState.SystemStatus, Is.EqualTo(StarSystemStatus.Threat));
        Assert.That(systemState.IsUnderWarPressure(), Is.True);
        Assert.That(systemState.IsSecured(0), Is.False);

        systemState.MarkInvasion();

        Assert.That(systemState.SystemStatus, Is.EqualTo(StarSystemStatus.Invasion));
        Assert.That(systemState.IsUnderWarPressure(), Is.True);

        systemState.MarkCaptured();

        Assert.That(systemState.SystemStatus, Is.EqualTo(StarSystemStatus.Captured));
        Assert.That(systemState.IsUnderWarPressure(), Is.True);

        systemState.MarkRecoveryReady();

        Assert.That(systemState.SystemStatus, Is.EqualTo(StarSystemStatus.RecoveryReady));
        Assert.That(systemState.IsUnderWarPressure(), Is.False);
        Assert.That(systemState.IsRecoveryReady(), Is.True);
        Assert.That(systemState.IsSecured(0), Is.False);
    }

    [Test]
    public void WarState_JsonRoundTrip_PreservesSystemWarStatus()
    {
        GameRuntimeState source =
            new GameRuntimeState();

        source.Galaxy.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_target",
                IsDiscovered = true,
                IsVisited = true,
                DevelopmentLevel = 1,
                DangerLevel = 0,
                Stability = 100,
                SystemStatus = StarSystemStatus.RecoveryReady
            });

        string json =
            JsonUtility.ToJson(source);

        GameRuntimeState restored =
            JsonUtility.FromJson<GameRuntimeState>(json);

        Assert.That(restored.Galaxy.Systems.Count, Is.EqualTo(1));
        Assert.That(restored.Galaxy.Systems[0].SystemId, Is.EqualTo("system_target"));
        Assert.That(restored.Galaxy.Systems[0].SystemStatus, Is.EqualTo(StarSystemStatus.RecoveryReady));
        Assert.That(restored.Galaxy.Systems[0].IsRecoveryReady(), Is.True);
    }

    [Test]
    public void WarTransition_StateFlow_CoversThreatInvasionCaptured()
    {
        StarSystemRuntimeState systemState =
            new StarSystemRuntimeState
            {
                SystemId = "system_target",
                SystemStatus = StarSystemStatus.Stable
            };

        systemState.MarkThreat();

        Assert.That(systemState.SystemStatus, Is.EqualTo(StarSystemStatus.Threat));
        Assert.That(systemState.IsUnderWarPressure(), Is.True);

        systemState.MarkInvasion();

        Assert.That(systemState.SystemStatus, Is.EqualTo(StarSystemStatus.Invasion));
        Assert.That(systemState.IsUnderWarPressure(), Is.True);

        systemState.MarkCaptured();

        Assert.That(systemState.SystemStatus, Is.EqualTo(StarSystemStatus.Captured));
        Assert.That(systemState.IsUnderWarPressure(), Is.True);
        Assert.That(systemState.IsSecured(0), Is.False);
    }

    [Test]
    public void WarTransition_StateFlow_CoversThreatInvasionRecoveryReady()
    {
        StarSystemRuntimeState systemState =
            new StarSystemRuntimeState
            {
                SystemId = "system_target",
                SystemStatus = StarSystemStatus.Stable
            };

        systemState.MarkThreat();

        Assert.That(systemState.SystemStatus, Is.EqualTo(StarSystemStatus.Threat));

        systemState.MarkInvasion();

        Assert.That(systemState.SystemStatus, Is.EqualTo(StarSystemStatus.Invasion));

        systemState.MarkRecoveryReady();

        Assert.That(systemState.SystemStatus, Is.EqualTo(StarSystemStatus.RecoveryReady));
        Assert.That(systemState.IsRecoveryReady(), Is.True);
        Assert.That(systemState.IsUnderWarPressure(), Is.False);
        Assert.That(systemState.IsSecured(0), Is.False);
    }

    [Test]
    public void GalaxyRuntimeState_AllowsActiveInvasionsForDifferentTargetsUntilLimit()
    {
        GalaxyRuntimeState galaxyState =
            new GalaxyRuntimeState();

        galaxyState.Invasions.Add(
            new InvasionState
            {
                InvasionId = "invasion_01",
                TargetSystemId = "system_alpha",
                LifecycleState = InvasionLifecycleState.Active
            });

        galaxyState.Invasions.Add(
            new InvasionState
            {
                InvasionId = "invasion_02",
                TargetSystemId = "system_beta",
                LifecycleState = InvasionLifecycleState.Preparing
            });

        Assert.That(galaxyState.CountActiveInvasions(), Is.EqualTo(2));
        Assert.That(galaxyState.HasActiveInvasionForTarget("system_alpha"), Is.True);
        Assert.That(galaxyState.HasActiveInvasionForTarget("system_gamma"), Is.False);
        Assert.That(galaxyState.CanStartAdditionalInvasion("system_gamma", 3), Is.True);
    }

    [Test]
    public void GalaxyRuntimeState_BlocksDuplicateTargetAndGlobalInvasionLimit()
    {
        GalaxyRuntimeState galaxyState =
            new GalaxyRuntimeState();

        galaxyState.Invasions.Add(
            new InvasionState
            {
                InvasionId = "invasion_01",
                TargetSystemId = "system_alpha",
                LifecycleState = InvasionLifecycleState.Active
            });

        galaxyState.Invasions.Add(
            new InvasionState
            {
                InvasionId = "invasion_02",
                TargetSystemId = "system_beta",
                LifecycleState = InvasionLifecycleState.Active
            });

        galaxyState.Invasions.Add(
            new InvasionState
            {
                InvasionId = "invasion_03",
                TargetSystemId = "system_gamma",
                LifecycleState = InvasionLifecycleState.Active
            });

        Assert.That(galaxyState.CanStartAdditionalInvasion("system_alpha", 3), Is.False);
        Assert.That(galaxyState.CanStartAdditionalInvasion("system_delta", 3), Is.False);
    }

    [Test]
    public void SaveValidation_KeepsActiveInvasionsForDifferentTargets()
    {
        GameRuntimeState state =
            new GameRuntimeState();

        state.Galaxy.Invasions.Add(
            new InvasionState
            {
                InvasionId = "invasion_alpha",
                TargetSystemId = "system_alpha",
                LifecycleState = InvasionLifecycleState.Active
            });

        state.Galaxy.Invasions.Add(
            new InvasionState
            {
                InvasionId = "invasion_beta",
                TargetSystemId = "system_beta",
                LifecycleState = InvasionLifecycleState.Active
            });

        SaveValidationResult result =
            new SaveValidationStage()
                .ValidateAndNormalize(state);

        Assert.That(result.IsValid, Is.True);
        Assert.That(state.Galaxy.Invasions.Count, Is.EqualTo(2));
        Assert.That(state.Galaxy.Invasions[0].LifecycleState, Is.EqualTo(InvasionLifecycleState.Active));
        Assert.That(state.Galaxy.Invasions[1].LifecycleState, Is.EqualTo(InvasionLifecycleState.Active));
    }

    [Test]
    public void EnemyFactionState_RebuildsFrontlineAndFrontierFromNeighborSystems()
    {
        EnemyFactionState faction =
            new EnemyFactionState
            {
                FactionId = "ai",
                OwnedSystemIds = new List<string>
                {
                    "system_owned"
                },
                TerritorySystemIds = new List<string>()
            };

        Dictionary<string, IReadOnlyList<string>> neighbors =
            new Dictionary<string, IReadOnlyList<string>>
            {
                {
                    "system_owned",
                    new List<string>
                    {
                        "system_frontier"
                    }
                },
                {
                    "system_frontier",
                    new List<string>
                    {
                        "system_owned"
                    }
                }
            };

        faction.RebuildFrontlineAndFrontier(neighbors);

        Assert.That(faction.IsFrontlineSystem("system_owned"), Is.True);
        Assert.That(faction.IsFrontierSystem("system_frontier"), Is.True);
        Assert.That(faction.HasOwnershipLink("system_frontier"), Is.False);
    }

    [Test]
    public void EnemyFactionState_FrontierDoesNotRequireRouteLock()
    {
        EnemyFactionState faction =
            new EnemyFactionState
            {
                FactionId = "infected",
                OwnedSystemIds = new List<string>
                {
                    "system_hive"
                },
                TerritorySystemIds = new List<string>
                {
                    "system_border"
                }
            };

        Dictionary<string, IReadOnlyList<string>> neighbors =
            new Dictionary<string, IReadOnlyList<string>>
            {
                {
                    "system_hive",
                    new List<string>
                    {
                        "system_border"
                    }
                }
            };

        faction.RebuildFrontlineAndFrontier(neighbors);

        Assert.That(faction.HasOwnershipLink("system_hive"), Is.True);
        Assert.That(faction.HasTerritoryLink("system_border"), Is.True);
        Assert.That(faction.IsFrontierSystem("system_border"), Is.True);
    }

    [Test]
    public void InvasionState_ApplyEscalation_UsesGalaxyLevelAndFactionRules()
    {
        InvasionState aiInvasion =
            new InvasionState();

        aiInvasion.ApplyEscalation(
            "ai",
            4);

        Assert.That(aiInvasion.Level, Is.EqualTo(4));
        Assert.That(aiInvasion.EscalationTier, Is.EqualTo(2));
        Assert.That(aiInvasion.EscalationPressure, Is.EqualTo(6));
        Assert.That(aiInvasion.EscalationRuleId, Is.EqualTo("ai_gl04_tier02"));
        Assert.That(aiInvasion.UsesPlayerPowerScaling, Is.False);

        InvasionState infectedInvasion =
            new InvasionState();

        infectedInvasion.ApplyEscalation(
            "infected",
            4);

        Assert.That(infectedInvasion.Level, Is.EqualTo(4));
        Assert.That(infectedInvasion.EscalationTier, Is.EqualTo(2));
        Assert.That(infectedInvasion.EscalationPressure, Is.EqualTo(8));
        Assert.That(infectedInvasion.EscalationRuleId, Is.EqualTo("infected_gl04_tier02"));
        Assert.That(infectedInvasion.UsesPlayerPowerScaling, Is.False);
    }

    [Test]
    public void InvasionState_ApplyEscalation_ClampsGalaxyLevelToOneTen()
    {
        InvasionState lowLevelInvasion =
            new InvasionState();

        lowLevelInvasion.ApplyEscalation(
            "ancients",
            -5);

        Assert.That(lowLevelInvasion.Level, Is.EqualTo(1));
        Assert.That(lowLevelInvasion.EscalationTier, Is.EqualTo(1));
        Assert.That(lowLevelInvasion.UsesPlayerPowerScaling, Is.False);

        InvasionState highLevelInvasion =
            new InvasionState();

        highLevelInvasion.ApplyEscalation(
            "ancients",
            99);

        Assert.That(highLevelInvasion.Level, Is.EqualTo(10));
        Assert.That(highLevelInvasion.EscalationTier, Is.EqualTo(3));
        Assert.That(highLevelInvasion.EscalationRuleId, Is.EqualTo("ancients_gl10_tier03"));
        Assert.That(highLevelInvasion.UsesPlayerPowerScaling, Is.False);
    }

    [Test]
    public void WarState_JsonRoundTrip_PreservesInvasionEscalation()
    {
        GameRuntimeState source =
            new GameRuntimeState();

        InvasionState invasion =
            new InvasionState
            {
                InvasionId = "invasion_escalation",
                FactionId = "infected",
                SourceSystemId = "system_source",
                TargetSystemId = "system_target",
                LifecycleState = InvasionLifecycleState.Active
            };

        invasion.ApplyEscalation(
            "infected",
            7);

        source.Galaxy.Invasions.Add(invasion);

        string json =
            JsonUtility.ToJson(source);

        GameRuntimeState restored =
            JsonUtility.FromJson<GameRuntimeState>(json);

        Assert.That(restored.Galaxy.Invasions.Count, Is.EqualTo(1));
        Assert.That(restored.Galaxy.Invasions[0].Level, Is.EqualTo(7));
        Assert.That(restored.Galaxy.Invasions[0].EscalationTier, Is.EqualTo(4));
        Assert.That(restored.Galaxy.Invasions[0].EscalationPressure, Is.EqualTo(13));
        Assert.That(restored.Galaxy.Invasions[0].EscalationRuleId, Is.EqualTo("infected_gl07_tier04"));
        Assert.That(restored.Galaxy.Invasions[0].UsesPlayerPowerScaling, Is.False);
    }

    [Test]
    public void GalaxyRuntimeState_BlocksCaptureOfLastRecoverableSystem()
    {
        GalaxyRuntimeState galaxyState =
            new GalaxyRuntimeState();

        galaxyState.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_last",
                SystemStatus = StarSystemStatus.Invasion
            });

        Assert.That(
            galaxyState.CanCaptureSystemWithoutHopelessCollapse("system_last"),
            Is.False);
    }

    [Test]
    public void GalaxyRuntimeState_AllowsCaptureWhenAnotherRecoverableSystemRemains()
    {
        GalaxyRuntimeState galaxyState =
            new GalaxyRuntimeState();

        galaxyState.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_target",
                SystemStatus = StarSystemStatus.Invasion
            });

        galaxyState.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_safe",
                SystemStatus = StarSystemStatus.Stable
            });

        Assert.That(
            galaxyState.CanCaptureSystemWithoutHopelessCollapse("system_target"),
            Is.True);
    }

    [Test]
    public void GalaxyRuntimeState_RepairHopelessCollapse_RestoresOneRecoveryReadySystem()
    {
        GalaxyRuntimeState galaxyState =
            new GalaxyRuntimeState();

        galaxyState.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_alpha",
                SystemStatus = StarSystemStatus.Captured
            });

        galaxyState.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_beta",
                SystemStatus = StarSystemStatus.Captured
            });

        bool repaired =
            galaxyState.RepairHopelessCollapse();

        Assert.That(repaired, Is.True);
        Assert.That(galaxyState.Systems[0].SystemStatus, Is.EqualTo(StarSystemStatus.RecoveryReady));
        Assert.That(galaxyState.Systems[1].SystemStatus, Is.EqualTo(StarSystemStatus.Captured));
    }

    [Test]
    public void SaveValidation_RepairsHopelessGalaxyCollapse()
    {
        GameRuntimeState state =
            new GameRuntimeState();

        state.Galaxy.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_alpha",
                SystemStatus = StarSystemStatus.Captured
            });

        state.Galaxy.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_beta",
                SystemStatus = StarSystemStatus.Captured
            });

        SaveValidationResult result =
            new SaveValidationStage()
                .ValidateAndNormalize(state);

        Assert.That(result.IsValid, Is.True);
        Assert.That(state.Galaxy.Systems[0].SystemStatus, Is.EqualTo(StarSystemStatus.RecoveryReady));
    }

    [Test]
    public void GalaxyRuntimeState_OfflineCatchUp_ResolvesExpiredActiveInvasion()
    {
        GalaxyRuntimeState galaxyState =
            new GalaxyRuntimeState();

        galaxyState.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_target",
                SystemStatus = StarSystemStatus.Invasion,
                Stability = 100
            });

        galaxyState.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_safe",
                SystemStatus = StarSystemStatus.Stable,
                Stability = 100
            });

        galaxyState.Invasions.Add(
            new InvasionState
            {
                InvasionId = "invasion_01",
                FactionId = "ai",
                TargetSystemId = "system_target",
                LifecycleState = InvasionLifecycleState.Active,
                ResolveAtTick = 5
            });

        int changedCount =
            galaxyState.ProcessOfflineWarCatchUp(5);

        Assert.That(changedCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(galaxyState.Invasions[0].LifecycleState, Is.EqualTo(InvasionLifecycleState.Resolved));
        Assert.That(galaxyState.Systems[0].SystemStatus, Is.EqualTo(StarSystemStatus.Captured));
    }

    [Test]
    public void GalaxyRuntimeState_OfflineCatchUp_ThreatDegradesIntoInvasion()
    {
        GalaxyRuntimeState galaxyState =
            new GalaxyRuntimeState();

        galaxyState.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_threat",
                SystemStatus = StarSystemStatus.Threat,
                Stability = 100
            });

        int changedCount =
            galaxyState.ProcessOfflineWarCatchUp(1);

        Assert.That(changedCount, Is.EqualTo(1));
        Assert.That(galaxyState.Systems[0].SystemStatus, Is.EqualTo(StarSystemStatus.Invasion));
    }

    [Test]
    public void GalaxyRuntimeState_OfflineCatchUp_DegradesCapturedSystemStability()
    {
        GalaxyRuntimeState galaxyState =
            new GalaxyRuntimeState();

        galaxyState.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_captured",
                SystemStatus = StarSystemStatus.Captured,
                Stability = 100
            });

        galaxyState.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_safe",
                SystemStatus = StarSystemStatus.Stable,
                Stability = 100
            });

        int changedCount =
            galaxyState.ProcessOfflineWarCatchUp(3);

        Assert.That(changedCount, Is.EqualTo(1));
        Assert.That(galaxyState.Systems[0].SystemStatus, Is.EqualTo(StarSystemStatus.Captured));
        Assert.That(galaxyState.Systems[0].Stability, Is.EqualTo(97));
    }

    [Test]
    public void GalaxyRuntimeState_OfflineCatchUp_RespectsHopelessnessBounds()
    {
        GalaxyRuntimeState galaxyState =
            new GalaxyRuntimeState();

        galaxyState.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_last",
                SystemStatus = StarSystemStatus.Invasion,
                Stability = 100
            });

        galaxyState.Invasions.Add(
            new InvasionState
            {
                InvasionId = "invasion_last",
                FactionId = "infected",
                TargetSystemId = "system_last",
                LifecycleState = InvasionLifecycleState.Active,
                ResolveAtTick = 3
            });

        galaxyState.ProcessOfflineWarCatchUp(3);

        Assert.That(galaxyState.Invasions[0].LifecycleState, Is.EqualTo(InvasionLifecycleState.Resolved));
        Assert.That(galaxyState.Systems[0].SystemStatus, Is.EqualTo(StarSystemStatus.RecoveryReady));
    }

    [Test]
    public void T08_MultiSystemInvasion_ResolvesDifferentTargetsWithoutStateOverwrite()
    {
        GalaxyRuntimeState galaxyState =
            new GalaxyRuntimeState();

        galaxyState.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_alpha",
                SystemStatus = StarSystemStatus.Invasion,
                Stability = 100
            });

        galaxyState.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_beta",
                SystemStatus = StarSystemStatus.Invasion,
                Stability = 100
            });

        galaxyState.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_safe",
                SystemStatus = StarSystemStatus.Stable,
                Stability = 100
            });

        galaxyState.Invasions.Add(
            new InvasionState
            {
                InvasionId = "invasion_alpha",
                FactionId = "ai",
                TargetSystemId = "system_alpha",
                LifecycleState = InvasionLifecycleState.Active,
                ResolveAtTick = 5
            });

        galaxyState.Invasions.Add(
            new InvasionState
            {
                InvasionId = "invasion_beta",
                FactionId = "infected",
                TargetSystemId = "system_beta",
                LifecycleState = InvasionLifecycleState.Active,
                ResolveAtTick = 5
            });

        int changedCount =
            galaxyState.ProcessOfflineWarCatchUp(5);

        Assert.That(changedCount, Is.GreaterThanOrEqualTo(2));
        Assert.That(galaxyState.Invasions[0].LifecycleState, Is.EqualTo(InvasionLifecycleState.Resolved));
        Assert.That(galaxyState.Invasions[1].LifecycleState, Is.EqualTo(InvasionLifecycleState.Resolved));
        Assert.That(galaxyState.Systems[0].SystemStatus, Is.EqualTo(StarSystemStatus.Captured));
        Assert.That(galaxyState.Systems[1].SystemStatus, Is.EqualTo(StarSystemStatus.Captured));
        Assert.That(galaxyState.Systems[2].SystemStatus, Is.EqualTo(StarSystemStatus.Stable));
    }

    [Test]
    public void T08_ScalingCurves_IncreaseByGalaxyLevelAndFactionWithoutPlayerPower()
    {
        InvasionState aiLow =
            new InvasionState();

        aiLow.ApplyEscalation(
            "ai",
            1);

        InvasionState aiHigh =
            new InvasionState();

        aiHigh.ApplyEscalation(
            "ai",
            10);

        InvasionState infectedHigh =
            new InvasionState();

        infectedHigh.ApplyEscalation(
            "infected",
            10);

        Assert.That(aiHigh.EscalationTier, Is.GreaterThan(aiLow.EscalationTier));
        Assert.That(aiHigh.EscalationPressure, Is.GreaterThan(aiLow.EscalationPressure));
        Assert.That(infectedHigh.EscalationPressure, Is.GreaterThan(aiHigh.EscalationPressure));

        Assert.That(aiLow.UsesPlayerPowerScaling, Is.False);
        Assert.That(aiHigh.UsesPlayerPowerScaling, Is.False);
        Assert.That(infectedHigh.UsesPlayerPowerScaling, Is.False);
    }

    [Test]
    public void T08_TerritoryTransitions_RebuildFrontlineAndFrontierAfterCapture()
    {
        EnemyFactionState faction =
            new EnemyFactionState
            {
                FactionId = "ai",
                OwnedSystemIds = new List<string>
                {
                    "system_core",
                    "system_captured"
                },
                TerritorySystemIds = new List<string>
                {
                    "system_core",
                    "system_captured"
                }
            };

        Dictionary<string, IReadOnlyList<string>> neighbors =
            new Dictionary<string, IReadOnlyList<string>>
            {
                {
                    "system_core",
                    new List<string>
                    {
                        "system_captured"
                    }
                },
                {
                    "system_captured",
                    new List<string>
                    {
                        "system_core",
                        "system_frontier"
                    }
                },
                {
                    "system_frontier",
                    new List<string>
                    {
                        "system_captured"
                    }
                }
            };

        faction.RebuildFrontlineAndFrontier(neighbors);

        Assert.That(faction.HasOwnershipLink("system_captured"), Is.True);
        Assert.That(faction.HasTerritoryLink("system_captured"), Is.True);
        Assert.That(faction.IsFrontlineSystem("system_captured"), Is.True);
        Assert.That(faction.IsFrontierSystem("system_frontier"), Is.True);
        Assert.That(faction.IsFrontlineSystem("system_core"), Is.False);
    }

    [Test]
    public void T08_AntiHopelessness_LastRecoverableSystemStaysRecoveryReady()
    {
        GalaxyRuntimeState galaxyState =
            new GalaxyRuntimeState();

        galaxyState.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_last",
                SystemStatus = StarSystemStatus.Invasion,
                Stability = 100
            });

        galaxyState.Invasions.Add(
            new InvasionState
            {
                InvasionId = "invasion_last",
                FactionId = "ancients",
                TargetSystemId = "system_last",
                LifecycleState = InvasionLifecycleState.Active,
                ResolveAtTick = 10
            });

        galaxyState.ProcessOfflineWarCatchUp(10);

        Assert.That(galaxyState.Invasions[0].LifecycleState, Is.EqualTo(InvasionLifecycleState.Resolved));
        Assert.That(galaxyState.Systems[0].SystemStatus, Is.EqualTo(StarSystemStatus.RecoveryReady));
        Assert.That(galaxyState.CanCaptureSystemWithoutHopelessCollapse("system_last"), Is.False);
    }

    [Test]
    public void T08_Performance_OfflineWarCatchUp_HandlesLargeStateWithinBudget()
    {
        GalaxyRuntimeState galaxyState =
            new GalaxyRuntimeState();

        for (int i = 0; i < 120; i++)
        {
            galaxyState.Systems.Add(
                new StarSystemRuntimeState
                {
                    SystemId = "system_" + i.ToString("000"),
                    SystemStatus = i % 3 == 0
                        ? StarSystemStatus.Invasion
                        : StarSystemStatus.Stable,
                    Stability = 100
                });
        }

        for (int i = 0; i < 40; i++)
        {
            galaxyState.Invasions.Add(
                new InvasionState
                {
                    InvasionId = "invasion_" + i.ToString("000"),
                    FactionId = i % 2 == 0 ? "ai" : "infected",
                    TargetSystemId = "system_" + i.ToString("000"),
                    LifecycleState = InvasionLifecycleState.Active,
                    ResolveAtTick = 10
                });
        }

        System.Diagnostics.Stopwatch stopwatch =
            System.Diagnostics.Stopwatch.StartNew();

        int changedCount =
            galaxyState.ProcessOfflineWarCatchUp(10);

        stopwatch.Stop();

        Assert.That(changedCount, Is.GreaterThan(0));
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(250));
        Assert.That(galaxyState.RepairHopelessCollapse(), Is.False);
    }

    [Test]
    public void S06_03_T08_StateTransitionTable_CoversCaptureLiberationAndRepeatedCapture()
    {
        StarSystemRuntimeState systemState =
            new StarSystemRuntimeState
            {
                SystemId = "system_target",
                SystemStatus = StarSystemStatus.Stable,
                Stability = 100,
                DevelopmentLevel = 2
            };

        Assert.That(systemState.IsSecured(0), Is.True);

        systemState.MarkThreat();

        Assert.That(systemState.SystemStatus, Is.EqualTo(StarSystemStatus.Threat));
        Assert.That(systemState.IsUnderWarPressure(), Is.True);

        systemState.MarkInvasion();

        Assert.That(systemState.SystemStatus, Is.EqualTo(StarSystemStatus.Invasion));
        Assert.That(systemState.IsUnderWarPressure(), Is.True);

        systemState.MarkCaptured();

        Assert.That(systemState.SystemStatus, Is.EqualTo(StarSystemStatus.Captured));
        Assert.That(systemState.IsSecured(0), Is.False);
        Assert.That(systemState.Stability, Is.EqualTo(75));
        Assert.That(systemState.DevelopmentLevel, Is.EqualTo(1));
        Assert.That(systemState.HasDamagedInfrastructure(), Is.True);

        systemState.MarkLiberatedByPlayer();
        systemState.MarkRecoveryHookPending(
            12,
            "player_liberation_after_invasion");

        Assert.That(systemState.SystemStatus, Is.EqualTo(StarSystemStatus.RecoveryReady));
        Assert.That(systemState.IsRecoveryReady(), Is.True);
        Assert.That(systemState.HasPendingRecoveryHook, Is.True);

        systemState.MarkThreat();
        systemState.MarkInvasion();
        systemState.MarkCaptured();

        Assert.That(systemState.SystemStatus, Is.EqualTo(StarSystemStatus.Captured));
        Assert.That(systemState.HasPendingRecoveryHook, Is.False);
        Assert.That(systemState.Stability, Is.EqualTo(50));
        Assert.That(systemState.DevelopmentLevel, Is.EqualTo(0));
    }

    [Test]
    public void S06_03_T08_CaptureSystem_AppliesInfrastructureDamageContract()
    {
        StarSystemRuntimeState systemState =
            new StarSystemRuntimeState
            {
                SystemId = "system_target",
                SystemStatus = StarSystemStatus.Invasion,
                Stability = 100,
                DevelopmentLevel = 3
            };

        systemState.MarkCaptured();

        Assert.That(systemState.SystemStatus, Is.EqualTo(StarSystemStatus.Captured));
        Assert.That(systemState.InfrastructureDamageState, Is.EqualTo(SystemInfrastructureDamageState.Damaged));
        Assert.That(systemState.InfrastructureDamage, Is.EqualTo(35));
        Assert.That(systemState.HasDamagedInfrastructure(), Is.True);
        Assert.That(systemState.HasDestroyedInfrastructure(), Is.False);
    }

    [Test]
    public void S06_03_T08_RepeatedCapture_CanDestroyInfrastructureWithoutNegativeValues()
    {
        StarSystemRuntimeState systemState =
            new StarSystemRuntimeState
            {
                SystemId = "system_target",
                SystemStatus = StarSystemStatus.RecoveryReady,
                Stability = 30,
                DevelopmentLevel = 1
            };

        systemState.MarkCaptured();
        systemState.MarkLiberatedByPlayer();
        systemState.MarkCaptured();
        systemState.MarkLiberatedByPlayer();
        systemState.MarkCaptured();

        Assert.That(systemState.SystemStatus, Is.EqualTo(StarSystemStatus.Captured));
        Assert.That(systemState.InfrastructureDamage, Is.EqualTo(100));
        Assert.That(systemState.InfrastructureDamageState, Is.EqualTo(SystemInfrastructureDamageState.Destroyed));
        Assert.That(systemState.Stability, Is.GreaterThanOrEqualTo(0));
        Assert.That(systemState.DevelopmentLevel, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void S06_03_T08_SaveLoad_PreservesCaptureLiberationInfrastructureAndRecoveryHook()
    {
        GameRuntimeState source =
            new GameRuntimeState();

        source.Galaxy.Systems.Add(
            new StarSystemRuntimeState
            {
                SystemId = "system_target",
                SystemStatus = StarSystemStatus.RecoveryReady,
                Stability = 10,
                DevelopmentLevel = 0,
                InfrastructureDamageState = SystemInfrastructureDamageState.Damaged,
                InfrastructureDamage = 35,
                HasPendingRecoveryHook = true,
                RecoveryHookCreatedAtTick = 12,
                RecoveryHookReason = "player_liberation_after_invasion"
            });

        string json =
            JsonUtility.ToJson(source);

        GameRuntimeState restored =
            JsonUtility.FromJson<GameRuntimeState>(json);

        StarSystemRuntimeState restoredSystem =
            restored.Galaxy.Systems[0];

        Assert.That(restoredSystem.SystemId, Is.EqualTo("system_target"));
        Assert.That(restoredSystem.SystemStatus, Is.EqualTo(StarSystemStatus.RecoveryReady));
        Assert.That(restoredSystem.InfrastructureDamageState, Is.EqualTo(SystemInfrastructureDamageState.Damaged));
        Assert.That(restoredSystem.InfrastructureDamage, Is.EqualTo(35));
        Assert.That(restoredSystem.HasPendingRecoveryHook, Is.True);
        Assert.That(restoredSystem.RecoveryHookCreatedAtTick, Is.EqualTo(12));
        Assert.That(restoredSystem.RecoveryHookReason, Is.EqualTo("player_liberation_after_invasion"));
    }

    [Test]
    public void S06_03_T08_RecoveryHook_ClearsWhenSystemIsCapturedAgain()
    {
        StarSystemRuntimeState systemState =
            new StarSystemRuntimeState
            {
                SystemId = "system_target",
                SystemStatus = StarSystemStatus.RecoveryReady,
                Stability = 25,
                DevelopmentLevel = 1
            };

        systemState.MarkRecoveryHookPending(
            20,
            "player_liberation_after_invasion");

        Assert.That(systemState.HasPendingRecoveryHook, Is.True);

        systemState.MarkCaptured();

        Assert.That(systemState.SystemStatus, Is.EqualTo(StarSystemStatus.Captured));
        Assert.That(systemState.HasPendingRecoveryHook, Is.False);
        Assert.That(systemState.RecoveryHookCreatedAtTick, Is.EqualTo(0));
        Assert.That(systemState.RecoveryHookReason, Is.Empty);
    }

    [Test]
    public void S06_03_T08_NoDuplicateWarOutcome_ResolvedInvasionIsNotActive()
    {
        InvasionState invasionState =
            new InvasionState
            {
                InvasionId = "invasion_target",
                TargetSystemId = "system_target",
                LifecycleState = InvasionLifecycleState.Active
            };

        Assert.That(invasionState.IsActive(), Is.True);

        invasionState.LifecycleState =
            InvasionLifecycleState.Resolved;

        Assert.That(invasionState.IsActive(), Is.False);

        invasionState.LifecycleState =
            InvasionLifecycleState.CleanedUp;

        Assert.That(invasionState.IsActive(), Is.False);
    }

    [Test]
    public void S06_03_T08_RequiredContracts_AreExposedForProductionServices()
    {
        Assert.That(typeof(ISystemSecurityService).GetMethod("CaptureSystem"), Is.Not.Null);
        Assert.That(typeof(ISystemSecurityService).GetMethod("LiberateSystemByPlayer"), Is.Not.Null);
        Assert.That(typeof(ISystemSecurityService).GetMethod("ApplyInfrastructureDamageFromWar"), Is.Not.Null);
        Assert.That(typeof(ISystemSecurityService).GetMethod("MarkRecoveryHookPending"), Is.Not.Null);
        Assert.That(typeof(ISystemSecurityService).GetMethod("IsNpcAutonomousLiberationAllowed"), Is.Not.Null);

        Assert.That(typeof(IInvasionService).GetMethod("ResolveInvasionFromCombatOutcome"), Is.Not.Null);
    }

}