using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Sprint 1 regression on the CURRENT state schema (including Sprint 6 war blocks).
/// Does not replace the existing SaveService2A file/backup/integrity tests.
/// </summary>
public sealed class S01_SavePipelineCrossSprintRegressionTests
{
    [Test]
    public void FutureSaveVersion_IsRejected_BeforeNormalization()
    {
        var state = new GameRuntimeState();
        state.Meta.SaveDataVersion = SaveDataVersions.Current + 1;

        SaveValidationResult result = new SaveValidationStage().ValidateAndNormalize(state);

        Assert.That(result.IsValid, Is.False);
        Assert.That(state.Meta.SaveDataVersion, Is.EqualTo(SaveDataVersions.Current + 1));
    }

    [Test]
    public void LegacyState_WithMissingWarLists_IsMigratedWithoutLosingPlayerProgress()
    {
        var state = new GameRuntimeState();
        state.Meta.SaveDataVersion = SaveDataVersions.SystemNpcPersistentState;
        state.Player.Credits = 2500;
        state.Player.Level = 3;
        state.Galaxy.EnemyFactions = null;
        state.Galaxy.EnemyGroups = null;
        state.Galaxy.Invasions = null;

        SaveValidationResult result = new SaveValidationStage().ValidateAndNormalize(state);

        Assert.That(result.IsValid, Is.True);
        Assert.That(state.Meta.SaveDataVersion, Is.EqualTo(SaveDataVersions.Current));
        Assert.That(state.Player.Credits, Is.EqualTo(2500));
        Assert.That(state.Player.Level, Is.EqualTo(3));
        Assert.That(state.Galaxy.EnemyFactions, Is.Not.Null);
        Assert.That(state.Galaxy.EnemyGroups, Is.Not.Null);
        Assert.That(state.Galaxy.Invasions, Is.Not.Null);
    }

    [Test]
    public void CorruptedPlayerNumbers_AreClampedAndRepeatedValidationIsStable()
    {
        var state = new GameRuntimeState();
        state.Meta.SaveDataVersion = SaveDataVersions.Current;
        state.Player.Level = -8;
        state.Player.Credits = -100;
        state.Player.Experience = -20;

        var validator = new SaveValidationStage();
        Assert.That(validator.ValidateAndNormalize(state).IsValid, Is.True);
        Assert.That(state.Player.Level, Is.EqualTo(1));
        Assert.That(state.Player.Credits, Is.Zero);
        Assert.That(state.Player.Experience, Is.Zero);

        string afterFirstValidation = JsonUtility.ToJson(state);
        Assert.That(validator.ValidateAndNormalize(state).IsValid, Is.True);
        Assert.That(JsonUtility.ToJson(state), Is.EqualTo(afterFirstValidation));
    }

    [Test]
    public void NullSave_IsRejectedWithoutThrowing()
    {
        Assert.That(
            new SaveValidationStage().ValidateAndNormalize(null).IsValid,
            Is.False);
    }
}
