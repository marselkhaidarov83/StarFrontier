using System.Collections.Generic;

public interface ICurrentMarketItemsProvider
{
    IReadOnlyList<MarketItemEntry> GetAvailableItems();
}