using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Validate and stage all resource edits before replacing any live values. No observer dispatch here.</summary>
public static class StationPaymentRules
{
    public static bool IsAstraliteItem(string itemId) => itemId == "item_astralite_ai_01" ||
        itemId == "item_astralite_ancients_01" || itemId == "item_astralite_infected_01";

    // Only ordinary cargo on the active ship is available; mission deliveries are never payment.
    public static long GetSpendableAstralites(PlayerState player)
    {
        var entries = player?.GetActiveShip()?.Cargo?.entries;
        return entries == null ? 0L : entries
            .Where(e => e != null && IsAstraliteItem(e.ItemId) && string.IsNullOrEmpty(e.MissionRuntimeId))
            .Sum(e => (long)Math.Max(0, e.Quantity));
    }

    public static bool TrySpend(PlayerState player, StationOperationRuntimeState quote, out string reason) =>
        Process(player, quote, true, out reason);
    public static bool CanAfford(PlayerState player, StationOperationRuntimeState quote, out string reason) =>
        Process(player, quote, false, out reason);
    private static bool Process(PlayerState player, StationOperationRuntimeState quote, bool commit, out string reason)
    {
        reason = "InvalidPayment";
        if (player == null || quote == null || quote.PaidCosts == null || quote.PaidCosts.Length == 0) return false;
        long credits = 0, diamonds = 0, astralites = 0;
        foreach (var cost in quote.PaidCosts)
        {
            if (cost == null || cost.Amount <= 0) return false;
            switch (cost.Resource)
            {
                case StationPaymentResource.Credits: credits += cost.Amount; break;
                case StationPaymentResource.Diamonds: diamonds += cost.Amount; break;
                // ItemConfigId in old snapshots is retained as data, but does not restrict the mineral species.
                case StationPaymentResource.Astralites: astralites += cost.Amount; break;
                default: return false;
            }
        }
        reason = "NotEnoughResources";
        if (credits > player.Credits || diamonds > player.Diamonds) return false;
        var cargo = player.GetActiveShip()?.Cargo;
        var staged = new List<RuntimeCargoEntryData>();
        if (astralites > 0)
        {
            if (cargo?.entries == null || GetSpendableAstralites(player) < astralites) return false;
            foreach (var entry in cargo.entries)
                if (entry != null) staged.Add(new RuntimeCargoEntryData(entry.ItemId, entry.Quantity) { MissionRuntimeId = entry.MissionRuntimeId });
            long remaining = astralites;
            foreach (var entry in staged)
            {
                if (!IsAstraliteItem(entry.ItemId) || !string.IsNullOrEmpty(entry.MissionRuntimeId)) continue;
                int take = (int)Math.Min(remaining, Math.Max(0, entry.Quantity));
                entry.Quantity -= take; remaining -= take;
                if (remaining == 0) break;
            }
            if (remaining != 0) return false;
        }
        if (!commit) { reason = string.Empty; return true; }
        // Commit has no calls to external services, no callbacks and no failure paths.
        player.Credits -= (int)credits;
        player.Diamonds -= (int)diamonds;
        if (astralites > 0) cargo.entries = staged.Where(x => x.Quantity > 0).ToList();
        reason = string.Empty;
        return true;
    }
}
