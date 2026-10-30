using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class S08_07_LootContentAndSaveEditModeTests
{
    private const string AllyRoot = "Assets/_Project/Content/Configs/Population/Ally";
    private const string EnemyRoot = "Assets/_Project/Content/Configs/Population/Enemies";
    private const string AstraliteRoot = "Assets/_Project/Content/Configs/Items/";

    [Test]
    public void EveryGeneratedNpcConfigHasItsOwnValidLootTable()
    {
        var tableIds = new HashSet<string>();
        int allyCount = 0;
        int enemyCount = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:AllyConfig", new[] { AllyRoot }))
        {
            var config = AssetDatabase.LoadAssetAtPath<AllyConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (config == null) continue;
            allyCount++;
            Assert.That(config.LootTable, Is.Not.Null, config.name);
            Assert.That(tableIds.Add(config.LootTable.Id), Is.True,
                config.name + " shares a table ID with another NPC config");
            Assert.That(config.LootTable.GetValidationErrors(), Is.Empty, config.name);
            Assert.That(config.LootTable.GetInitialCargoQuantity(),
                Is.LessThanOrEqualTo(config.BaseCargoCapacityMin), config.name);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:EnemyConfig", new[] { EnemyRoot }))
        {
            var config = AssetDatabase.LoadAssetAtPath<EnemyConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (config == null) continue;
            enemyCount++;
            Assert.That(config.LootTable, Is.Not.Null, config.name);
            Assert.That(tableIds.Add(config.LootTable.Id), Is.True,
                config.name + " shares a table ID with another NPC config");
            Assert.That(config.LootTable.GetValidationErrors(), Is.Empty, config.name);
            Assert.That(config.LootTable.GetInitialCargoQuantity(),
                Is.LessThanOrEqualTo(config.BaseCargoCapacity), config.name);
        }

        Assert.That(allyCount, Is.GreaterThanOrEqualTo(51), "Generated AllyConfig coverage is incomplete");
        Assert.That(enemyCount, Is.GreaterThanOrEqualTo(30), "Generated EnemyConfig coverage is incomplete");
    }

    [Test]
    public void AstraliteSpeciesResolveToDistinctNamedItemsWithVisibleIcons()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<EnemyAstraliteCatalogConfig>(
            EnemyRoot + "/enemy_astralite_catalog_01.asset");
        Assert.That(catalog, Is.Not.Null);

        var ids = new HashSet<string>();
        foreach (EnemyFaction2A species in new[]
                 { EnemyFaction2A.AI, EnemyFaction2A.Ancients, EnemyFaction2A.Infected })
        {
            ItemConfig item = catalog.GetItem(species);
            Assert.That(item, Is.Not.Null, species.ToString());
            Assert.That(item.Id, Is.Not.Empty, species.ToString());
            Assert.That(item.DisplayName, Is.Not.Empty, species.ToString());
            Assert.That(item.Icon, Is.Not.Null,
                species + " Astralite needs a visible icon in the loot panel");
            Assert.That(ids.Add(item.Id), Is.True,
                species + " uses another species' Astralite item");
            Assert.That(AssetDatabase.GetAssetPath(item), Does.StartWith(AstraliteRoot));
        }
    }

    [Test]
    public void GeneratedEquipmentHasAnIconForWorldLoot()
    {
        const string weaponRoot = "Assets/_Project/Content/Configs/Weapons";
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:WeaponConfig", new[] { weaponRoot }))
        {
            WeaponConfig weapon = AssetDatabase.LoadAssetAtPath<WeaponConfig>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (weapon == null)
                continue;
            count++;
            Assert.That(weapon.Icon, Is.Not.Null, weapon.name);
        }
        Assert.That(count, Is.GreaterThanOrEqualTo(300));
    }

    [Test]
    public void SpaceLootSaveRoundTripPreservesPositionQuantityAndEquipmentInstance()
    {
        var original = new GameRuntimeState();
        original.SystemNpcSimulation.SpaceLoot.Add(new SpaceLootRuntimeState
        {
            LootId = "loot-01",
            SourceNpcId = "npc-01",
            SourceBatchId = "batch-02",
            SystemId = "system-03",
            Position = new Vector3(101f, -84f, -2f),
            EjectionTargetPosition = new Vector3(201f, -84f, -2f),
            EjectionRemainingSeconds = 0.3f,
            Kind = SpaceLootKind.Equipment,
            EquipmentConfigId = "weapon-01",
            Quantity = 1,
            Equipment = new SystemNpcWeaponRuntimeState
            {
                InstanceId = "equipment-01",
                WeaponConfigId = "weapon-01",
                DurabilityPercent = 37,
                SlotIndex = 2,
                RolledStats = new WeaponRuntimeStats { Damage = 19, Range = 650f }
            }
        });

        var restored = JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(original));
        Assert.That(restored.SystemNpcSimulation.SpaceLoot, Has.Count.EqualTo(1));
        SpaceLootRuntimeState drop = restored.SystemNpcSimulation.SpaceLoot[0];
        Assert.That(drop.LootId, Is.EqualTo("loot-01"));
        Assert.That(drop.SourceBatchId, Is.EqualTo("batch-02"));
        Assert.That(drop.SystemId, Is.EqualTo("system-03"));
        Assert.That(drop.Position, Is.EqualTo(new Vector3(101f, -84f, -2f)));
        Assert.That(drop.EjectionTargetPosition, Is.EqualTo(new Vector3(201f, -84f, -2f)));
        Assert.That(drop.EjectionRemainingSeconds, Is.EqualTo(0.3f));
        Assert.That(drop.Quantity, Is.EqualTo(1));
        Assert.That(drop.Equipment.InstanceId, Is.EqualTo("equipment-01"));
        Assert.That(drop.Equipment.DurabilityPercent, Is.EqualTo(37));
        Assert.That(drop.Equipment.RolledStats.Damage, Is.EqualTo(19));
    }

    [Test]
    public void LandingRenumbersRemainingWholeBatchesWithoutChangingTheirIdentityOrCargoSpace()
    {
        var npc = new SystemNpcRuntimeState
        {
            CargoCapacity = 20,
            CargoBatches = new List<NpcCargoBatchRuntimeState>
            {
                new() { BatchId = "batch-02", ItemConfigId = "food", Quantity = 5, BatchNumber = 2 },
                new() { BatchId = "batch-03", ItemConfigId = "electronics", Quantity = 3, BatchNumber = 3 }
            },
            ProcessedLootRuleIndexes = new List<int> { 0, 1 },
            DeathLootProcessed = true
        };

        npc.ResetLootForNewFlight();

        Assert.That(npc.CargoBatches[0].BatchId, Is.EqualTo("batch-02"));
        Assert.That(npc.CargoBatches[0].BatchNumber, Is.EqualTo(1));
        Assert.That(npc.CargoBatches[1].BatchId, Is.EqualTo("batch-03"));
        Assert.That(npc.CargoBatches[1].BatchNumber, Is.EqualTo(2));
        Assert.That(npc.FreeCargoCapacity, Is.EqualTo(12));
        Assert.That(npc.ProcessedLootRuleIndexes, Is.Empty);
        Assert.That(npc.DeathLootProcessed, Is.False);
    }

    [Test]
    public void SpaceLootCopyKeepsEquipmentInstanceIndependent()
    {
        var original = new SpaceLootRuntimeState
        {
            LootId = "loot-01",
            SystemId = "system-01",
            SourceNpcType = SystemNpcType.Enemy,
            SourceNpcConfigId = "enemy_ancients_L01_01",
            CurrentHealth = 1,
            Quantity = 1,
            Equipment = new SystemNpcWeaponRuntimeState
            {
                InstanceId = "equipment-01", DurabilityPercent = 64
            }
        };

        SpaceLootRuntimeState copy = original.Copy();
        original.Equipment.DurabilityPercent = 1;
        original.LootId = "changed";

        Assert.That(copy.LootId, Is.EqualTo("loot-01"));
        Assert.That(copy.SourceNpcType, Is.EqualTo(SystemNpcType.Enemy));
        Assert.That(copy.SourceNpcConfigId, Is.EqualTo("enemy_ancients_L01_01"));
        Assert.That(copy.CurrentHealth, Is.EqualTo(1));
        Assert.That(copy.Equipment.InstanceId, Is.EqualTo("equipment-01"));
        Assert.That(copy.Equipment.DurabilityPercent, Is.EqualTo(64));
    }

    [Test]
    public void LootPanelShowsNameWholeQuantityAndCargoSpace()
    {
        string text = SpaceLootDisplayFormatter.Format(5, 2);
        Assert.That(text, Is.EqualTo("Количество: 5\nМесто в трюме: 10"));

        string empty = SpaceLootDisplayFormatter.Format(-3, -4);
        Assert.That(empty, Does.Contain("Количество: 0"));
        Assert.That(empty, Does.Contain("Место в трюме: 0"));
    }

    [Test]
    public void EjectionOffsetUsesConfiguredRadiusAndKeepsSystemMapDepth()
    {
        var origin = new Vector3(12f, -8f, -2f);
        Vector3 east = NpcLootProcessor.ComputeEjectionPosition(origin, 100f, 0f);
        Vector3 north = NpcLootProcessor.ComputeEjectionPosition(origin, 100f, Mathf.PI / 2f);

        Assert.That(Vector2.Distance(new Vector2(origin.x, origin.y), new Vector2(east.x, east.y)),
            Is.EqualTo(100f).Within(0.001f));
        Assert.That(Vector2.Distance(new Vector2(origin.x, origin.y), new Vector2(north.x, north.y)),
            Is.EqualTo(100f).Within(0.001f));
        Assert.That(east.z, Is.EqualTo(origin.z));
        Assert.That(north.z, Is.EqualTo(origin.z));
        Assert.That(east.x, Is.GreaterThan(origin.x));
        Assert.That(north.y, Is.GreaterThan(origin.y));
        Assert.That(NpcLootProcessor.ComputeEjectionPosition(origin, 0f, 1f), Is.EqualTo(origin));
    }

    [TestCase(51, 50, 100, 50, true)]
    [TestCase(50, 49, 100, 50, false)]
    [TestCase(49, 0, 100, 50, false)]
    [TestCase(100, 0, 100, 50, true)]
    [TestCase(100, 99, 100, 50, false)]
    public void HullThresholdRequiresCrossingFromAbove(
        int before, int after, int max, int thresholdPercent, bool expected)
    {
        Assert.That(NpcLootProcessor.CrossedThreshold(
            before, after, max, thresholdPercent), Is.EqualTo(expected));
    }
}
