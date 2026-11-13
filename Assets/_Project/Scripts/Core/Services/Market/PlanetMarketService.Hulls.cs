using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class PlanetMarketService
{
    private static bool IsStationMarket(MarketRuntimeData market) => market?.PlanetId?.StartsWith("station:", StringComparison.Ordinal) == true;
    private MarketHullRule HullRule(MarketProfileConfig profile, string id) =>
        profile?.GetLevel(GalaxyLevel)?.Hulls?.Find(x => x?.Hull != null && x.Hull.Id == id)
        ?? Balance?.HullCatalog?.Find(x => x?.Hull != null && x.Hull.Id == id);
    public HullEquipmentConfig GetHullConfig(string id)
    {
        if (!Current(out _, out var profile)) return null;
        return HullRule(profile, id)?.Hull;
    }
    public int HullPrice(HullEquipmentRuntimeState hull, bool buy)
    {
        if (hull == null || !Current(out var market, out var profile)) return 0;
        int price = market.Hulls.Find(x => x?.Hull?.InstanceId == hull.InstanceId)?.NewPrice
            ?? HullRule(profile, hull.ConfigId)?.NewPrice ?? 0;
        return MarketEconomyRules.EquipmentSellPrice(price, hull.CurrentDurability, hull.MaximumDurability, MinimumBuyout);
    }
    private bool CanSellStoredHull(MarketRuntimeData market, string id)
    {
        var ships = State.Player.PlayerShipState;
        if (!IsStationMarket(market) || ships?.OwnedShips == null ||
            ships.OwnedShips.Any(x => x?.InstalledHull?.InstanceId == id)) return false;
        var owned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ship in ships.OwnedShips)
            if (ship != null && !string.IsNullOrEmpty(ship.ShipId))
                owned.Add(HullEquipmentRuntimeState.IsEmptyPlaceholder(ship.InstalledHull)
                    ? "installed:" + ship.ShipId : ship.InstalledHull.InstanceId);
        foreach (var location in State.Markets)
            foreach (var hull in location?.PersonalHulls ?? new List<HullEquipmentRuntimeState>())
                if (hull != null && !string.IsNullOrEmpty(hull.InstanceId)) owned.Add(hull.InstanceId);
        return owned.Contains(id) && owned.Count > 1;
    }
    public bool CanTradeHull(string id, bool buy, bool warehouse = false)
    {
        if (_committing || !warehouse || Ship == null || !Current(out var market, out var profile)) return false;
        var listing = market.Hulls.Find(x => x?.Hull?.InstanceId == id && x.ExpiresAtSeconds > market.ElapsedSeconds);
        var hull = buy ? listing?.Hull : market.PersonalHulls.Find(x => x?.InstanceId == id);
        if (hull == null || HullRule(profile, hull.ConfigId)?.Hull == null) return false;
        int price = HullPrice(hull, buy);
        return price > 0 && (buy ? State.Player.Credits >= price && !market.PersonalHulls.Any(x => x?.InstanceId == id)
            : CanSellStoredHull(market, id) && (long)State.Player.Credits + price <= int.MaxValue && !market.Hulls.Any(x => x?.Hull?.InstanceId == id));
    }
    public bool TradeHull(string id, bool buy, string operationId, long expectedRevision, out string reason, bool warehouse = false)
    {
        reason = "HullUnavailable";
        if (_committing || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(operationId) ||
            !Current(out var market, out var profile)) return false;
        string intent = (buy ? "hull_buy:" : "hull_sell:") + id + (warehouse ? ":storage" : "");
        var previousMarket = State.Markets.Find(x => x?.Receipts?.Any(y => y?.OperationId == operationId) == true);
        if (previousMarket != null)
        {
            var receipt = previousMarket.Receipts.Find(x => x?.OperationId == operationId);
            bool same = ReferenceEquals(previousMarket, market) && receipt.Intent == intent;
            reason = same ? "" : "OperationConflict"; return same;
        }
        if (expectedRevision >= 0 && market.Revision != expectedRevision) { reason = "MarketChanged"; return false; }
        if (!CanTradeHull(id, buy, warehouse)) return false;
        var listing = market.Hulls.Find(x => x?.Hull?.InstanceId == id);
        var hull = buy ? listing.Hull : market.PersonalHulls.Find(x => x?.InstanceId == id);
        var rule = HullRule(profile, hull.ConfigId);
        int price = HullPrice(hull, buy);
        _committing = true;
        try
        {
            if (buy) { market.Hulls.Remove(listing); market.PersonalHulls.Add(hull); }
            else
            {
                market.PersonalHulls.Remove(hull);
                market.Hulls.Add(new MarketHullListing { Hull = hull, NewPrice = rule.NewPrice,
                    ExpiresAtSeconds = market.ElapsedSeconds + Math.Max(.01, rule.LifetimeSeconds) });
            }
            State.Player.Credits += buy ? -price : price;
            market.Revision++;
            market.Receipts.Add(new MarketTransactionReceipt { OperationId = operationId, Intent = intent, Quantity = 1, TotalPrice = price });
            _equipmentDeadlines.Remove(market);
            Log("HULL", market, intent + " | Total=" + price);
            reason = ""; Notify("market_hull"); return true;
        }
        finally { _committing = false; }
    }
    private static bool ValidHullRule(MarketHullRule rule) => rule?.Hull != null && rule.NewPrice > 0 && rule.MaximumListings > 0 &&
        rule.SpawnIntervalSeconds > 0 && !float.IsNaN(rule.SpawnIntervalSeconds) && !float.IsInfinity(rule.SpawnIntervalSeconds) &&
        rule.LifetimeSeconds > 0 && !float.IsNaN(rule.LifetimeSeconds) && !float.IsInfinity(rule.LifetimeSeconds);
    private bool SeedHullListings(MarketRuntimeData market, MarketGalaxyLevelSettings settings)
    {
        if (settings?.Hulls == null) return false;
        long before = market.Revision;
        foreach (var rule in settings.Hulls)
        {
            if (!ValidHullRule(rule) || !rule.StartInStock) continue;
            string timerId = "hull:" + rule.Hull.Id;
            if (market.EquipmentSpawns.Exists(existing => existing?.WeaponId == timerId)) continue;

            var timer = new MarketEquipmentSpawnState { WeaponId = timerId };
            market.EquipmentSpawns.Add(timer);
            if (market.Hulls.Exists(listing => listing?.Hull?.ConfigId == rule.Hull.Id &&
                    listing.ExpiresAtSeconds > market.ElapsedSeconds)) continue;

            timer.SpawnSequence++;
            AddHullListing(market, rule, timer.SpawnSequence, market.ElapsedSeconds);
        }

        if (before == market.Revision) return false;
        Log("HULL_INITIAL_STOCK", market, "Count=" + market.Hulls.Count);
        return true;
    }

    private static void AddHullListing(MarketRuntimeData market, MarketHullRule rule, long sequence, double time)
    {
        int durability = Math.Max(1, rule.Hull.MaximumHull);
        market.Hulls.Add(new MarketHullListing
        {
            Hull = new HullEquipmentRuntimeState
            {
                InstanceId = "hull:" + market.SystemId + ":" + market.PlanetId + ":" + rule.Hull.Id + ":" + sequence,
                ConfigId = rule.Hull.Id,
                CurrentDurability = durability,
                MaximumDurability = durability
            },
            NewPrice = rule.NewPrice,
            ExpiresAtSeconds = time + rule.LifetimeSeconds
        });
        market.Revision++;
    }

    private static double NextHullDeadline(MarketRuntimeData market, MarketGalaxyLevelSettings settings)
    {
        double next = double.PositiveInfinity;
        foreach (var rule in settings.Hulls ?? new List<MarketHullRule>())
        {
            if (!ValidHullRule(rule)) continue;
            var timer = market.EquipmentSpawns.Find(x => x?.WeaponId == "hull:" + rule.Hull.Id);
            next = Math.Min(next, market.ElapsedSeconds - market.EquipmentPendingSeconds + rule.SpawnIntervalSeconds - (timer?.Remainder ?? 0));
        }
        foreach (var listing in market.Hulls)
            if (listing?.Hull != null) next = Math.Min(next, listing.ExpiresAtSeconds);
        return next;
    }
    private void AdvanceHulls(MarketRuntimeData market, MarketGalaxyLevelSettings settings, double seconds)
    {
        long before = market.Revision;
        double end = market.ElapsedSeconds + seconds;
        foreach (var rule in settings.Hulls ?? new List<MarketHullRule>())
        {
            if (!ValidHullRule(rule)) continue;
            string timerId = "hull:" + rule.Hull.Id;
            var timer = market.EquipmentSpawns.Find(x => x?.WeaponId == timerId);
            if (timer == null) { timer = new MarketEquipmentSpawnState { WeaponId = timerId }; market.EquipmentSpawns.Add(timer); }
            double next = market.ElapsedSeconds + rule.SpawnIntervalSeconds - timer.Remainder;
            for (double time = next; time <= end + 1e-9; time += rule.SpawnIntervalSeconds)
            {
                timer.SpawnSequence++;
                if (market.Hulls.RemoveAll(x => x?.Hull == null || (x.Hull.ConfigId == rule.Hull.Id && x.ExpiresAtSeconds <= time)) > 0) market.Revision++;
                if (market.Hulls.Count(x => x.Hull.ConfigId == rule.Hull.Id) >= rule.MaximumListings) continue;
                AddHullListing(market, rule, timer.SpawnSequence, time);
            }
            timer.Remainder = (timer.Remainder + seconds) % rule.SpawnIntervalSeconds;
        }
        if (market.Hulls.RemoveAll(x => x?.Hull == null || x.ExpiresAtSeconds <= end) > 0) market.Revision++;
        if (market.Revision != before) Log("HULL_LISTINGS", market, "Count=" + market.Hulls.Count);
    }
}
