using NUnit.Framework;
using System.IO;

public sealed class S05_03_T08_NpcActivityVerificationTests
{
    [Test]
    public void NpcBehaviorTypes_ContainRoutePatrolThreatAndCombatBehaviors()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Data/Configs/Npc/SystemNpcBehaviorType.cs");

        Assert.That(text, Does.Contain("TravelToAnotherSystem"));
        Assert.That(text, Does.Contain("PatrolSystem"));
        Assert.That(text, Does.Contain("EngageEnemies"));
    }

    [Test]
    public void RouteTraversal_UsesUnlockedRoutesAndCompletesSystemTransition()
    {
        string behaviorText = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Npc/SystemNpcBehaviorService.cs");

        Assert.That(
            behaviorText,
            Does.Contain("SetupTravelToAnotherSystem"),
            "Behavior service must setup inter-system NPC travel.");

        Assert.That(
            behaviorText,
            Does.Contain("FindUnlockedRouteFromCurrentSystem"),
            "NPC inter-system travel must select only unlocked routes.");

        Assert.That(
            behaviorText,
            Does.Contain("HasUnlockedRoute"),
            "NPC route traversal must use route availability checks.");

        Assert.That(
            behaviorText,
            Does.Contain("SystemNpcTravelState.TravelingToAnotherSystem"),
            "NPC must enter TravelingToAnotherSystem state.");

        string movementText = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Npc/SystemNpcMovementService.cs");

        Assert.That(
            movementText,
            Does.Contain("CompleteSystemTravel"),
            "Movement service must complete inter-system NPC travel.");

        Assert.That(
            movementText,
            Does.Contain("npc.CurrentSystemId = arrivedSystemId"),
            "Completing route traversal must move NPC to target system.");

        Assert.That(
            movementText,
            Does.Contain("ApplyInitialFacingToSun"),
            "NPC must face the sun after arriving through a system route.");
    }

    [Test]
    public void Patrol_MilitaryAndRangerUsePatrolStateAndRouteTargets()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Npc/SystemNpcBehaviorService.cs");

        Assert.That(
            text,
            Does.Contain("SetupPatrolSystem"),
            "Behavior service must setup system patrols.");

        Assert.That(
            text,
            Does.Contain("SystemNpcTravelState.Patrolling"),
            "Patrol behavior must use Patrolling travel state.");

        Assert.That(
            text,
            Does.Contain("RandomPatrolPosition"),
            "Patrol must pick an in-system patrol target.");

        Assert.That(
            text,
            Does.Contain("npc.AllyRole == AllyRole2A.Military"),
            "Military allies must be allowed to patrol.");

        Assert.That(
            text,
            Does.Contain("npc.AllyRole == AllyRole2A.Ranger"),
            "Ranger allies must be allowed to patrol.");
    }

    [Test]
    public void ThreatReaction_MilitaryAndRangerCanInterruptForEnemies()
    {
        string text = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Npc/SystemNpcBehaviorService.cs");

        Assert.That(
            text,
            Does.Contain("ShouldInterruptForThreat"),
            "NPC behavior must be able to interrupt current activity for threats.");

        Assert.That(
            text,
            Does.Contain("CanNpcReactToThreats"),
            "Threat reaction must be role-gated.");

        Assert.That(
            text,
            Does.Contain("FindCombatTargetId"),
            "Threat reaction must resolve a live combat target at runtime.");

        Assert.That(
            text,
            Does.Contain("SystemNpcBehaviorType.EngageEnemies"),
            "Threat reaction must route capable NPCs into EngageEnemies.");

        Assert.That(
            text,
            Does.Contain("AllyRole2A.Military"),
            "Military allies should be default threat responders.");

        Assert.That(
            text,
            Does.Contain("AllyRole2A.Ranger"),
            "Ranger allies should be default threat responders.");
    }

    [Test]
    public void NpcCombat_UsesGalaxyCombatServiceAndRuntimeProjectileEvents()
    {
        string galaxyCombatText = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Npc/Galaxy/GalaxyNpcCombatService.cs");

        Assert.That(
            galaxyCombatText,
            Does.Contain("GameTickStartedEvent"),
            "Galaxy NPC combat must run from game tick events.");

        Assert.That(
            galaxyCombatText,
            Does.Contain("_systemNpcCombatService.Tick"),
            "Galaxy NPC combat must delegate combat resolution to system NPC combat service.");

        string combatText = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Npc/SystemNpcCombatService.cs");

        Assert.That(
            combatText,
            Does.Contain("new GalaxyNpcProjectileCreatedEvent"),
            "NPC combat must publish projectile creation events.");

        Assert.That(
            combatText,
            Does.Contain("new GalaxyNpcProjectileImpactEvent"),
            "NPC combat must publish projectile impact events.");

        Assert.That(
            combatText,
            Does.Contain("ApplyDamage"),
            "NPC combat must apply damage through runtime services, not visual triggers.");
    }

    [Test]
    public void OfflineStep_DoesNotSpawnNewNpcOrResolveCombat()
    {
        string offlineText = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Npc/SystemNpcOfflineRelocationService.cs");

        Assert.That(
            offlineText,
            Does.Contain("TryProcessOffline"),
            "Offline relocation service must expose the offline step entry point.");

        Assert.That(
            offlineText,
            Does.Contain("TravelToAnotherSystem"),
            "Offline step must only relocate NPCs that can travel between systems.");

        Assert.That(
            offlineText,
            Does.Contain("IsHostileToPlayer"),
            "Offline destination must reject systems occupied by hostile NPCs.");

        Assert.That(
            offlineText,
            Does.Not.Contain("ProcessOfflinePopulation"),
            "Offline step must not create new NPCs through offline population.");

        Assert.That(
            offlineText,
            Does.Not.Contain("_combatService.Tick"),
            "Current T05/T08 scope must not resolve offline NPC combat.");
    }

    [Test]
    public void AutonomousLiberation_RemainsBlockedUntilDecisionC05()
    {
        string interfaceText = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Galaxy/ISystemSecurityService.cs");

        Assert.That(
            interfaceText,
            Does.Contain("TrySetSystemStableByNpcAutonomy"),
            "Security service must expose an explicit blocked path for NPC autonomous liberation.");

        string serviceText = ReadProjectFile(
            "Assets/_Project/Scripts/Core/Services/Galaxy/SystemSecurityService.cs");

        Assert.That(
            serviceText,
            Does.Contain("C-05"),
            "NPC autonomous liberation must be documented as blocked until C-05.");

        Assert.That(
            serviceText,
            Does.Contain("return false"),
            "NPC autonomous liberation method must refuse to change system status.");
    }

    private static string ReadProjectFile(string projectPath)
    {
        Assert.That(
            File.Exists(projectPath),
            Is.True,
            "Missing file: " + projectPath);

        return File.ReadAllText(projectPath);
    }
}