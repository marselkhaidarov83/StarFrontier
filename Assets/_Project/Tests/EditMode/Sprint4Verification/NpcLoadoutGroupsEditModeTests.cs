using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class NpcLoadoutGroupsEditModeTests
{
    private readonly List<Object> _objects = new();
    private Random.State _randomState;

    [SetUp] public void SetUp() { _randomState = Random.state; Random.InitState(9487); }
    [TearDown] public void TearDown()
    {
        Random.state = _randomState;
        foreach (Object item in _objects) Object.DestroyImmediate(item);
        _objects.Clear();
    }

    [Test]
    public void CreationUsesTheChosenHullAndRolledComponentStatistics()
    {
        AllyConfig actor = Actor(2, ShipComponentSlots.Engine | ShipComponentSlots.ShieldGenerator);
        var hullIds = new HashSet<string>();
        for (int i = 0; i < 64; i++)
        {
            var npc = SystemNpcRuntimeFactory.CreateAlly(actor, "s", "s", null, Vector3.zero);
            HullConfig hull = actor.HullGroup.Resolve(npc.InstalledHull.ConfigId);
            hullIds.Add(hull.Id);
            Assert.That(npc.MaxHull, Is.InRange(hull.MaximumHullMin, hull.MaximumHullMax));
            Assert.AreEqual(npc.InstalledHull.MaximumDurability, npc.MaxHull);
            Assert.AreEqual(npc.MaxHull, npc.CurrentHull);
            Assert.That(npc.CargoCapacity, Is.InRange(hull.CargoCapacityMin, hull.CargoCapacityMax));
            Assert.That(npc.InstalledHull.Acceleration, Is.InRange(hull.AccelerationMin, hull.AccelerationMax));
            Assert.AreEqual(hull.TurnRadius, npc.TurnRadius);
            var engine = ShipLoadoutRules.Component(npc, ShipComponentSlot.Engine);
            var field = ShipLoadoutRules.Component(npc, ShipComponentSlot.ShieldGenerator);
            Assert.AreEqual(engine.ComponentStats.Speed, npc.Speed);
            Assert.AreEqual(field.ComponentStats.ShieldCapacity, npc.MaxShield);
            Assert.AreEqual(npc.MaxShield, npc.CurrentShield);
            Assert.AreEqual(2, npc.InstalledComponents.Count);
            Assert.AreEqual(0, npc.MaxEnergy);
        }
        Assert.AreEqual(2, hullIds.Count, "Both hull alternatives should be selectable.");
    }

    [Test]
    public void EachEquipmentSlotIsSelectedIndependently()
    {
        AllyConfig actor = Actor(2, ShipComponentSlots.Engine | ShipComponentSlots.ShieldGenerator);
        var pairs = new HashSet<string>();
        for (int i = 0; i < 96; i++)
        {
            var npc = SystemNpcRuntimeFactory.CreateAlly(actor, "s", "s", null, Vector3.zero);
            pairs.Add(string.Join("/", npc.InstalledComponents.Select(x => x.ComponentConfigId)));
        }
        Assert.Greater(pairs.Count, 3, "Selection must combine alternatives across slots, rather than select a fixed kit.");
    }

    [Test]
    public void StarterHullHasOnlyAnEngineAndCannotAppearOnTheMarket()
    {
        AllyConfig actor = Actor(0, ShipComponentSlots.Engine);
        var npc = SystemNpcRuntimeFactory.CreateAlly(actor, "s", "s", null, Vector3.zero);
        Assert.AreEqual(1, npc.InstalledComponents.Count);
        Assert.AreEqual((int)ShipComponentSlot.Engine, npc.InstalledComponents[0].SlotIndex);
        Assert.IsEmpty(npc.Weapons);
        Assert.AreEqual(0, npc.MaxShield);
        Assert.IsFalse(actor.HullGroup.Resolve(npc.InstalledHull.ConfigId).MarketAvailable);
    }

    private AllyConfig Actor(int level, ShipComponentSlots slots)
    {
        var actor = Asset<AllyConfig>("actor"); Set(actor, "level", level);
        var group = Asset<HullGroupConfig>("hulls"); group.Level = level;
        var hulls = new List<HullConfig>();
        for (int i = 0; i < 2; i++)
        {
            var hull = Asset<HullConfig>("hull" + i); hull.Level = level; hull.Hull = actor;
            hull.MaximumHullMin = 100 + i * 100; hull.MaximumHullMax = hull.MaximumHullMin + 20;
            hull.CargoCapacityMin = 400 + i * 100; hull.CargoCapacityMax = hull.CargoCapacityMin + 20;
            hull.AccelerationMin = 1.25f + i; hull.AccelerationMax = 1.75f + i;
            hull.TurnRadius = 300 + i * 50; hull.AvailableComponentSlots = slots;
            hull.WeaponSlotCount = level == 0 ? 0 : 1; hulls.Add(hull);
        }
        Set(group, "hullConfigs", hulls); Set(actor, "hullGroup", group);
        var equipment = Asset<EquipmentGroupConfig>("equipment"); equipment.Level = level;
        var types = new List<EquipmentSlotGroup>();
        foreach (ShipComponentSlot slot in new[] { ShipComponentSlot.Engine, ShipComponentSlot.ShieldGenerator })
        {
            var type = new EquipmentSlotGroup { Slot = slot };
            for (int i = 0; i < 3; i++)
            {
                var item = Asset<ShipEquipmentConfig>(slot + "_" + i); item.Level = Mathf.Max(1, level); item.Slot = slot;
                item.SpeedMin = 10 + i * 10; item.SpeedMax = item.SpeedMin + 4;
                item.ShieldMin = 30 + i * 10; item.ShieldMax = item.ShieldMin + 4;
                type.Equipment.Add(item);
            }
            types.Add(type);
        }
        Set(equipment, "slotGroups", types); Set(actor, "equipmentGroup", equipment);
        return actor;
    }

    private T Asset<T>(string id) where T : BaseConfig
    {
        T item = ScriptableObject.CreateInstance<T>(); _objects.Add(item); Set(item, "id", id); return item;
    }
    private static void Set(object target, string field, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var info = type.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (info != null) { info.SetValue(target, value); return; }
        }
        throw new System.MissingFieldException(field);
    }
}
