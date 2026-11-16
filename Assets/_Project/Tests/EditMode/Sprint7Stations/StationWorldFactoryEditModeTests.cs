using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class StationWorldFactoryEditModeTests
{
    private readonly List<ScriptableObject> _assets = new List<ScriptableObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var asset in _assets) UnityEngine.Object.DestroyImmediate(asset);
        _assets.Clear();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void NewWorld_RejectsMissingSystemIdentity(string id)
    {
        var system = Make<StarSystemConfig>(id);
        var galaxy = GalaxyWith(SectorWith("sector", system));
        Assert.Throws<InvalidOperationException>(() =>
            GalaxyRuntimeStateFactory.CreateNewGalaxyRuntimeState(galaxy, false, false));
    }

    [Test]
    public void NewWorld_RejectsDuplicateSystemOwnersAcrossSectors()
    {
        var first = Make<StarSystemConfig>("same_system");
        var second = Make<StarSystemConfig>("same_system");
        Set(first, "station", Station("station_first", 2));
        Set(second, "station", Station("station_second", 5));
        var galaxy = GalaxyWith(SectorWith("sector_a", first), SectorWith("sector_b", second));
        Assert.Throws<InvalidOperationException>(() =>
            GalaxyRuntimeStateFactory.CreateNewGalaxyRuntimeState(galaxy, false, false));
    }

    [Test]
    public void NewWorld_UsesIndividualStationLevels()
    {
        var unbuilt = Make<StarSystemConfig>("system_unbuilt");
        var developed = Make<StarSystemConfig>("system_developed");
        Set(unbuilt, "station", Station("station_unbuilt", 0));
        Set(developed, "station", Station("station_developed", 6));
        var galaxy = GalaxyWith(SectorWith("sector", unbuilt, developed));

        var state = GalaxyRuntimeStateFactory.CreateNewGalaxyRuntimeState(galaxy, false, false);

        Assert.That(state.Systems.Count, Is.EqualTo(2));
        Assert.That(state.Systems[0].Station.Level, Is.Zero);
        Assert.That(state.Systems[1].Station.Level, Is.EqualTo(6));
        Assert.That(state.Systems[0].Station.IsOperational, Is.False);
        Assert.That(state.Systems[1].Station.IsOperational, Is.True);
        Assert.That(StationLifecycleRules.GetValidationErrors(state), Is.Empty);
    }

    [TestCase(false, 0)]
    [TestCase(false, 6)]
    [TestCase(false, 7)]
    [TestCase(false, 8)]
    [TestCase(true, 8)]
    public void NewWorld_DistributesCapturedSystemsAcrossFactions(bool mobileTestMode, int enemyCount)
    {
        var allied = SectorWith("allied", Make<StarSystemConfig>("allied_system"));
        var enemies = Enumerable.Range(0, enemyCount)
            .Select(index => Make<StarSystemConfig>("enemy_" + index))
            .ToArray();
        Set(allied.Systems[0], "station", Station("station_allied", 1));
        foreach (var enemy in enemies)
            Set(enemy, "station", Station("station_" + enemy.Id, 1));
        var firstEnemy = SectorWith("enemy_first", enemies.Take(enemyCount / 2).ToArray());
        var secondEnemy = SectorWith("enemy_second", enemies.Skip(enemyCount / 2).ToArray());
        Set(allied, "order", 0);
        Set(firstEnemy, "order", 1);
        Set(secondEnemy, "order", 2);
        Set(firstEnemy, "isEnemyAtStart", !mobileTestMode);
        Set(secondEnemy, "isEnemyAtStart", !mobileTestMode);
        var galaxy = GalaxyWith(allied, firstEnemy, secondEnemy);

        var state = GalaxyRuntimeStateFactory.CreateNewGalaxyRuntimeState(
            galaxy, false, false, mobileTestMode, alliedSectorCount: 1);

        string[] factionIds = { "ai", "ancients", "infected" };
        Assert.That(state.EnemyFactions.Select(faction => faction.FactionId),
            Is.EqualTo(factionIds.Take(Math.Min(enemyCount, factionIds.Length))));
        Assert.That(state.Systems.Where(system => system.SystemStatus == StarSystemStatus.Captured)
            .Select(system => system.SystemId), Is.EqualTo(enemies.Select(system => system.Id)));
        Assert.That(state.Systems.All(system => system.Station.IsEnabled && !system.Station.IsDestroyed), Is.True);
        for (int index = 0; index < state.EnemyFactions.Count; index++)
        {
            var expected = enemies.Where((system, position) => position % factionIds.Length == index)
                .Select(system => system.Id);
            Assert.That(state.EnemyFactions[index].OwnedSystemIds, Is.EqualTo(expected));
            Assert.That(state.EnemyFactions[index].TerritorySystemIds, Is.EqualTo(expected));
        }
    }

    private StationConfig Station(string id, int level)
    {
        var station = Make<StationConfig>(id);
        Set(station, "initialLevel", level);
        Set(station, "stationTypeConfig", Make<StationTypeConfig>(id + "_type"));
        return station;
    }

    private SectorConfig SectorWith(string id, params StarSystemConfig[] systems)
    {
        var sector = Make<SectorConfig>(id);
        Set(sector, "systems", systems);
        return sector;
    }

    private GalaxyConfig GalaxyWith(params SectorConfig[] sectors)
    {
        var galaxy = Make<GalaxyConfig>("galaxy_factory_test");
        foreach (var sector in sectors) galaxy.Sectors.Add(sector);
        return galaxy;
    }

    private T Make<T>(string id) where T : BaseConfig
    {
        var asset = ScriptableObject.CreateInstance<T>();
        _assets.Add(asset);
        Set(asset, "id", id);
        return asset;
    }

    private static void Set(object target, string name, object value)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null) continue;
            field.SetValue(target, value);
            return;
        }
        throw new MissingFieldException(target.GetType().FullName, name);
    }
}
