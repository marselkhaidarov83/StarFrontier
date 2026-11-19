using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ShipEquipmentProgressionTests
{
    [Test]
    public void InstanceRollStaysInsideRangesAndSurvivesSaveAndLootCopy()
    {
        var config = ScriptableObject.CreateInstance<ShipEquipmentConfig>();
        try
        {
            config.CargoSizeMin = 4; config.CargoSizeMax = 5;
            config.SpeedMin = 100; config.SpeedMax = 120;
            config.FuelCapacityMin = 200; config.FuelCapacityMax = 220;
            config.ShieldMin = 300; config.ShieldMax = 325;
            config.GrabberStrengthMin = 20; config.GrabberStrengthMax = 25;
            for (int seed = 0; seed < 100; seed++)
            {
                var instance = SystemNpcWeaponRuntimeState.NewComponent(config, "equipment-" + seed);
                var stats = instance.ComponentStats;
                Assert.That(stats.CargoSize, Is.InRange(4, 5));
                Assert.That(stats.Speed, Is.InRange(100f, 120f));
                Assert.That(stats.FuelCapacity, Is.InRange(200, 220));
                Assert.That(stats.ShieldCapacity, Is.InRange(300, 325));
                Assert.That(stats.Strength, Is.InRange(20, 25));
                Assert.AreEqual(stats.CargoSize, instance.RolledStats.CargoSize);
                string json = JsonUtility.ToJson(instance);
                var restored = JsonUtility.FromJson<SystemNpcWeaponRuntimeState>(json);
                config.SpeedMin = 999; config.SpeedMax = 1000;
                Assert.AreEqual(stats.Speed, restored.GetComponentStats(config).Speed);
                var copy = new SpaceLootRuntimeState { Equipment = restored }.Copy();
                Assert.AreEqual(stats.Speed, copy.Equipment.ComponentStats.Speed);
                Assert.AreNotSame(restored.ComponentStats, copy.Equipment.ComponentStats);
                copy.Equipment.ComponentStats.Speed = -1;
                Assert.AreEqual(stats.Speed, restored.ComponentStats.Speed);
                config.SpeedMin = 100; config.SpeedMax = 120;
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
    }

    [Test]
    public void EverySlotHasThreeVariantsTenLevelsAndIncreasingRangesAndCargo()
    {
        var equipment = AssetDatabase.FindAssets("t:ShipEquipmentConfig")
            .Select(g => AssetDatabase.LoadAssetAtPath<ShipEquipmentConfig>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        Assert.AreEqual(210, equipment.Length);
        foreach (ShipComponentSlot slot in Enum.GetValues(typeof(ShipComponentSlot)))
        foreach (ShipEquipmentVariant variant in Enum.GetValues(typeof(ShipEquipmentVariant)))
        {
            var levels = equipment.Where(c => c.Slot == slot && c.Variant == variant).OrderBy(c => c.Level).ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(1, 10), levels.Select(c => c.Level));
            Assert.AreEqual(10, levels.Length);
            for (int i = 1; i < levels.Length; i++)
            {
                Assert.Greater(levels[i].CargoSizeMin, levels[i - 1].CargoSizeMin);
                Assert.Greater(levels[i].CargoSizeMax, levels[i - 1].CargoSizeMax);
                var previous = levels[i - 1].RollRuntimeStats(7);
                var current = levels[i].RollRuntimeStats(7);
                Assert.Greater(MainStat(slot, current), MainStat(slot, previous));
            }
            Assert.IsTrue(levels.All(c => c.Icon != null));
        }
    }

    [Test]
    public void EveryMarketLevelOffersAllEquipmentAndAlliedWeaponsUpToThatLevel()
    {
        var profiles = AssetDatabase.FindAssets("t:MarketProfileConfig")
            .Select(g => AssetDatabase.LoadAssetAtPath<MarketProfileConfig>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        Assert.AreEqual(10, profiles.Length);
        foreach (var profile in profiles)
        foreach (var level in profile.GalaxyLevels)
        {
            var components = level.Equipment.Where(r => r.Component != null).ToArray();
            var weapons = level.Equipment.Where(r => r.Weapon != null).ToArray();
            Assert.AreEqual(21 * level.GalaxyLevel, components.Length, profile.Id);
            Assert.AreEqual(15 * level.GalaxyLevel, weapons.Length, profile.Id);
            Assert.IsTrue(components.All(r => r.Component.Level <= level.GalaxyLevel && r.Allied));
            Assert.IsTrue(weapons.All(r => r.Weapon.Level <= level.GalaxyLevel && r.Allied && r.Weapon.Id.StartsWith("weapon_common_")));
            Assert.AreEqual(level.Equipment.Count, level.Equipment.Select(r => r.EquipmentId).Distinct().Count());
        }
    }

    private static float MainStat(ShipComponentSlot slot, ShipEquipmentRuntimeStats stats)
    {
        switch (slot)
        {
            case ShipComponentSlot.Engine: return stats.Speed;
            case ShipComponentSlot.FuelTank: return stats.FuelCapacity;
            case ShipComponentSlot.Radar: return stats.RadarRange;
            case ShipComponentSlot.Scanner: return stats.ScannerRange;
            case ShipComponentSlot.RepairDroid: return stats.RepairPerCycle;
            case ShipComponentSlot.Grabber: return stats.Strength;
            default: return stats.ShieldCapacity;
        }
    }
}
