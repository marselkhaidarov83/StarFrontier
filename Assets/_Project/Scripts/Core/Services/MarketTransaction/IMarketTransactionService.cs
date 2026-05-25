    public interface IMarketTransactionService
    {
        BuyItemResult BuyItem(string itemId, int quantity);
        SellItemResult SellItem(string itemId, int quantity);
    }