public interface IItemPriceProvider
    {
        bool TryGetBuyPrice(string itemId, out int price);
        bool TryGetSellPrice(string itemId, out int price);
    }