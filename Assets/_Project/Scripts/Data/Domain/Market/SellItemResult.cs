using System;

    [Serializable]
    public class SellItemResult
    {
        public SellItemResultType resultType;
        public string itemId;
        public int quantity;
        public int unitPrice;
        public int totalPrice;

        public bool IsSuccess => resultType == SellItemResultType.Success;

        public static SellItemResult Create(
            SellItemResultType resultType,
            string itemId,
            int quantity,
            int unitPrice,
            int totalPrice)
        {
            return new SellItemResult
            {
                resultType = resultType,
                itemId = itemId,
                quantity = quantity,
                unitPrice = unitPrice,
                totalPrice = totalPrice
            };
        }
    }