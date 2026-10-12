using NUnit.Framework;
using UnityEngine;

/// <summary>Regression of Sprint 2 galaxy navigation state on the latest schema.</summary>
public sealed class S02_GalaxyStatePersistenceRegressionTests
{
    [Test]
    public void GalaxyMap_DiscoveredVisitedAndUnlockedFlagsSurviveSaveRoundTrip()
    {
        var source = new GameRuntimeState();
        source.Galaxy.CurrentSystemId = "system_a";
        source.Galaxy.GalaxyDay = 7;
        source.Galaxy.Sectors.Add(new SectorRuntimeState { SectorId = "sector_01", IsUnlocked = true });
        source.Galaxy.Sectors.Add(new SectorRuntimeState { SectorId = "sector_02", IsUnlocked = false });
        source.Galaxy.Systems.Add(new StarSystemRuntimeState
        {
            SystemId = "system_a", IsDiscovered = true, IsVisited = true,
            SystemStatus = StarSystemStatus.Stable
        });
        source.Galaxy.Systems.Add(new StarSystemRuntimeState
        {
            SystemId = "system_b", IsDiscovered = false, IsVisited = false,
            SystemStatus = StarSystemStatus.Threat
        });
        source.Galaxy.Routes.Add(new RouteRuntimeState { RouteId = "route_ab", IsUnlocked = false });

        GameRuntimeState loaded = JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(source));

        Assert.That(loaded.Galaxy.CurrentSystemId, Is.EqualTo("system_a"));
        Assert.That(loaded.Galaxy.GalaxyDay, Is.EqualTo(7));
        Assert.That(loaded.Galaxy.Sectors[0].IsUnlocked, Is.True);
        Assert.That(loaded.Galaxy.Sectors[1].IsUnlocked, Is.False);
        Assert.That(loaded.Galaxy.Systems[0].IsVisited, Is.True);
        Assert.That(loaded.Galaxy.Systems[1].IsDiscovered, Is.False);
        Assert.That(loaded.Galaxy.Systems[1].SystemStatus, Is.EqualTo(StarSystemStatus.Threat));
        Assert.That(loaded.Galaxy.Routes[0].IsUnlocked, Is.False);
    }

    [Test]
    public void NewlyCreatedGalaxyRuntimeState_HasIndependentMutableCollections()
    {
        var first = new GalaxyRuntimeState();
        var second = new GalaxyRuntimeState();

        first.Sectors.Add(new SectorRuntimeState { SectorId = "sector_01" });
        first.Routes.Add(new RouteRuntimeState { RouteId = "route_01" });
        first.Systems.Add(new StarSystemRuntimeState { SystemId = "system_01" });

        Assert.That(second.Sectors, Is.Empty);
        Assert.That(second.Routes, Is.Empty);
        Assert.That(second.Systems, Is.Empty);
        Assert.That(second.Sectors, Is.Not.SameAs(first.Sectors));
    }

    [Test]
    public void WarOverlayMustNotImplicitlyUnlockLockedSectorOrRoute()
    {
        var source = new GalaxyRuntimeState();
        source.Sectors.Add(new SectorRuntimeState { SectorId = "sector_locked", IsUnlocked = false });
        source.Routes.Add(new RouteRuntimeState { RouteId = "route_locked", IsUnlocked = false });
        source.Systems.Add(new StarSystemRuntimeState
        {
            SystemId = "system_captured", SystemStatus = StarSystemStatus.Captured
        });

        GalaxyRuntimeState loaded = JsonUtility.FromJson<GalaxyRuntimeState>(JsonUtility.ToJson(source));

        Assert.That(loaded.Sectors[0].IsUnlocked, Is.False);
        Assert.That(loaded.Routes[0].IsUnlocked, Is.False);
        Assert.That(loaded.Systems[0].SystemStatus, Is.EqualTo(StarSystemStatus.Captured));
    }
}
