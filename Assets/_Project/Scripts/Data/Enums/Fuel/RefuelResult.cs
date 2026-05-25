using System;

    [Serializable]
    public class RefuelResult
    {
        public RefuelResultType resultType;
        public int fuelAdded;
        public int totalPrice;
        public int currentFuel;
        public int fuelCapacity;

        public bool IsSuccess => resultType == RefuelResultType.Success;

        public static RefuelResult Create(
            RefuelResultType resultType,
            int fuelAdded,
            int totalPrice,
            int currentFuel,
            int fuelCapacity)
        {
            return new RefuelResult
            {
                resultType = resultType,
                fuelAdded = fuelAdded,
                totalPrice = totalPrice,
                currentFuel = currentFuel,
                fuelCapacity = fuelCapacity
            };
        }
    }

    // public readonly struct RefuelOperationResult
    // {
    //     public readonly bool Success;
    //     public readonly RefuelErrorCode ErrorCode;
    //     public readonly int FuelAdded;
    //     public readonly int CreditsSpent;

    //     public RefuelOperationResult(
    //         bool success,
    //         RefuelErrorCode errorCode,
    //         int fuelAdded,
    //         int creditsSpent)
    //     {
    //         Success = success;
    //         ErrorCode = errorCode;
    //         FuelAdded = fuelAdded;
    //         CreditsSpent = creditsSpent;
    //     }

    //     public static RefuelOperationResult Failed(RefuelErrorCode errorCode)
    //     {
    //         return new RefuelOperationResult(false, errorCode, 0, 0);
    //     }

    //     public static RefuelOperationResult Completed(int fuelAdded, int creditsSpent)
    //     {
    //         return new RefuelOperationResult(true, RefuelErrorCode.None, fuelAdded, creditsSpent);
    //     }
    // }