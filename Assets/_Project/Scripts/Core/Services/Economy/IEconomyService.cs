using System.Collections.Generic;

    public interface IEconomyService
    {
        int GetBuyPrice(string itemId);
        int GetSellPrice(string itemId);
        List<MarketItemData> GetMarketItemsForCurrentSystem();

        int GetCredits();
        bool CanBuy(string itemId, int quantity);
        bool CanSell(string itemId, int quantity);
        EconomyOperationResult BuyItem(string itemId, int quantity);
        EconomyOperationResult SellItem(string itemId, int quantity);
        void AddCredits(int amount);
        bool SpendCredits(int amount);        
    }