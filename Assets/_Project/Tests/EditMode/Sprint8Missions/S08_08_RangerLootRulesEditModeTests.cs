using NUnit.Framework;

public sealed class S08_08_RangerLootRulesEditModeTests
{
    private static SystemNpcRuntimeState Ranger() => new()
    {
        RuntimeNpcId = "ranger-01",
        NpcType = SystemNpcType.Ally,
        AllyRole = AllyRole2A.Ranger,
        CurrentSystemId = "system-01",
        IsAlive = true
    };

    private static SpaceLootRuntimeState Loot() => new()
    {
        LootId = "loot-01",
        SystemId = "system-01",
        Kind = SpaceLootKind.Item,
        Quantity = 3,
        CurrentHealth = 1
    };

    [Test]
    public void RangerChecksWholeLootMassAgainstGrabberAndRemainingCargo()
    {
        SystemNpcRuntimeState ranger = Ranger();
        SpaceLootRuntimeState loot = Loot();

        Assert.That(RangerLootCollectionRules2A.CanCollect(
            ranger, loot, 6, 5, 20), Is.False,
            "A whole space batch heavier than the grabber cannot be split");
        Assert.That(RangerLootCollectionRules2A.CanCollect(
            ranger, loot, 5, 5, 4), Is.False,
            "A ranger does not take a batch beyond remaining capacity");
        Assert.That(RangerLootCollectionRules2A.CanCollect(
            ranger, loot, 5, 5, 5), Is.True);
    }

    [Test]
    public void RangerRejectsLootInAnotherSystemAndAllowsZeroCargoCurrency()
    {
        SystemNpcRuntimeState ranger = Ranger();
        SpaceLootRuntimeState loot = Loot();
        loot.SystemId = "system-02";
        Assert.That(RangerLootCollectionRules2A.CanCollect(
            ranger, loot, 0, 5, 0), Is.False);

        loot.SystemId = ranger.CurrentSystemId;
        loot.Kind = SpaceLootKind.Diamonds;
        Assert.That(RangerLootCollectionRules2A.CanCollect(
            ranger, loot, 0, 5, 0), Is.True);

        loot.PullCollectorId = "another-ranger";
        Assert.That(RangerLootCollectionRules2A.CanCollect(
            ranger, loot, 0, 5, 0), Is.False,
            "A claimed trophy must not be selected by a second collector");
    }
}
