using NUnit.Framework;
using System.IO;

public sealed class S04_03_BeamWeaponRuntimeTests
{
    [Test]
    public void BeamRuntimeState_Exists()
    {
        AssertFileExists(
            "Assets/_Project/Scripts/Data/Runtime/Combat/CombatBeamRuntimeState2A.cs");
    }

    [Test]
    public void BeamEvents_Exist()
    {
        AssertFileExists(
            "Assets/_Project/Scripts/Data/Events/Combat/CombatBeamStartedEvent2A.cs");

        AssertFileExists(
            "Assets/_Project/Scripts/Data/Events/Combat/CombatBeamEndedEvent2A.cs");
    }

    [Test]
    public void WeaponRuntimeStats_HasShotTypeAndShotCount()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Data/Runtime/WeaponRuntimeStats.cs");

        Assert.That(text, Does.Contain("WeaponShotType2A ShotType"));
        Assert.That(text, Does.Contain("int ShotCount"));
    }

    [Test]
    public void BeamView_UsesLineRendererAndDoesNotApplyDamage()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Presentation/Combat/CombatBeamView2A.cs");

        Assert.That(text, Does.Contain("LineRenderer"));
        Assert.That(text, Does.Contain("SetEndpoints"));
        Assert.That(text, Does.Contain("TryGetBeam"));

        Assert.That(text, Does.Not.Contain("ApplyDamage"));
        Assert.That(text, Does.Not.Contain("OnTriggerEnter2D"));
    }

    [Test]
    public void BeamRuntimeSettings_DefaultsToProductionTickDuration()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Data/Runtime/Combat/CombatBeamRuntimeSettings2A.cs");

        Assert.That(text, Does.Contain("DefaultBeamTickDuration01 = 0.9f"));
        Assert.That(text, Does.Contain("Mathf.Clamp"));
        Assert.That(text, Does.Contain("ResetToDefault"));
    }

    [Test]
    public void CombatService_BeamDamageStaysInRuntimeService()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Npc/SystemNpcCombatService.cs");

        Assert.That(text, Does.Contain("TryCreateBeam"));
        Assert.That(text, Does.Contain("ApplyDueBeamDamage"));
        Assert.That(text, Does.Contain("ApplyBeamDamage"));
        Assert.That(text, Does.Contain("GetBeamDamagePortion"));
        Assert.That(text, Does.Contain("CombatBeamRuntimeSettings2A.BeamTickDuration01"));
        Assert.That(text, Does.Contain("new CombatBeamStartedEvent2A"));
        Assert.That(text, Does.Contain("new CombatBeamEndedEvent2A"));
    }

    private static void AssertFileExists(string projectPath)
    {
        Assert.That(File.Exists(projectPath), Is.True, "Missing file: " + projectPath);
    }

    private static string ReadProjectFile(string projectPath)
    {
        AssertFileExists(projectPath);
        return File.ReadAllText(projectPath);
    }
}