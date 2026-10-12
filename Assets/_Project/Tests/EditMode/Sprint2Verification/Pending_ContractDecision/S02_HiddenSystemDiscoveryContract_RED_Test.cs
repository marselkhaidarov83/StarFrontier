using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// RED / pending owner decision. DO NOT install with the ready suite.
/// Current GalaxyRuntimeStateFactory hardcodes IsDiscovered=true and this test
/// will reveal the difference from the existing IsHiddenAtStart contract.
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

    private static void SetPrivate(object target, string name, object value)
    {
        var field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field: " + name);
        field.SetValue(target, value);
    }
}
