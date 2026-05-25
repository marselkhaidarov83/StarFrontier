using System;

    [Serializable]
    public class BuyItemResult
    {
        public BuyItemResultType resultType;
        public string itemId;
        public int quantity;
        public int unitPrice;
        public int totalPrice;

        public bool IsSuccess => resultType == BuyItemResultType.Success;

        public static BuyItemResult Create(
            BuyItemResultType resultType,
            string itemId,
            int quantity,
            int unitPrice,
            int totalPrice)
        {
            return new BuyItemResult
            {
                resultType = resultType,
                itemId = itemId,
                quantity = quantity,
                unitPrice = unitPrice,
                totalPrice = totalPrice
            };
        }
    }