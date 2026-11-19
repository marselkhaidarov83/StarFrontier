using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class PlanetMarketService
{
    private static bool IsStationMarket(MarketRuntimeData market) => market?.PlanetId?.StartsWith("station:", StringComparison.Ordinal) == true;
    private MarketHullRule HullRule(MarketProfileConfig profile, string id) =>
        profile?.GetLevel(MarketLevel)?.Hulls?.Find(x => x?.Hull != null && x.Hull.Id == id)
        ?? Balance?.HullCatalog?.Find(x => x?.Hull != null && x.Hull.Id == id);
    public HullConfig GetHullConfig(string id)
    {
        if (!Current(out _, out var profile)) return null;
        return Balance?.HullCatalog?.Find(rule => rule?.Hull != null && rule.Hull.Id == id)?.Hull
            ?? HullRule(profile, id)?.Hull;
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
        if (buy && GetHullConfig(hull.ConfigId).Level > MarketLevel) return false;
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
        if (buy && GetHullConfig(hull.ConfigId).Level > MarketLevel) return false;
        int price = HullPrice(hull, buy);
        _committing = true;
        try
        {
            if (buy) { market.Hulls.Remove(listing); market.PersonalHulls.Add(hull); }
            else
            {
                market.PersonalHulls.Remove(hull);
                if (ValidHullRule(rule, MarketLevel)) market.Hulls.Add(new MarketHullListing { Hull = hull, NewPrice = rule.NewPrice,
                    ExpiresAtSeconds = NextOfferExpiry(market, profile) });
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
    private static bool ValidHullRule(MarketHullRule rule, int marketLevel) =>
        rule?.Hull != null && rule.Hull.Level <= marketLevel && rule.NewPrice > 0;
}
