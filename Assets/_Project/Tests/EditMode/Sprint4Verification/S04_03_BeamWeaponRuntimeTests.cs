using NUnit.Framework;
using System.IO;

public sealed class S04_03_BeamWeaponRuntimeTests
{
    [Test]
    public void BeamRuntimeState_Exists()
    {
        Assert.IsTrue(File.Exists(
            "Assets/_Project/Scripts/Data/Runtime/Npc/CombatBeamRuntimeState2A.cs"));
    }

    [Test]
    public void BeamEvents_Exist()
    {
        Assert.IsTrue(File.Exists(
            "Assets/_Project/Scripts/Data/Events/Npc/CombatBeamStartedEvent2A.cs"));

        Assert.IsTrue(File.Exists(
            "Assets/_Project/Scripts/Data/Events/Npc/CombatBeamEndedEvent2A.cs"));
    }

    [Test]
    public void WeaponRuntimeStats_HasShotTypeAndShotCount()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Data/Runtime/WeaponRuntimeStats.cs");

        Assert.IsTrue(text.Contains("WeaponShotType2A ShotType"));
        Assert.IsTrue(text.Contains("int ShotCount"));
    }

    [Test]
    public void BeamView_UsesLineRendererAndDoesNotApplyDamage()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Presentation/Npc/CombatBeamView2A.cs");

        Assert.IsTrue(text.Contains("LineRenderer"));
        Assert.IsFalse(text.Contains("ApplyDamage"));
        Assert.IsFalse(text.Contains("OnTriggerEnter2D"));
    }

    [Test]
    public void CombatService_BeamDamageStaysInRuntimeService()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Npc/SystemNpcCombatService.cs");

        Assert.IsTrue(text.Contains("TryCreateBeam"));
        Assert.IsTrue(text.Contains("ApplyBeamDamage"));
        Assert.IsTrue(text.Contains("GetBeamDamagePortion"));
        Assert.IsTrue(text.Contains("BeamTickDuration01 = 0.9f"));
    }

    private static string ReadProjectFile(string projectPath)
    {
        Assert.IsTrue(File.Exists(projectPath), "Missing file: " + projectPath);
        return File.ReadAllText(projectPath);
    }
}