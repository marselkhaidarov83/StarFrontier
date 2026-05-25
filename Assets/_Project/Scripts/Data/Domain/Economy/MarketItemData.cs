using System;

    [Serializable]
    public class MarketItemData
    {
        public string itemId;
        public string displayName;
        public int basePrice;
        public int buyPrice;
        public int sellPrice;
        public ItemConfig itemConfig;

        public MarketItemData()
        {
        }

        public MarketItemData(
            string itemId,
            string displayName,
            int basePrice,
            int buyPrice,
            int sellPrice,
            ItemConfig itemConfig)
        {
            this.itemId = itemId;
            this.displayName = displayName;
            this.basePrice = basePrice;
            this.buyPrice = buyPrice;
            this.sellPrice = sellPrice;
            this.itemConfig = itemConfig;
        }
    }