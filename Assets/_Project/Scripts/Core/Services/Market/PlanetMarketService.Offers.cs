using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class PlanetMarketService
{
    private static double NextOfferExpiry(MarketRuntimeData market, MarketProfileConfig profile) =>
        market.ElapsedSeconds + Math.Max(0, Math.Max(1, profile.OfferRefreshIntervalSeconds) -
            market.OfferRefreshRemainder - market.EquipmentPendingSeconds);

    private static void RefreshOffers(MarketRuntimeData market, MarketProfileConfig profile,
        MarketGalaxyLevelSettings settings, double refreshAt) =>
        RefreshOffers(market, profile.Id, settings, refreshAt, Math.Max(1, profile.OfferRefreshIntervalSeconds));

    private static void RefreshOffers(MarketRuntimeData market, string profileId,
        MarketGalaxyLevelSettings settings, double refreshAt, int period)
    {
        // Only stock on sale is replaced. Purchased items and personal warehouses remain independent.
        market.Equipment.Clear();
        market.Hulls.Clear();
        market.OffersInitialized = true;
        market.Revision++;
        if (settings == null) return;

        var random = new System.Random(ShipEquipmentConfig.StableSeed(
            profileId + "|" + market.SystemId + "|" + market.PlanetId + "|" + market.OfferRefreshSequence));
        double expiry = refreshAt + period;
        var hulls = (settings.Hulls ?? new List<MarketHullRule>())
            .Where(rule => ValidHullRule(rule, settings.GalaxyLevel) && rule.Weight > 0).ToList();
        var weapons = (settings.Equipment ?? new List<MarketEquipmentRule>())
            .Where(rule => ValidEquipmentRule(rule, settings.GalaxyLevel) && rule.Allied &&
                rule.Component == null && rule.Weapon != null && rule.Weight > 0).ToList();
        var components = (settings.Equipment ?? new List<MarketEquipmentRule>())
            .Where(rule => ValidEquipmentRule(rule, settings.GalaxyLevel) && rule.Allied &&
                rule.Component != null && rule.Weight > 0).ToList();

        for (int i = 0; i < Math.Max(0, settings.HullOfferCount) && hulls.Count > 0; i++)
        {
            var rule = PickWeighted(hulls, random, candidate => candidate.Weight);
            string id = OfferId(market, "hull", i);
            market.Hulls.Add(new MarketHullListing
            {
                Hull = rule.Hull.CreateRuntimeState(id), NewPrice = rule.NewPrice, ExpiresAtSeconds = expiry
            });
        }
        AddEquipmentOffers(market, weapons, Math.Max(0, settings.WeaponOfferCount), "weapon", random, expiry);
        AddEquipmentOffers(market, components, Math.Max(0, settings.EquipmentOfferCount), "equipment", random, expiry);
    }

    // Sampling with replacement: every listing is a separate item with its own rolled statistics.
    private static void AddEquipmentOffers(MarketRuntimeData market, List<MarketEquipmentRule> pool,
        int count, string group, System.Random random, double expiry)
    {
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            var rule = PickWeighted(pool, random, candidate => candidate.Weight);
            string id = OfferId(market, group, i);
            SystemNpcWeaponRuntimeState item;
            if (rule.Component != null) item = SystemNpcWeaponRuntimeState.NewComponent(rule.Component, id);
            else
            {
                var stats = rule.Weapon.RollRuntimeStats(ShipEquipmentConfig.StableSeed(id));
                item = new SystemNpcWeaponRuntimeState
                {
                    InstanceId = id, WeaponConfigId = rule.Weapon.Id, RolledStats = stats, SlotIndex = -1,
                    CurrentDurability = rule.Weapon.MaximumDurability, MaximumDurability = rule.Weapon.MaximumDurability
                };
            }
            market.Equipment.Add(new MarketEquipmentListing
            {
                Equipment = item, NewPrice = rule.NewPrice, Allied = true, ExpiresAtSeconds = expiry
            });
        }
    }

    private static string OfferId(MarketRuntimeData market, string group, int index) =>
        "market:" + market.SystemId + ":" + market.PlanetId + ":batch:" + market.OfferRefreshSequence + ":" + group + ":" + index;

    private static T PickWeighted<T>(List<T> items, System.Random random, Func<T, int> weight)
    {
        long total = 0;
        foreach (var item in items) total += Math.Max(0, weight(item));
        long roll = (long)(random.NextDouble() * total);
        foreach (var item in items)
        {
            roll -= Math.Max(0, weight(item));
            if (roll < 0) return item;
        }
        return items[items.Count - 1];
    }
}
