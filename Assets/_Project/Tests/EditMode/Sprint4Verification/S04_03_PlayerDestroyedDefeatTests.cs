using NUnit.Framework;
using System.IO;

public sealed class S04_03_PlayerDestroyedDefeatTests
{
    [Test]
    public void PlayerCombatTargetService_RegistersEncounterDefeatWhenPlayerShipDestroyed()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Combat/PlayerCombatTargetService.cs");

        Assert.IsTrue(
            text.Contains("RegisterPlayerDestroyed"),
            "PlayerCombatTargetService must register player destruction in encounter.");

        Assert.IsTrue(
            text.Contains("result.IsDestroyed"),
            "PlayerCombatTargetService must react to destroyed damage result.");
    }

    [Test]
    public void PlayerCombatEntity_RegistersEncounterDefeatWhenScenePlayerDestroyed()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Presentation/Combat/Player/PlayerCombatEntity.cs");

        Assert.IsTrue(
            text.Contains("RegisterPlayerDestroyed"),
            "PlayerCombatEntity must register player destruction in encounter.");

        Assert.IsTrue(
            text.Contains("PlayerDestroyedEvent"),
            "PlayerCombatEntity must publish player destroyed event.");
    }

    [Test]
    public void SystemEncounterService_PublishesDefeatEventForPlayerDestroyed()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Combat/SystemEncounterService.cs");

        Assert.IsTrue(
            text.Contains("SystemEncounterDefeatReason.PlayerDestroyed"),
            "SystemEncounterService must support PlayerDestroyed defeat reason.");

        Assert.IsTrue(
            text.Contains("SystemEncounterDefeatedEvent"),
            "SystemEncounterService must publish SystemEncounterDefeatedEvent.");

        Assert.IsTrue(
            text.Contains("CombatDefeatEvent2A"),
            "SystemEncounterService must publish CombatDefeatEvent2A.");
    }

    private static string ReadProjectFile(string projectPath)
    {
        Assert.IsTrue(File.Exists(projectPath), "Missing file: " + projectPath);
        return File.ReadAllText(projectPath);
    }
}