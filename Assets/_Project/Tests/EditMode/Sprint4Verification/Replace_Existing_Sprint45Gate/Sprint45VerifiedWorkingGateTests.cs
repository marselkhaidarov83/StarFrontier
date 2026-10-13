using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Replaces a misleading "verified working" gate which counted [Test] attributes
/// and checked source-file presence. Passing these tests proves only these
/// actual EditMode behaviors, NOT Play Mode, Android or CANONICAL_COMPLETE.
/// </summary>
public sealed class Sprint45VerifiedWorkingGateTests
{
    [Test]
    public void CombatDamage_SpentShieldCannotAlsoAbsorbHullDamage()
    {
        CombatDamageResult2A result = new DamageService2A().ApplyDamage(3, 5, 6);

        Assert.That(result.ShieldDamage, Is.EqualTo(3));
        Assert.That(result.HullDamage, Is.EqualTo(3));
        Assert.That(result.AppliedDamage, Is.EqualTo(6));
        Assert.That(result.CurrentShield, Is.Zero);
        Assert.That(result.CurrentHull, Is.EqualTo(2));
    }

    [Test]
    public void NpcSave_ResumesWithCorrectDestroyedAndRespawnTimer()
    {
        var source = new SystemNpcSimulationSaveData();
        source.Npcs.Add(new SystemNpcSaveData
        {
            RuntimeNpcId = "npc_dead", NpcType = SystemNpcType.Enemy,
            LifeState = SystemNpcLifeState.Destroyed, IsAlive = false,
            DestroyedAtTick = 25, NextRespawnTick = 40
        });

        SystemNpcSimulationSaveData loaded =
            JsonUtility.FromJson<SystemNpcSimulationSaveData>(JsonUtility.ToJson(source));

        Assert.That(loaded.Npcs[0].LifeState, Is.EqualTo(SystemNpcLifeState.Destroyed));
        Assert.That(loaded.Npcs[0].IsAlive, Is.False);
        Assert.That(loaded.Npcs[0].NextRespawnTick, Is.EqualTo(40));
    }

    [Test]
    public void CurrentNpcPopulationSnapshot_AndLegacyNpcEntriesCanBeReadTogether()
    {
        var source = new SystemNpcSimulationSaveData();
        source.PopulationEntries.Add(new SystemNpcPopulationSnapshotEntrySaveData
        {
            SystemId = "system_a", Count = 5, GroupCount = 2
        });
        source.Npcs.Add(new SystemNpcSaveData { RuntimeNpcId = "legacy_1" });

        SystemNpcSimulationSaveData loaded =
            JsonUtility.FromJson<SystemNpcSimulationSaveData>(JsonUtility.ToJson(source));

        Assert.That(loaded.PopulationEntries.Count, Is.EqualTo(1));
        Assert.That(loaded.Npcs.Count, Is.EqualTo(1));
    }
}
