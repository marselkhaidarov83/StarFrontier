using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class S08_02_MissionGameplayEditModeTests
{
    private readonly List<UnityEngine.Object> _objects = new();

    [TearDown]
    public void Cleanup()
    {
        foreach (var item in _objects) UnityEngine.Object.DestroyImmediate(item);
        _objects.Clear();
    }

    [Test]
    public void TemplateReferences_ArePolymorphic()
    {
        MissionTemplateConfig delivery = Create<DeliveryMissionTemplateConfig>();
        MissionTemplateConfig recon = Create<ReconMissionTemplateConfig>();
        Assert.That(delivery.MissionType, Is.EqualTo(MissionType.Delivery));
        Assert.That(recon.MissionType, Is.EqualTo(MissionType.Recon));
    }

    [Test]
    public void CargoSelection_RespectsConfiguredMaximumAndActualVolume()
    {
        var template = Delivery();
        var item = Item("heavy", 2);
        Set(template, "cargoOptions", new List<DeliveryCargoOption> { new DeliveryCargoOption { Item = item, Weight = 1 } });
        Assert.That(DeliveryMissionCargoSelector.TrySelect(template, 7, id => true, 0, 1, out var selected, out int quantity), Is.True);
        Assert.That(selected, Is.SameAs(item));
        Assert.That(quantity, Is.EqualTo(3));
        Assert.That(quantity * item.CargoSize, Is.LessThanOrEqualTo(7));
        Set(template, "maximumQuantity", 2);
        Assert.That(DeliveryMissionCargoSelector.TrySelect(template, 99, id => true, 0, 1, out selected, out quantity), Is.True);
        Assert.That(quantity, Is.EqualTo(2));
    }

    [Test]
    public void CargoSelection_ExcludesDisabledOrNonFittingGoods()
    {
        var template = Delivery();
        var disabled = Item("disabled", 1); var heavy = Item("heavy", 4); var light = Item("light", 1);
        Set(template, "cargoOptions", new List<DeliveryCargoOption>
        {
            new DeliveryCargoOption { Item = disabled, Weight = 0 },
            new DeliveryCargoOption { Item = heavy, Weight = 100 },
            new DeliveryCargoOption { Item = light, Weight = 1 }
        });
        Assert.That(DeliveryMissionCargoSelector.TrySelect(template, 2, id => true, 0, 0, out var selected, out int quantity), Is.True);
        Assert.That(selected, Is.SameAs(light));
        Assert.That(DeliveryMissionCargoSelector.TrySelect(template, 0, id => true, 0, 0, out selected, out quantity), Is.False);
    }

    [Test]
    public void OrdinaryTrade_CannotConsumeMissionCargoOfTheSameItem()
    {
        var cargo = new RuntimeCargoInventory();
        cargo.entries.Add(new RuntimeCargoEntryData("goods", 5) { MissionRuntimeId = "contract" });
        cargo.entries.Add(new RuntimeCargoEntryData("goods", 2));
        Assert.That(cargo.GetQuantity("goods"), Is.EqualTo(7));
        Assert.That(cargo.GetTradeableQuantity("goods"), Is.EqualTo(2));
        Assert.That(cargo.RemoveItem("goods", 3), Is.False);
        Assert.That(cargo.RemoveItem("goods", 2), Is.True);
        Assert.That(cargo.GetQuantity("goods"), Is.EqualTo(5));
        Assert.That(cargo.RemoveItem("goods", 1), Is.False);
    }

    [Test]
    public void DeliveryCleanup_RemovesOnlyItsOwnPhysicalStack()
    {
        var state = new GameRuntimeState();
        var ship = new ShipRuntimeData { ShipId = "ship" };
        state.Player.PlayerShipState.OwnedShips.Add(ship);
        state.Player.PlayerShipState.ActiveShipId = ship.ShipId;
        ship.Cargo.entries.Add(new RuntimeCargoEntryData("goods", 5) { MissionRuntimeId = "contract" });
        ship.Cargo.entries.Add(new RuntimeCargoEntryData("goods", 7));
        var mission = new MissionInstanceData { MissionRuntimeId = "contract", Objective = new MissionObjectiveData { MissionCargoItemId = "goods", MissionCargoAmount = 5 } };
        Assert.That(MissionCargoOperations.HasCargo(state, mission, true), Is.True);
        MissionCargoOperations.Remove(state, mission);
        Assert.That(ship.Cargo.GetQuantity("goods"), Is.EqualTo(7));
        Assert.That(MissionCargoOperations.HasCargo(state, mission, true), Is.False);
    }

    [Test]
    public void ReconDepth_IsMeasuredFromLastAlliedSystem_NotIssuerDistance()
    {
        var a = System("a"); var e1 = System("e1"); var e2 = System("e2");
        var f = System("f"); var b = System("b"); var e3 = System("e3");
        Connect(a, e1); Connect(e1, e2); Connect(a, f); Connect(f, b); Connect(b, e3);
        var galaxy = new GalaxyRuntimeState();
        foreach (var system in new[] { a, e1, e2, f, b, e3 })
            galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = system.Id,
                SystemStatus = system.Id.StartsWith("e", StringComparison.Ordinal) ? StarSystemStatus.Captured : StarSystemStatus.Stable });
        var result = ReconMissionTargetSelector.BuildCandidates(galaxy, new[] { a, e1, e2, f, b, e3 }, a.Id, route => true);
        Assert.That(result.Any(c => c.SystemId == a.Id || c.SystemId == b.Id || c.SystemId == f.Id), Is.False);
        Assert.That(result.Single(c => c.SystemId == e2.Id).EnemyJumps, Is.EqualTo(2));
        Assert.That(result.Single(c => c.SystemId == e3.Id).EnemyJumps, Is.EqualTo(1));
        Assert.That(result.Single(c => c.SystemId == e3.Id).LastAlliedSystemId, Is.EqualTo(b.Id));
    }

    [Test]
    public void ReconPicker_UsesGroupWeight_WithoutMultiplyingByNumberOfSystems()
    {
        var template = Create<ReconMissionTemplateConfig>();
        Set(template, "distanceWeights", new List<ReconDistanceWeight>
        { new ReconDistanceWeight { EnemyJumps = 1, Weight = 1 }, new ReconDistanceWeight { EnemyJumps = 2, Weight = 1 } });
        var candidates = new[]
        {
            new ReconMissionCandidate { SystemId = "a", EnemyJumps = 1 },
            new ReconMissionCandidate { SystemId = "b", EnemyJumps = 1 },
            new ReconMissionCandidate { SystemId = "c", EnemyJumps = 2 }
        };
        Assert.That(ReconMissionTargetSelector.Pick(candidates, template, 0.6, 0).EnemyJumps, Is.EqualTo(2));
        Set(template, "furtherJumpWeightMultiplier", 0.5f);
        Assert.That(template.GetDistanceWeight(3), Is.EqualTo(0.5));
    }

    [Test]
    public void DeliveryTarget_UsesUnlockedAlliedPathAndActualFuelCost()
    {
        var source = System("source"); var intermediate = System("intermediate");
        var target = System("target"); var enemy = System("enemy");
        var planet = Create<PlanetConfig>(); Set(planet, "id", "target_planet");
        Set(planet, "isInhabited", true); Set(target, "planetRefs", new[] { planet });
        var direct = Create<RouteConfig>(); Set(direct, "id", "direct");
        direct.FromSystem = source; direct.ToSystem = target;
        var first = Create<RouteConfig>(); Set(first, "id", "first");
        first.FromSystem = source; first.ToSystem = intermediate;
        var second = Create<RouteConfig>(); Set(second, "id", "second");
        second.FromSystem = intermediate; second.ToSystem = target;
        var hostile = Create<RouteConfig>(); Set(hostile, "id", "hostile");
        hostile.FromSystem = target; hostile.ToSystem = enemy;
        source.Routes.AddRange(new[] { direct, first });
        intermediate.Routes.AddRange(new[] { first, second });
        target.Routes.AddRange(new[] { direct, second, hostile });
        enemy.Routes.Add(hostile);
        var galaxy = new GalaxyRuntimeState();
        foreach (var system in new[] { source, intermediate, target, enemy })
            galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = system.Id,
                IsDiscovered = true, SystemStatus = system == enemy ? StarSystemStatus.Captured : StarSystemStatus.Stable });
        int Cost(string a, string b) => (a == source.Id && b == target.Id) ||
            (a == target.Id && b == source.Id) ? 7 : 2;
        var choices = DeliveryMissionTargetSelector.BuildCandidates(galaxy,
            new[] { source, intermediate, target, enemy }, source.Id, route => true, Cost);
        Assert.That(choices.Count, Is.EqualTo(1));
        Assert.That(choices[0].System, Is.SameAs(target));
        Assert.That(choices[0].FuelCost, Is.EqualTo(4));
        Assert.That(DeliveryMissionTargetSelector.CalculateCredits(choices[0].FuelCost, 10f), Is.EqualTo(40));
        Assert.That(DeliveryMissionTargetSelector.BuildCandidates(galaxy,
            new[] { source, intermediate, target, enemy }, source.Id,
            route => route.Id == "direct", Cost), Is.Empty);
    }

    [Test]
    public void ReconSurvey_ReservesTenPercentAndCountsEachEnemyOnce()
    {
        var config = Create<ReconMissionTemplateConfig>();
        Set(config, "minimumEnemyCount", 1); Set(config, "maximumEnemyCount", 15);
        Set(config, "enemyReservePercent", 10);
        Assert.That(ReconMissionSurveyRules.PickRequired(10, config, 1), Is.EqualTo(9));
        Assert.That(ReconMissionSurveyRules.PickRequired(1, config, 1), Is.Zero);
        var enemies = new[]
        {
            Enemy("one", 0), Enemy("two", 3), Enemy("far", 30)
        };
        var found = ReconMissionSurveyRules.FindNewNearbyEnemyIds(enemies,
            new[] { "one" }, Vector3.zero, 5f, 2);
        Assert.That(found, Is.EqualTo(new[] { "two" }));
    }

    private static SystemNpcRuntimeState Enemy(string id, float x) => new SystemNpcRuntimeState
    {
        RuntimeNpcId = id, NpcType = SystemNpcType.Enemy, IsAlive = true,
        LifeState = SystemNpcLifeState.Alive, CurrentPosition = new Vector3(x, 0, 0)
    };

    private DeliveryMissionTemplateConfig Delivery()
    {
        var config = Create<DeliveryMissionTemplateConfig>();
        Set(config, "minimumQuantity", 1); Set(config, "maximumQuantity", 50);
        return config;
    }
    private ItemConfig Item(string id, int size)
    {
        var config = Create<ItemConfig>(); Set(config, "id", id); Set(config, "cargoSize", size); return config;
    }
    private StarSystemConfig System(string id)
    {
        var config = Create<StarSystemConfig>(); Set(config, "id", id); return config;
    }
    private void Connect(StarSystemConfig a, StarSystemConfig b)
    {
        var route = Create<RouteConfig>(); route.FromSystem = a; route.ToSystem = b;
        a.Routes.Add(route); b.Routes.Add(route);
    }
    private T Create<T>() where T : ScriptableObject
    {
        var result = ScriptableObject.CreateInstance<T>(); _objects.Add(result); return result;
    }
    private static void Set(object target, string field, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (info == null) continue;
            info.SetValue(target, value); return;
        }
        throw new InvalidOperationException("Missing field: " + field);
    }
}
