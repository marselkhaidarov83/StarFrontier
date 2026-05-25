 public readonly struct EconomyOperationResult
    {
        public readonly bool Success;
        public readonly EconomyErrorCode ErrorCode;
        public readonly int ChangedCredits;
        public readonly string ItemId;
        public readonly int Quantity;

        public EconomyOperationResult(
            bool success,
            EconomyErrorCode errorCode,
            int changedCredits,
            string itemId,
            int quantity)
        {
            Success = success;
            ErrorCode = errorCode;
            ChangedCredits = changedCredits;
            ItemId = itemId;
            Quantity = quantity;
        }

        public static EconomyOperationResult Failed(EconomyErrorCode errorCode, string itemId, int quantity)
        {
            return new EconomyOperationResult(false, errorCode, 0, itemId, quantity);
        }

        public static EconomyOperationResult Completed(int changedCredits, string itemId, int quantity)
        {
            return new EconomyOperationResult(true, EconomyErrorCode.None, changedCredits, itemId, quantity);
        }
    }