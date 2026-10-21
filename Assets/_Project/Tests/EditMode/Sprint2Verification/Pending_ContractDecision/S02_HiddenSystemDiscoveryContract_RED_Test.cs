using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Normal games respect HiddenAtStart. Test games discover systems in the configured
/// number of unlocked sectors, ordered by SectorConfig.Order.
/// </summary>
public sealed class S02_HiddenSystemDiscoveryContract_RED_Test
{
    [Test]
    public void NewGame_ShouldNotAutoDiscoverSystemMarkedHiddenAtStart()
    {
        var galaxy = ScriptableObject.CreateInstance<GalaxyConfig>();
        var sector = ScriptableObject.CreateInstance<SectorConfig>();
        var hidden = ScriptableObject.CreateInstance<StarSystemConfig>();

        try
        {
            SetPrivate(hidden, "isHiddenAtStart", true);
            SetPrivate(hidden, "isStartSystem", false);
            SetPrivate(sector, "systems", new[] { hidden });
            galaxy.Sectors.Add(sector);

            GalaxyRuntimeState state =
                GalaxyRuntimeStateFactory.CreateNewGalaxyRuntimeState(galaxy, false, false);

            Assert.That(state.Systems.Count, Is.EqualTo(1));
            Assert.That(state.Systems[0].IsDiscovered, Is.False,
                "HiddenAtStart must not be bypassed by a debug discovery override.");
        }
        finally
        {
            Object.DestroyImmediate(hidden);
            Object.DestroyImmediate(sector);
            Object.DestroyImmediate(galaxy);
        }
    }

    [TestCase(0)]
    [TestCase(2)]
    [TestCase(5)]
    public void TestGame_DiscoversSystemsOnlyInConfiguredUnlockedSectors(int unlockedSectorCount)
    {
        var assets = new List<Object>();
        var galaxy = ScriptableObject.CreateInstance<GalaxyConfig>();
        assets.Add(galaxy);
        try
        {
            // Reverse input order to check that sector Order determines the first N sectors.
            for (int order = 5; order >= 0; order--)
            {
                var sector = ScriptableObject.CreateInstance<SectorConfig>();
                var hidden = ScriptableObject.CreateInstance<StarSystemConfig>();
                var visible = ScriptableObject.CreateInstance<StarSystemConfig>();
                assets.Add(sector);
                assets.Add(hidden);
                assets.Add(visible);
                typeof(BaseConfig).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(sector, "sector_" + order);
                SetPrivate(sector, "order", order);
                SetPrivate(hidden, "isHiddenAtStart", true);
                SetPrivate(visible, "isHiddenAtStart", false);
                SetPrivate(sector, "systems", new[] { hidden, visible });
                galaxy.Sectors.Add(sector);
            }

            GalaxyRuntimeState state = GalaxyRuntimeStateFactory.CreateNewGalaxyRuntimeState(
                galaxy, false, false, true, unlockedSectorCount);

            Assert.That(state.Systems.Count, Is.EqualTo(12));
            for (int index = 0; index < state.Sectors.Count; index++)
            {
                bool expected = index < unlockedSectorCount;
                Assert.That(state.Sectors[index].SectorId, Is.EqualTo("sector_" + index));
                Assert.That(state.Sectors[index].IsUnlocked, Is.EqualTo(expected));
                Assert.That(state.Systems[index * 2].IsDiscovered, Is.EqualTo(expected),
                    "Hidden system in test sector index " + index);
                Assert.That(state.Systems[index * 2 + 1].IsDiscovered, Is.EqualTo(expected),
                    "Visible system in test sector index " + index);
            }
        }
        finally
        {
            foreach (Object asset in assets)
                Object.DestroyImmediate(asset);
        }
    }

    private static void SetPrivate(object target, string name, object value)
    {
        var field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field: " + name);
        field.SetValue(target, value);
    }
}
