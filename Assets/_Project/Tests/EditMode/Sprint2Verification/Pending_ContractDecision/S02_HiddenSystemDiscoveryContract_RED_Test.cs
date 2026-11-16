using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Normal games discover all systems. Test games discover systems in the configured
/// number of unlocked sectors, ordered by SectorConfig.Order.
/// </summary>
public sealed class S02_HiddenSystemDiscoveryContract_RED_Test
{
    [Test]
    public void NewGame_DiscoversSystemsInLockedSectors()
    {
        var galaxy = ScriptableObject.CreateInstance<GalaxyConfig>();
        var sector = ScriptableObject.CreateInstance<SectorConfig>();
        var system = ScriptableObject.CreateInstance<StarSystemConfig>();

        try
        {
            SetConfigId(galaxy, "galaxy_hidden_discovery");
            SetConfigId(sector, "sector_hidden_discovery");
            SetConfigId(system, "system_discovery");
            SetPrivate(sector, "systems", new[] { system });
            galaxy.Sectors.Add(sector);

            GalaxyRuntimeState state =
                GalaxyRuntimeStateFactory.CreateNewGalaxyRuntimeState(galaxy, false, false);

            Assert.That(state.Systems.Count, Is.EqualTo(1));
            Assert.That(state.Sectors[0].IsUnlocked, Is.False);
            Assert.That(state.Systems[0].IsDiscovered, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(system);
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
        SetConfigId(galaxy, "galaxy_test_discovery");
        try
        {
            // Reverse input order to check that sector Order determines the first N sectors.
            for (int order = 5; order >= 0; order--)
            {
                var sector = ScriptableObject.CreateInstance<SectorConfig>();
                var first = ScriptableObject.CreateInstance<StarSystemConfig>();
                var second = ScriptableObject.CreateInstance<StarSystemConfig>();
                assets.Add(sector);
                assets.Add(first);
                assets.Add(second);
                SetConfigId(sector, "sector_" + order);
                SetConfigId(first, "system_first_" + order);
                SetConfigId(second, "system_second_" + order);
                SetPrivate(sector, "order", order);
                SetPrivate(sector, "systems", new[] { first, second });
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
                    "First system in test sector index " + index);
                Assert.That(state.Systems[index * 2 + 1].IsDiscovered, Is.EqualTo(expected),
                    "Second system in test sector index " + index);
            }
        }
        finally
        {
            foreach (Object asset in assets)
                Object.DestroyImmediate(asset);
        }
    }

    private static void SetConfigId(BaseConfig target, string id)
    {
        var field = typeof(BaseConfig).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing BaseConfig.id");
        field.SetValue(target, id);
    }

    private static void SetPrivate(object target, string name, object value)
    {
        var field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field: " + name);
        field.SetValue(target, value);
    }
}
