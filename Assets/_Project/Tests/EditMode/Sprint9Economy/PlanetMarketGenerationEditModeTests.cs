using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed partial class PlanetMarketEconomyTests
{
    [Test]
    public void RefreshUsesThreeQuotasAndPreservesPurchasedEquipment()
    {
        var settings = _profile.GalaxyLevels[0];
        settings.HullOfferCount = 2; settings.WeaponOfferCount = 3; settings.EquipmentOfferCount = 4;
        var hull = Asset<HullConfig>("batch-hull");
        settings.Hulls.Add(new MarketHullRule { Hull = hull, NewPrice = 300, Weight = 100 });
        var component = Asset<ShipEquipmentConfig>("batch-engine");
        _config.Components.Add(component);
        settings.Equipment.Add(new MarketEquipmentRule { Component = component, NewPrice = 100, Weight = 100 });
        Market.OffersInitialized = false;
        var initial = Market;
        Assert.AreEqual(2, initial.Hulls.Count);
        Assert.AreEqual(3, initial.Equipment.Count(x => !x.Equipment.IsComponent));
        Assert.AreEqual(4, initial.Equipment.Count(x => x.Equipment.IsComponent));
        var ids = initial.Equipment.Select(x => x.Equipment.InstanceId).Concat(initial.Hulls.Select(x => x.Hull.InstanceId)).ToArray();
        var bought = initial.Equipment.First(x => !x.Equipment.IsComponent).Equipment;
        Assert.IsTrue(_market.TradeEquipment(bought.InstanceId, true, "batch-buy", -1, out var reason, true), reason);
        Assert.AreEqual(2, Market.Equipment.Count(x => !x.Equipment.IsComponent));
        _market.AdvanceTime(10);
        Assert.AreSame(bought, Market.PersonalEquipment.Single());
        Assert.AreEqual(2, Market.Hulls.Count);
        Assert.AreEqual(3, Market.Equipment.Count(x => !x.Equipment.IsComponent));
        Assert.AreEqual(4, Market.Equipment.Count(x => x.Equipment.IsComponent));
        Assert.IsFalse(Market.Equipment.Any(x => ids.Contains(x.Equipment.InstanceId)));
        Assert.IsFalse(Market.Hulls.Any(x => ids.Contains(x.Hull.InstanceId)));
        Assert.AreEqual(57, Stock.Quantity, "Replacing equipment must not reset ordinary trade stocks.");
    }

    [Test]
    public void MarketLevelFollowsResearchInsteadOfSectorCount()
    {
        _profile.GalaxyLevels.Add(new MarketGalaxyLevelSettings
        {
            GalaxyLevel = 2, WeaponOfferCount = 3,
            Equipment = new List<MarketEquipmentRule> { new MarketEquipmentRule { Weapon = _weapon, NewPrice = 100 } }
        });
        _session.State.Galaxy.Sectors.Add(new SectorRuntimeState { SectorId = "another-sector", IsUnlocked = true });
        Assert.AreEqual(1, Market.AppliedGalaxyLevel);
        _session.State.Galaxy.Research.Levels.Find(x => x.Upgrade == ResearchUpgrade.TradeMarketImprovement).Level = 2;
        _events.PublishSafely(new ResearchCompletedEvent(ResearchUpgrade.TradeMarketImprovement, 2), _ => { });
        Assert.AreEqual(2, Market.AppliedGalaxyLevel);
        Assert.AreEqual(3, Market.Equipment.Count);
    }

    [Test]
    public void WeightsBiasModelsAndExcludeZeroWeightAndLockedLevels()
    {
        var higher = Asset<WeaponConfig>("weighted-high"); Set(higher, "level", 10);
        var locked = Asset<WeaponConfig>("weighted-locked"); Set(locked, "level", 11);
        var disabled = Asset<WeaponConfig>("weighted-disabled");
        _profile.GalaxyLevels.Add(new MarketGalaxyLevelSettings
        {
            GalaxyLevel = 10, WeaponOfferCount = 10000,
            Equipment = new List<MarketEquipmentRule>
            {
                new MarketEquipmentRule { Weapon = _weapon, NewPrice = 100, Weight = 100 },
                new MarketEquipmentRule { Weapon = higher, NewPrice = 200, Weight = 82 },
                new MarketEquipmentRule { Weapon = locked, NewPrice = 200, Weight = 100000 },
                new MarketEquipmentRule { Weapon = disabled, NewPrice = 200, Weight = 0 }
            }
        });
        _session.State.Galaxy.Research.Levels.Find(x => x.Upgrade == ResearchUpgrade.TradeMarketImprovement).Level = 10;
        _events.PublishSafely(new ResearchCompletedEvent(ResearchUpgrade.TradeMarketImprovement, 10), _ => { });
        var items = Market.Equipment;
        Assert.AreEqual(10000, items.Count);
        Assert.IsTrue(items.All(x => x.Equipment.WeaponConfigId == _weapon.Id || x.Equipment.WeaponConfigId == higher.Id));
        double lowerShare = items.Count(x => x.Equipment.WeaponConfigId == _weapon.Id) / 10000.0;
        Assert.That(lowerShare, Is.InRange(.52, .58));
        Assert.AreEqual(items.Count, items.Select(x => x.Equipment.InstanceId).Distinct().Count());
    }

    [Test]
    public void OfferRefreshPeriodIsIndependentOfTradeStockInterval()
    {
        _profile.OfferRefreshIntervalSeconds = 30;
        Market.OffersInitialized = false;
        string first = Market.Equipment.Single().Equipment.InstanceId;
        _market.AdvanceTime(10);
        Assert.AreEqual(first, Market.Equipment.Single().Equipment.InstanceId);
        Assert.AreEqual(57, Stock.Quantity);
        _market.AdvanceTime(20);
        Assert.AreNotEqual(first, Market.Equipment.Single().Equipment.InstanceId);
        Assert.AreEqual(71, Stock.Quantity);
    }
}
