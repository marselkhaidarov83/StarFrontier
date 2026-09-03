using NUnit.Framework;
using System.IO;

public sealed class S05_05_NpcDebugVerificationTests
{
    [Test]
    public void T01_DebugSpawnCommands_AreAvailableThroughBootstrapper()
    {
        string bootstrapperText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Bootstrap/Bootstrapper.cs");

        Assert.That(
            bootstrapperText,
            Does.Contain("Spawn Enemy Attack Group In Current System"),
            "Bootstrapper must expose enemy debug spawn command.");

        Assert.That(
            bootstrapperText,
            Does.Contain("Spawn Ally Ranger In Current System"),
            "Bootstrapper must expose ally Ranger debug spawn command.");

        Assert.That(
            bootstrapperText,
            Does.Contain("DebugSpawnAllyInCurrentSystem(AllyRole2A"),
            "Bootstrapper must spawn ally NPCs by role through the population service.");
    }

    [Test]
    public void T02_DebugDespawnKillReset_AreAvailableThroughRuntimeService()
    {
        string interfaceText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Services/Npc/ISystemNpcRuntimeService.cs");

        string runtimeText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Services/Npc/SystemNpcRuntimeService.cs");

        string bootstrapperText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Bootstrap/Bootstrapper.cs");

        Assert.That(interfaceText, Does.Contain("bool DespawnNpc(string runtimeNpcId)"));
        Assert.That(interfaceText, Does.Contain("bool KillNpc(string runtimeNpcId"));
        Assert.That(interfaceText, Does.Contain("bool ResetNpc(string runtimeNpcId)"));

        Assert.That(runtimeText, Does.Contain("public bool DespawnNpc"));
        Assert.That(runtimeText, Does.Contain("public bool KillNpc"));
        Assert.That(runtimeText, Does.Contain("public bool ResetNpc"));

        Assert.That(bootstrapperText, Does.Contain("Despawn Target NPC By Runtime ID"));
        Assert.That(bootstrapperText, Does.Contain("Kill Target NPC By Runtime ID"));
        Assert.That(bootstrapperText, Does.Contain("Reset Target NPC By Runtime ID"));
    }

    [Test]
    public void T03_DebugNpcState_PrintsIdentityRouteTargetStateAndTimers()
    {
        string bootstrapperText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Bootstrap/Bootstrapper.cs");

        Assert.That(bootstrapperText, Does.Contain("Print All NPC Debug State"));
        Assert.That(bootstrapperText, Does.Contain("Print Target NPC Debug State"));

        Assert.That(bootstrapperText, Does.Contain("RuntimeNpcId"));
        Assert.That(bootstrapperText, Does.Contain("Role"));
        Assert.That(bootstrapperText, Does.Contain("Faction"));
        Assert.That(bootstrapperText, Does.Contain("Route"));
        Assert.That(bootstrapperText, Does.Contain("Target"));
        Assert.That(bootstrapperText, Does.Contain("State"));
        Assert.That(bootstrapperText, Does.Contain("Timers"));
    }

    [Test]
    public void T04_DebugForcedRouteAndOfflineStep_AreAvailable()
    {
        string interfaceText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Services/Npc/ISystemNpcOfflineRelocationService.cs");

        string serviceText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Services/Npc/SystemNpcOfflineRelocationService.cs");

        string bootstrapperText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Bootstrap/Bootstrapper.cs");

        Assert.That(interfaceText, Does.Contain("DebugForceTargetNpcRoute"));
        Assert.That(interfaceText, Does.Contain("DebugProcessOfflineStep"));

        Assert.That(serviceText, Does.Contain("public bool DebugForceTargetNpcRoute"));
        Assert.That(serviceText, Does.Contain("public bool DebugProcessOfflineStep"));
        Assert.That(serviceText, Does.Contain("SystemNpcTravelState.TravelingToAnotherSystem"));

        Assert.That(bootstrapperText, Does.Contain("Force Target NPC Route To Linked System"));
        Assert.That(bootstrapperText, Does.Contain("Run NPC Offline Step"));
    }

    [Test]
    public void T05_StressSpawnDebug_IsAvailableWithoutFixedNpcLimit()
    {
        string bootstrapperText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Bootstrap/Bootstrapper.cs");

        Assert.That(bootstrapperText, Does.Contain("Stress Spawn NPC"));
        Assert.That(bootstrapperText, Does.Contain("Stress Spawn NPC Wave"));
        Assert.That(bootstrapperText, Does.Contain("Print NPC Runtime Count"));

        Assert.That(bootstrapperText, Does.Contain("debugNpcStressSpawnAttempts"));
        Assert.That(bootstrapperText, Does.Contain("debugNpcStressSpawnWaves"));
        Assert.That(bootstrapperText, Does.Contain("RunDebugNpcStressSpawnWave"));
        Assert.That(bootstrapperText, Does.Contain("GetDebugNpcRuntimeCount"));

        Assert.That(
            bootstrapperText,
            Does.Not.Contain("MaxNpcLimit"),
            "T05 must not introduce final production NPC cap. C-06 keeps exact limit open.");
    }

    [Test]
    public void T06_InvalidCommandsAndInvalidState_AreLogged()
    {
        string runtimeText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Services/Npc/SystemNpcRuntimeService.cs");

        string bootstrapperText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Bootstrap/Bootstrapper.cs");

        Assert.That(runtimeText, Does.Contain("Debug.LogWarning"));
        Assert.That(runtimeText, Does.Contain("RuntimeNpcId is empty"));
        Assert.That(runtimeText, Does.Contain("NPC not found"));

        Assert.That(bootstrapperText, Does.Contain("Validate NPC Runtime State"));
        Assert.That(bootstrapperText, Does.Contain("ValidateDebugNpcRuntimeState"));
        Assert.That(bootstrapperText, Does.Contain("Config not found"));
        Assert.That(bootstrapperText, Does.Contain("CurrentSystemId invalid"));
        Assert.That(bootstrapperText, Does.Contain("TravelingToAnotherSystem without TargetSystemId"));
    }

    private static string ReadProjectFile(string path)
    {
        Assert.That(
            File.Exists(path),
            Is.True,
            "Missing project file: " + path);

        return File.ReadAllText(path);
    }
}