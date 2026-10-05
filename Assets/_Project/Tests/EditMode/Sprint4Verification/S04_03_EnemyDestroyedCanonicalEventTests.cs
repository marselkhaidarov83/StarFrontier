using NUnit.Framework;
using System.IO;

public sealed class S04_03_EnemyDestroyedCanonicalEventTests
{
    [Test]
    public void SystemNpcRuntimeService_UsesSystemNpcDestroyedEventAsCanonicalDestroyedEvent()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Npc/SystemNpcRuntimeService.cs");

        Assert.IsTrue(
            text.Contains("new SystemNpcDestroyedEvent"),
            "Enemy/NPC destruction must publish SystemNpcDestroyedEvent.");

        Assert.IsFalse(
            text.Contains("new SystemEnemyDestroyedEvent"),
            "SystemNpcRuntimeService must not publish legacy SystemEnemyDestroyedEvent.");
    }

    [Test]
    public void EnemyDeath_NotifiesEncounterService()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Npc/SystemNpcRuntimeService.cs");

        Assert.IsTrue(
            text.Contains("RegisterEnemyDestroyed"),
            "Enemy death must notify SystemEncounterService.RegisterEnemyDestroyed.");
    }

    [Test]
    public void EnemyDestroyedEvent_IsNotDuplicatedInProductionNpcRuntimePath()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Npc/SystemNpcRuntimeService.cs");

        int npcDestroyedCount = CountOccurrences(text, "new SystemNpcDestroyedEvent");
        int legacyDestroyedCount = CountOccurrences(text, "new SystemEnemyDestroyedEvent");

        Assert.AreEqual(
            1,
            npcDestroyedCount,
            "SystemNpcRuntimeService must publish exactly one canonical destroyed event.");

        Assert.AreEqual(
            0,
            legacyDestroyedCount,
            "SystemNpcRuntimeService must not publish legacy destroyed events.");
    }

    private static string ReadProjectFile(string projectPath)
    {
        Assert.IsTrue(File.Exists(projectPath), "Missing file: " + projectPath);
        return File.ReadAllText(projectPath);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;

        while (true)
        {
            index = text.IndexOf(pattern, index, System.StringComparison.Ordinal);

            if (index < 0)
                return count;

            count++;
            index += pattern.Length;
        }
    }
}