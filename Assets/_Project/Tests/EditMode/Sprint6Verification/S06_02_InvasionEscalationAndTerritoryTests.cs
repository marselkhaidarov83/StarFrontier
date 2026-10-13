using System.Collections.Generic;
using NUnit.Framework;

/// <summary>2A-S06-02: limits, simultaneous invasions, faction scaling and frontlines.</summary>
public sealed class S06_02_InvasionEscalationAndTerritoryTests
{
    [Test]
    public void MultipleTargets_AreAllowedUntilGlobalInvasionLimit()
    {
        var galaxy = new GalaxyRuntimeState();
        galaxy.Invasions.Add(Active("inv_1", "system_a"));
        galaxy.Invasions.Add(Active("inv_2", "system_b"));

        Assert.That(galaxy.CountActiveInvasions(), Is.EqualTo(2));
        Assert.That(galaxy.CanStartAdditionalInvasion("system_c", 3), Is.True);
        Assert.That(galaxy.CanStartAdditionalInvasion("system_a", 3), Is.False);
        Assert.That(galaxy.CanStartAdditionalInvasion("system_c", 2), Is.False);
        Assert.That(galaxy.CanStartAdditionalInvasion("system_c", 0), Is.False);
        Assert.That(galaxy.CanStartAdditionalInvasion(" ", 3), Is.False);
    }

    [Test]
    public void ResolvedInvasion_DoesNotConsumeActiveSlot()
    {
        var galaxy = new GalaxyRuntimeState();
        galaxy.Invasions.Add(Active("inv_1", "system_a"));
        galaxy.Invasions.Add(new InvasionState
        {
            InvasionId = "old", TargetSystemId = "system_b",
            LifecycleState = InvasionLifecycleState.Resolved
        });

        Assert.That(galaxy.CountActiveInvasions(), Is.EqualTo(1));
        Assert.That(galaxy.CanStartAdditionalInvasion("system_b", 2), Is.True);
    }

    [TestCase("ai")]
    [TestCase("ancients")]
    [TestCase("infected")]
    public void FactionEscalation_IsMonotonicAcrossLevels_WithoutPlayerPowerScaling(string faction)
    {
        int previousPressure = 0;
        for (int level = 1; level <= 10; level++)
        {
            var invasion = new InvasionState { UsesPlayerPowerScaling = true };
            invasion.ApplyEscalation(faction, level);

            Assert.That(invasion.Level, Is.EqualTo(level));
            Assert.That(invasion.EscalationTier, Is.InRange(1, 5));
            Assert.That(invasion.EscalationPressure, Is.GreaterThanOrEqualTo(previousPressure));
            Assert.That(invasion.UsesPlayerPowerScaling, Is.False);
            Assert.That(invasion.EscalationRuleId, Is.Not.Empty);
            previousPressure = invasion.EscalationPressure;
        }
    }

    [Test]
    public void Escalation_ClampsOutOfRangeGalaxyLevels()
    {
        var below = new InvasionState();
        var above = new InvasionState();
        below.ApplyEscalation("AI", -100);
        above.ApplyEscalation(" AI ", 99);

        Assert.That(below.Level, Is.EqualTo(1));
        Assert.That(above.Level, Is.EqualTo(10));
        Assert.That(below.FactionId, Is.EqualTo("ai"));
        Assert.That(above.FactionId, Is.EqualTo("ai"));
    }

    [Test]
    public void FrontlineRebuild_TracksOwnedBorderAndNeighborFrontierWithoutDuplicates()
    {
        var faction = new EnemyFactionState { FactionId = "ai" };
        faction.OwnedSystemIds.Add("owned_a");
        faction.OwnedSystemIds.Add("owned_b");
        faction.TerritorySystemIds.Add("contested");
        var neighbors = new Dictionary<string, IReadOnlyList<string>>
        {
            { "owned_a", new[] { "owned_b", "civilian", "civilian" } },
            { "owned_b", new[] { "owned_a", "contested" } }
        };

        faction.RebuildFrontlineAndFrontier(neighbors);
        Assert.That(faction.FrontlineSystemIds, Is.EquivalentTo(new[] { "owned_a", "owned_b" }));
        Assert.That(faction.FrontierSystemIds, Is.EquivalentTo(new[] { "civilian", "contested" }));

        faction.RebuildFrontlineAndFrontier(neighbors);
        Assert.That(faction.FrontierSystemIds.Count, Is.EqualTo(2),
            "Rebuilding must not accumulate duplicated frontier IDs.");
    }

    [Test]
    public void AntiHopelessness_NeverAllowsCaptureOfLastRecoverableSystem()
    {
        var galaxy = new GalaxyRuntimeState();
        galaxy.Systems.Add(new StarSystemRuntimeState
        {
            SystemId = "last_safe", SystemStatus = StarSystemStatus.Stable
        });
        galaxy.Systems.Add(new StarSystemRuntimeState
        {
            SystemId = "enemy", SystemStatus = StarSystemStatus.Captured
        });

        Assert.That(galaxy.CanCaptureSystemWithoutHopelessCollapse("last_safe"), Is.False);
        Assert.That(galaxy.CanCaptureSystemWithoutHopelessCollapse("enemy"), Is.True);
    }

    private static InvasionState Active(string id, string systemId)
    {
        return new InvasionState
        {
            InvasionId = id, TargetSystemId = systemId,
            LifecycleState = InvasionLifecycleState.Active
        };
    }
}
