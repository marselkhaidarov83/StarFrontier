using System;

// Pure calculations shared by online progress, offline progress and quotes.
public static class MarketEconomyRules
{
    public static int Price(MarketItemEntry entry, int stock)
    {
        int basic = entry.BasePrice > 0 ? entry.BasePrice : entry.ItemConfig?.BasePrice ?? 0;
        int maximum = Math.Max(basic, entry.MaximumPrice > 0 ? entry.MaximumPrice : basic);
        if (basic <= 0 || entry.MaximumStock <= 0) return 0;
        double scarcity = 1 - Math.Min(1, Math.Max(0, stock) / (double)entry.MaximumStock);
        return (int)Math.Min(int.MaxValue, Math.Max(1, Math.Round(basic + (maximum - (double)basic) * scarcity, MidpointRounding.AwayFromZero)));
    }
    public static long TotalPrice(MarketItemEntry entry, int stock, int quantity, bool buy)
    {
        if (quantity <= 0 || entry.MaximumStock <= 0 || (buy && stock < quantity)) return 0;
        long basic = entry.BasePrice > 0 ? entry.BasePrice : entry.ItemConfig?.BasePrice ?? 0;
        long maximum = Math.Max(basic, entry.MaximumPrice > 0 ? entry.MaximumPrice : basic);
        if (basic <= 0) return 0;
        // Buy uses stocks S, S-1, ...; sell uses S+1, S+2, ... . Reversing an operation cannot mint credits.
        long low = buy ? (long)stock - quantity + 1 : (long)stock + 1;
        long high = buy ? stock : (long)stock + quantity;
        long cap = entry.MaximumStock;
        long inRangeLow = Math.Max(0, low), inRangeHigh = Math.Min(cap, high);
        long count = Math.Max(0, inRangeHigh - inRangeLow + 1);
        long total = basic * quantity;
        if (count > 0)
            total += FloorSum(count, 2 * cap, 2 * (maximum - basic),
                2 * (maximum - basic) * (cap - inRangeHigh) + cap);
        return total;
    }
    // Sum floor((a*i+b)/m), i=0..n-1. Inputs are nonnegative and bounded by serialized int prices/stocks.
    private static long FloorSum(long n, long m, long a, long b)
    {
        long answer = 0;
        while (true)
        {
            if (a >= m) { answer += n * (n - 1) / 2 * (a / m); a %= m; }
            if (b >= m) { answer += n * (b / m); b %= m; }
            long y = a * n + b;
            if (y < m) return answer;
            n = y / m; b = y % m;
            long swap = m; m = a; a = swap;
        }
    }
    public static void AdvanceStock(MarketRuntimeItemState state, MarketItemEntry entry,
        double intervals)
    {
        if (intervals <= 0 || double.IsNaN(intervals) || double.IsInfinity(intervals)) return;
        double change = entry.StockChange;
        if (change == 0) return;
        // Production must never destroy a player's over-cap sale.
        if (change > 0 && state.Quantity >= entry.MaximumStock) { state.FractionalStock = 0; return; }
        double next = state.Quantity + state.FractionalStock + change * intervals;
        double limit = change > 0 ? Math.Max(0, entry.MaximumStock) : int.MaxValue;
        next = Math.Max(0, Math.Min(limit, next));
        state.Quantity = (int)Math.Floor(next + 1e-9);
        state.FractionalStock = Math.Max(0, next - state.Quantity);
    }
    public static int EquipmentSellPrice(int newPrice, int currentDurability, int maximumDurability)
    {
        if (newPrice <= 0) return 0;
        double fraction = maximumDurability > 0 ? Math.Max(0, Math.Min(maximumDurability, currentDurability)) / (double)maximumDurability : 0;
        return (int)Math.Max(1, Math.Min(newPrice, Math.Round(newPrice * fraction, MidpointRounding.AwayFromZero)));
    }
}
