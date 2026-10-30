using NUnit.Framework;

public sealed class S08_08_LootTransferEditModeTests
{
    [Test]
    public void LootCanOverfillCargoWithoutChangingNormalPurchaseGate()
    {
        var cargo = new RuntimeCargoInventory();
        Assert.That(cargo.AddItem("ore", 3, 2), Is.False);
        Assert.That(cargo.AddLootItem("ore", 3), Is.True);
        Assert.That(cargo.GetQuantity("ore"), Is.EqualTo(3));
        Assert.That(cargo.GetFreeCapacity(2), Is.Zero);
        Assert.That(cargo.AddItem("ore", 1, 2), Is.False);
    }

    [Test]
    public void PartialDropLeavesRemainderAndRejectsExcessQuantity()
    {
        var cargo = new RuntimeCargoInventory();
        Assert.That(cargo.AddLootItem("ore", 10), Is.True);
        Assert.That(cargo.RemoveAnyItem("ore", 3), Is.True);
        Assert.That(cargo.GetQuantity("ore"), Is.EqualTo(7));
        Assert.That(cargo.RemoveAnyItem("ore", 8), Is.False);
        Assert.That(cargo.GetQuantity("ore"), Is.EqualTo(7));
    }

    [Test]
    public void LootSnapshotKeepsIdentityPositionAndEquipmentInstance()
    {
        var loot = new SpaceLootRuntimeState
        {
            LootId = "loot-01",
            SystemId = "system-01",
            Position = new UnityEngine.Vector3(10f, 20f, -2f),
            Kind = SpaceLootKind.Equipment,
            Quantity = 1,
            Equipment = new SystemNpcWeaponRuntimeState
            {
                InstanceId = "weapon-01",
                WeaponConfigId = "laser-01",
                DurabilityPercent = 37
            }
        };
        SpaceLootRuntimeState copy = loot.Copy();
        Assert.That(copy.LootId, Is.EqualTo("loot-01"));
        Assert.That(copy.Position, Is.EqualTo(loot.Position));
        Assert.That(copy.Equipment.InstanceId, Is.EqualTo("weapon-01"));
        Assert.That(copy.Equipment.DurabilityPercent, Is.EqualTo(37));
        Assert.That(copy.Equipment, Is.Not.SameAs(loot.Equipment));
    }
}
