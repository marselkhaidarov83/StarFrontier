using System;

    [Serializable]
    public sealed class TravelResult
    {
        public bool Success { get; }
        public TravelFailReason FailReason { get; }
        public string FromSystemId { get; }
        public string ToSystemId { get; }
        public int FuelSpent { get; }

        public TravelResult(
            bool success,
            TravelFailReason failReason,
            string fromSystemId,
            string toSystemId,
            int fuelSpent)
        {
            Success = success;
            FailReason = failReason;
            FromSystemId = fromSystemId;
            ToSystemId = toSystemId;
            FuelSpent = fuelSpent;
        }

        public static TravelResult Failed(
            TravelFailReason failReason,
            string fromSystemId,
            string toSystemId)
        {
            return new TravelResult(
                success: false,
                failReason: failReason,
                fromSystemId: fromSystemId,
                toSystemId: toSystemId,
                fuelSpent: 0);
        }

        public static TravelResult Completed(
            string fromSystemId,
            string toSystemId,
            int fuelSpent)
        {
            return new TravelResult(
                success: true,
                failReason: TravelFailReason.None,
                fromSystemId: fromSystemId,
                toSystemId: toSystemId,
                fuelSpent: fuelSpent);
        }

        public override string ToString()
        {
            return $"TravelResult(Success={Success}, FailReason={FailReason}, From={FromSystemId}, To={ToSystemId}, FuelSpent={FuelSpent})";
        }
    }