using NUnit.Framework;
using System.IO;

public sealed class S05_05_NpcDebugVerificationTests
{
    [Test]
    public void T01_DebugSpawnCommands_AreAvailableThroughDebugController()
    {
        string debugControllerText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Bootstrap/DebugController2A.cs");

        Assert.That(
            debugControllerText,
            Does.Contain("Spawn Enemy Attack Group In Current System"),
            "DebugController2A must expose enemy debug spawn command.");

        Assert.That(
            debugControllerText,
            Does.Contain("Spawn Ally Ranger In Current System"),
            "DebugController2A must expose ally Ranger debug spawn command.");

        Assert.That(
            debugControllerText,
            Does.Contain("DebugSpawnAllyInCurrentSystem(AllyRole2A"),
            "DebugController2A must spawn ally NPCs by role through the population service.");
    }

    [Test]
    public void T02_DebugDespawnKillReset_AreAvailableThroughRuntimeService()
    {
        string interfaceText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Services/Npc/ISystemNpcRuntimeService.cs");

        string runtimeText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Services/Npc/SystemNpcRuntimeService.cs");

        string debugControllerText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Bootstrap/DebugController2A.cs");

        Assert.That(interfaceText, Does.Contain("bool DespawnNpc(string runtimeNpcId)"));
        Assert.That(interfaceText, Does.Contain("bool KillNpc(string runtimeNpcId"));
        Assert.That(interfaceText, Does.Contain("bool ResetNpc(string runtimeNpcId)"));

        Assert.That(runtimeText, Does.Contain("public bool DespawnNpc"));
        Assert.That(runtimeText, Does.Contain("public bool KillNpc"));
        Assert.That(runtimeText, Does.Contain("public bool ResetNpc"));

        Assert.That(debugControllerText, Does.Contain("Despawn Target NPC By Runtime ID"));
        Assert.That(debugControllerText, Does.Contain("Kill Target NPC By Runtime ID"));
        Assert.That(debugControllerText, Does.Contain("Reset Target NPC By Runtime ID"));
    }

    [Test]
    public void T03_DebugNpcState_PrintsIdentityRouteTargetStateAndTimers()
    {
        string debugControllerText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Bootstrap/DebugController2A.cs");

        Assert.That(debugControllerText, Does.Contain("Print All NPC Debug State"));
        Assert.That(debugControllerText, Does.Contain("Print Target NPC Debug State"));

        Assert.That(debugControllerText, Does.Contain("RuntimeNpcId"));
        Assert.That(debugControllerText, Does.Contain("Role"));
        Assert.That(debugControllerText, Does.Contain("Faction"));
        Assert.That(debugControllerText, Does.Contain("Route"));
        Assert.That(debugControllerText, Does.Contain("Target"));
        Assert.That(debugControllerText, Does.Contain("State"));
        Assert.That(debugControllerText, Does.Contain("Timers"));
    }

    [Test]
    public void T04_DebugForcedRouteAndOfflineStep_AreAvailable()
    {
        string interfaceText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Services/Npc/ISystemNpcOfflineRelocationService.cs");

        string serviceText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Services/Npc/SystemNpcOfflineRelocationService.cs");

        string debugControllerText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Bootstrap/DebugController2A.cs");

        Assert.That(interfaceText, Does.Contain("DebugForceTargetNpcRoute"));
        Assert.That(interfaceText, Does.Contain("DebugProcessOfflineStep"));

        Assert.That(serviceText, Does.Contain("public bool DebugForceTargetNpcRoute"));
        Assert.That(serviceText, Does.Contain("public bool DebugProcessOfflineStep"));
        Assert.That(serviceText, Does.Contain("SystemNpcTravelState.TravelingToAnotherSystem"));

        Assert.That(debugControllerText, Does.Contain("Force Target NPC Route To Linked System"));
        Assert.That(debugControllerText, Does.Contain("Run NPC Offline Step"));
    }

    [Test]
    public void T05_StressSpawnDebug_IsAvailableWithoutFixedNpcLimit()
    {
        string debugControllerText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Bootstrap/DebugController2A.cs");

        Assert.That(debugControllerText, Does.Contain("Stress Spawn NPC"));
        Assert.That(debugControllerText, Does.Contain("Stress Spawn NPC Wave"));
        Assert.That(debugControllerText, Does.Contain("Print NPC Runtime Count"));

        Assert.That(debugControllerText, Does.Contain("debugNpcStressSpawnAttempts"));
        Assert.That(debugControllerText, Does.Contain("debugNpcStressSpawnWaves"));
        Assert.That(debugControllerText, Does.Contain("RunDebugNpcStressSpawnWave"));
        Assert.That(debugControllerText, Does.Contain("GetDebugNpcRuntimeCount"));

        Assert.That(
            debugControllerText,
            Does.Not.Contain("MaxNpcLimit"),
            "T05 must not introduce final production NPC cap. C-06 keeps exact limit open.");
    }

    [Test]
    public void T06_InvalidCommandsAndInvalidState_AreLogged()
    {
        string runtimeText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Services/Npc/SystemNpcRuntimeService.cs");

        string debugControllerText =
            ReadProjectFile("Assets/_Project/Scripts/Core/Bootstrap/DebugController2A.cs");

        Assert.That(runtimeText, Does.Contain("Debug.LogWarning"));
        Assert.That(runtimeText, Does.Contain("RuntimeNpcId is empty"));
        Assert.That(runtimeText, Does.Contain("NPC not found"));

        Assert.That(debugControllerText, Does.Contain("DebugCombatWarning"));
        Assert.That(debugControllerText, Does.Contain("debugCombatTargetRuntimeNpcId is empty"));
        Assert.That(debugControllerText, Does.Contain("NPC was not found"));
        Assert.That(debugControllerText, Does.Contain("ServiceRegistry is not initialized"));
        Assert.That(debugControllerText, Does.Contain("IConfigService is not registered"));
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