using NUnit.Framework;
using UnityEditor;

public sealed class S08_08_LootContentEditModeTests
{
    private const string Content = "Assets/_Project/Content/Configs/";

    [Test]
    public void PlayerAndEveryRangerUseOneStandardGrabber()
    {
        var grabber = AssetDatabase.LoadAssetAtPath<GrabberConfig>(
            Content + "Game/grabber_standard_01.asset");
        Assert.That(grabber, Is.Not.Null);
        Assert.That(grabber.Strength, Is.EqualTo(5));
        Assert.That(grabber.Range, Is.EqualTo(100f));
        Assert.That(grabber.PullSpeedPerTick, Is.EqualTo(25f));

        var newGame = AssetDatabase.LoadAssetAtPath<NewGameConfig>(
            Content + "Game/newGameConfig_01.asset");
        Assert.That(newGame, Is.Not.Null);
        Assert.That(newGame.StarterShipConfig, Is.Not.Null);
        Assert.That(newGame.StarterShipConfig.Grabber, Is.SameAs(grabber));

        int rangerCount = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AllyConfig",
                     new[] { Content + "Population/Ally/Ranger" }))
        {
            var ranger = AssetDatabase.LoadAssetAtPath<AllyConfig>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (ranger == null) continue;
            rangerCount++;
            Assert.That(ranger.Grabber, Is.SameAs(grabber), ranger.name);
        }
        Assert.That(rangerCount, Is.GreaterThanOrEqualTo(11));
    }

    [Test]
    public void CurrencyItemsHaveIconsAndDoNotUseCargoSpace()
    {
        foreach (string itemName in new[] { "credits", "diamonds" })
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemConfig>(
                Content + "Items/item_" + itemName + "_01.asset");
            Assert.That(item, Is.Not.Null, itemName);
            Assert.That(item.Icon, Is.Not.Null, itemName);
            Assert.That(item.ItemCategory, Is.EqualTo(ItemCategory.Currency), itemName);
            Assert.That(item.CargoSize, Is.Zero, itemName);
            Assert.That(new ItemConfigValidator().Validate(item), Is.Empty, itemName);
        }
    }

    [Test]
    public void EveryEnemyRollsOneToTwoDiamondsOnlyOnDeath()
    {
        var diamonds = AssetDatabase.LoadAssetAtPath<ItemConfig>(
            Content + "Items/item_diamonds_01.asset");
        int enemyCount = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:EnemyConfig",
                     new[] { Content + "Population/Enemies" }))
        {
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyConfig>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (enemy == null) continue;
            enemyCount++;
            Assert.That(enemy.LootTable, Is.Not.Null, enemy.name);

            int deathDiamondEntries = 0;
            foreach (NpcLootTriggerRule rule in enemy.LootTable.Rules)
            {
                if (rule?.Generated == null) continue;
                foreach (NpcLootGeneratedEntry entry in rule.Generated)
                {
                    if (entry == null || entry.Kind != NpcLootGeneratedKind.Diamonds)
                        continue;
                    Assert.That(rule.IsDeath, Is.True, enemy.name);
                    Assert.That(entry.Item, Is.SameAs(diamonds), enemy.name);
                    Assert.That(entry.MinAmount, Is.EqualTo(1), enemy.name);
                    Assert.That(entry.MaxAmount, Is.EqualTo(2), enemy.name);
                    Assert.That(entry.ChancePercent, Is.EqualTo(50f), enemy.name);
                    deathDiamondEntries++;
                }
            }
            Assert.That(deathDiamondEntries, Is.EqualTo(1), enemy.name);
        }
        Assert.That(enemyCount, Is.GreaterThanOrEqualTo(30));
    }
}
