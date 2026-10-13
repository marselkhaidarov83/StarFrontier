using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Sprint 5: population snapshot is the current save contract; Npcs is legacy-only.
/// </summary>
public sealed class S05_PopulationSnapshotPersistenceRegressionTests
{
    [Test]
    public void CurrentPopulationSnapshots_PreserveDifferentSystemsAndRoles()
    {
        var source = new SystemNpcSimulationSaveData();
        source.PopulationEntries.Add(new SystemNpcPopulationSnapshotEntrySaveData
        {
            SystemId = "system_a", NpcType = SystemNpcType.Ally,
            AllyRole = AllyRole2A.Military, ConfigId = "ally_military_L04",
            SpawnRuleId = "spawn_a", Count = 3, GroupCount = 1
        });
        source.PopulationEntries.Add(new SystemNpcPopulationSnapshotEntrySaveData
        {
            SystemId = "system_b", NpcType = SystemNpcType.Enemy,
            ConfigId = "enemy_ai_L04", SpawnRuleId = "spawn_b",
            Count = 5, GroupCount = 2
        });
        source.PopulationTimers.Add(new SystemPopulationRuleTimerState("system_b", "spawn_b")
        {
            NextSpawnTick = 240, TimerSeconds = 1.5f
        });

        SystemNpcSimulationSaveData loaded =
            JsonUtility.FromJson<SystemNpcSimulationSaveData>(JsonUtility.ToJson(source));

        Assert.That(loaded.PopulationEntries.Count, Is.EqualTo(2));
        Assert.That(loaded.PopulationEntries[0].SystemId, Is.EqualTo("system_a"));
        Assert.That(loaded.PopulationEntries[0].AllyRole, Is.EqualTo(AllyRole2A.Military));
        Assert.That(loaded.PopulationEntries[0].Count, Is.EqualTo(3));
        Assert.That(loaded.PopulationEntries[1].SystemId, Is.EqualTo("system_b"));
        Assert.That(loaded.PopulationEntries[1].GroupCount, Is.EqualTo(2));
        Assert.That(loaded.PopulationTimers[0].NextSpawnTick, Is.EqualTo(240));
        Assert.That(loaded.Npcs, Is.Empty,
            "Current snapshots should not require populating the legacy per-NPC list.");
    }

    [Test]
    public void LegacyNpcPayload_StillRoundTripsWithoutCreatingPopulationSnapshots()
    {
        var source = new SystemNpcSimulationSaveData();
        source.Npcs.Add(new SystemNpcSaveData
        {
            RuntimeNpcId = "npc_legacy", NpcType = SystemNpcType.Enemy,
            GroupRuntimeId = "group_1", Level = 4,
            CurrentHull = 0, LifeState = SystemNpcLifeState.Destroyed,
            IsAlive = false, DestroyedAtTick = 120, NextRespawnTick = 190
        });

        SystemNpcSimulationSaveData loaded =
            JsonUtility.FromJson<SystemNpcSimulationSaveData>(JsonUtility.ToJson(source));

        Assert.That(loaded.Npcs.Count, Is.EqualTo(1));
        Assert.That(loaded.Npcs[0].GroupRuntimeId, Is.EqualTo("group_1"));
        Assert.That(loaded.Npcs[0].LifeState, Is.EqualTo(SystemNpcLifeState.Destroyed));
        Assert.That(loaded.Npcs[0].NextRespawnTick, Is.EqualTo(190));
        Assert.That(loaded.PopulationEntries, Is.Empty);
    }

    [Test]
    public void SnapshotCollections_AreIsolatedBetweenSaveInstances()
    {
        var one = new SystemNpcSimulationSaveData();
        var two = new SystemNpcSimulationSaveData();
        one.PopulationEntries.Add(new SystemNpcPopulationSnapshotEntrySaveData
        {
            SystemId = "system_one", Count = 2
        });

        Assert.That(two.PopulationEntries, Is.Empty);
        Assert.That(two.PopulationEntries, Is.Not.SameAs(one.PopulationEntries));
        Assert.That(two.PopulationTimers, Is.Not.SameAs(one.PopulationTimers));
    }
}
