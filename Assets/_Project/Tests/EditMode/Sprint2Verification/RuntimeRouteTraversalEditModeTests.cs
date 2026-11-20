using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class RuntimeRouteTraversalEditModeTests
{
    private readonly List<Object> _assets = new();
    private StarSystemConfig[] _systems;
    private RouteConfig _ab, _bc, _ac;
    private GalaxyRuntimeState _galaxy;

    [SetUp]
    public void SetUp()
    {
        _systems = new[] { Asset<StarSystemConfig>("a"), Asset<StarSystemConfig>("b"), Asset<StarSystemConfig>("c") };
        _ab = Route("ab", _systems[0], _systems[1], 9, 8);
        _bc = Route("bc", _systems[1], _systems[2], 9, 8);
        _ac = Route("ac", _systems[0], _systems[2], 20, 3);
        _galaxy = new GalaxyRuntimeState
        {
            Systems = _systems.Select(s => new StarSystemRuntimeState { SystemId = s.Id, IsDiscovered = true }).ToList(),
            Routes = new List<RouteRuntimeState>
            {
                new() { RouteId = "ab", IsUnlocked = true },
                new() { RouteId = "bc", IsUnlocked = true },
                new() { RouteId = "ac", IsUnlocked = true }
            }
        };
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object asset in _assets) Object.DestroyImmediate(asset);
        _assets.Clear();
    }

    [Test]
    public void MissingRuntimeStateCannotOpenRouteUsingItsStartSetting()
    {
        _ab.IsLockedAtStart = false;
        _galaxy.Routes.Clear();
        Assert.IsFalse(_ab.IsUnlocked(_galaxy));
        Assert.IsFalse(_ab.IsUnlocked(null));
        Assert.IsEmpty(new GalaxyMapPathFinder().FindShortestPath(_systems, _galaxy, "a", "c"));
        Assert.IsFalse(GalaxyGraphFactory.CreateFromConfigs(_systems, _galaxy).AreNeighbors("a", "b"));
    }

    [Test]
    public void FuelDevelopmentLevelChangesTheCheapestPathInBothDirections()
    {
        var finder = new GalaxyMapPathFinder();
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, finder.FindShortestPath(_systems, _galaxy, "a", "c"));
        Assert.AreEqual(18, finder.CalculatePathCost(_systems, _galaxy, new List<string> { "a", "b", "c" }));
        _galaxy.Routes.Single(r => r.RouteId == "ac").DevelopmentLevel = 2;
        CollectionAssert.AreEqual(new[] { "a", "c" }, finder.FindShortestPath(_systems, _galaxy, "a", "c"));
        CollectionAssert.AreEqual(new[] { "c", "a" }, finder.FindShortestPath(_systems, _galaxy, "c", "a"));
        Assert.AreEqual(3, finder.CalculatePathCost(_systems, _galaxy, new List<string> { "a", "c" }));
    }

    [Test]
    public void ClosingRoutesDisconnectsThePathAndUnlockingRestoresIt()
    {
        _galaxy.Routes.Single(r => r.RouteId == "ab").IsUnlocked = false;
        _galaxy.Routes.Single(r => r.RouteId == "ac").IsUnlocked = false;
        var finder = new GalaxyMapPathFinder();
        Assert.IsEmpty(finder.FindShortestPath(_systems, _galaxy, "a", "c"));
        Assert.AreEqual(int.MaxValue, finder.CalculatePathCost(_systems, _galaxy, new List<string> { "a", "c" }));
        _galaxy.Routes.Single(r => r.RouteId == "ac").IsUnlocked = true;
        CollectionAssert.AreEqual(new[] { "a", "c" }, finder.FindShortestPath(_systems, _galaxy, "a", "c"));
        Assert.IsTrue(GalaxyGraphFactory.CreateFromConfigs(_systems, _galaxy).AreNeighbors("c", "a"));
    }

    [Test]
    public void NewGameCreatesOneStatePerRouteAttachedToTwoSystems()
    {
        var sector = Asset<SectorConfig>("sector");
        Set(sector, "systems", _systems);
        var galaxy = Asset<GalaxyConfig>("galaxy");
        galaxy.Sectors.Add(sector);
        GalaxyRuntimeState state = GalaxyRuntimeStateFactory.CreateNewGalaxyRuntimeState(galaxy, true, false);
        Assert.AreEqual(3, state.Routes.Count);
        Assert.AreEqual(3, state.Routes.Select(r => r.RouteId).Distinct().Count());
        Assert.IsTrue(state.Routes.All(r => r.IsUnlocked));
    }

    private T Asset<T>(string id) where T : BaseConfig
    {
        T asset = ScriptableObject.CreateInstance<T>();
        _assets.Add(asset);
        typeof(BaseConfig).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(asset, id);
        return asset;
    }

    private RouteConfig Route(string id, StarSystemConfig from, StarSystemConfig to, int level1, int level2)
    {
        var route = Asset<RouteConfig>(id);
        route.FromSystem = from;
        route.ToSystem = to;
        Set(route, "fuelByDevelopmentLevel", new List<int> { level1, level2 });
        from.Routes.Add(route);
        to.Routes.Add(route);
        return route;
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
