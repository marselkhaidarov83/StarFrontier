using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>2A-S06-01-T07/T08: war Save/Load, sanitation and duplicate active invasions.</summary>
public sealed class S06_01_WarSaveAndDuplicateInvasionTests
{
    [Test]
    public void SaveValidation_CancelsOnlyDuplicateActiveTarget_NotIndependentSystems()
    {
        var state = new GameRuntimeState();
        state.Meta.SaveDataVersion = SaveDataVersions.Current;
        state.Galaxy.Invasions.Add(Create("inv_a_1", "system_a", InvasionLifecycleState.Active));
        state.Galaxy.Invasions.Add(Create("inv_a_2", "system_a", InvasionLifecycleState.Preparing));
        state.Galaxy.Invasions.Add(Create("inv_b_1", "system_b", InvasionLifecycleState.Active));

        SaveValidationResult result = new SaveValidationStage().ValidateAndNormalize(state);

        Assert.That(result.IsValid, Is.True);
        Assert.That(state.Galaxy.Invasions.Count, Is.EqualTo(3));
        Assert.That(state.Galaxy.Invasions[0].LifecycleState, Is.EqualTo(InvasionLifecycleState.Active));
        Assert.That(state.Galaxy.Invasions[1].LifecycleState, Is.EqualTo(InvasionLifecycleState.Cancelled));
        Assert.That(state.Galaxy.Invasions[2].LifecycleState, Is.EqualTo(InvasionLifecycleState.Active));
        Assert.That(state.Galaxy.CountActiveInvasions(), Is.EqualTo(2));
    }

    [Test]
    public void BrokenWarCollections_AreRepairedWithoutDuplicateRecords()
    {
        var state = new GameRuntimeState();
        state.Meta.SaveDataVersion = SaveDataVersions.Current;
        state.Galaxy.EnemyFactions = new List<EnemyFactionState> { null,
            new EnemyFactionState { FactionId = "ai" } };
        state.Galaxy.EnemyGroups = new List<EnemyGroupState> { null,
            new EnemyGroupState { RuntimeGroupId = "group_a", Level = 3 } };
        state.Galaxy.Invasions = new List<InvasionState> { null,
            Create("inv_a", "system_a", InvasionLifecycleState.Active) };

        var validator = new SaveValidationStage();
        Assert.That(validator.ValidateAndNormalize(state).IsValid, Is.True);
        Assert.That(state.Galaxy.EnemyFactions.Count, Is.EqualTo(1));
        Assert.That(state.Galaxy.EnemyGroups.Count, Is.EqualTo(1));
        Assert.That(state.Galaxy.Invasions.Count, Is.EqualTo(1));

        Assert.That(validator.ValidateAndNormalize(state).IsValid, Is.True);
        Assert.That(state.Galaxy.EnemyFactions.Count, Is.EqualTo(1));
        Assert.That(state.Galaxy.EnemyGroups.Count, Is.EqualTo(1));
        Assert.That(state.Galaxy.Invasions.Count, Is.EqualTo(1));
    }

    [Test]
    public void ActiveWarEntitiesAndTimers_SurviveJsonAndValidation()
    {
        var source = new GameRuntimeState();
        source.Meta.SaveDataVersion = SaveDataVersions.Current;
        source.Galaxy.EnemyGroups.Add(new EnemyGroupState
        {
            RuntimeGroupId = "group_ai", FactionId = "ai", SystemId = "system_a",
            Level = 4, LifecycleState = EnemyGroupLifecycleState.Active,
            CreatedAtTick = 2, NextSpawnTick = 200,
            MemberRuntimeNpcIds = new List<string> { "npc_1", "npc_2" }
        });
        source.Galaxy.Invasions.Add(new InvasionState
        {
            InvasionId = "inv_ai", FactionId = "ai", TargetSystemId = "system_b",
            LifecycleState = InvasionLifecycleState.Preparing,
            ResolveAtTick = 300, NextUpdateTick = 201,
            EnemyGroupRuntimeIds = new List<string> { "group_ai" }
        });

        var loaded = JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(source));
        Assert.That(new SaveValidationStage().ValidateAndNormalize(loaded).IsValid, Is.True);

        Assert.That(loaded.Galaxy.EnemyGroups[0].NextSpawnTick, Is.EqualTo(200));
        Assert.That(loaded.Galaxy.EnemyGroups[0].MemberRuntimeNpcIds.Count, Is.EqualTo(2));
        Assert.That(loaded.Galaxy.Invasions[0].ResolveAtTick, Is.EqualTo(300));
        Assert.That(loaded.Galaxy.Invasions[0].NextUpdateTick, Is.EqualTo(201));
        Assert.That(loaded.Galaxy.Invasions[0].IsActive(), Is.True);
    }

    private static InvasionState Create(string id, string target, InvasionLifecycleState lifecycle)
    {
        return new InvasionState
        {
            InvasionId = id, TargetSystemId = target, FactionId = "ai",
            LifecycleState = lifecycle, Level = 4
        };
    }
}
