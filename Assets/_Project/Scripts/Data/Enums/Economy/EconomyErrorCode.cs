public enum EconomyErrorCode
    {
        None = 0,
        InvalidItemId = 1,
        InvalidQuantity = 2,
        PriceNotFound = 3,
        NotEnoughCredits = 4,
        NotEnoughCargoSpace = 5,
        ItemNotOwned = 6,
        NotEnoughItemsToSell = 7,
        NotEnoughMarketStock = 8, MarketChanged = 9, TransactionBusy = 10, OperationConflict = 11, ValueOverflow = 12
    }