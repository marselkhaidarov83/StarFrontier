using System;

    [Serializable]
    public class RepairResult
    {
        public RepairResultType resultType;
        public int hullRepaired;
        public int totalPrice;
        public int currentHull;
        public int hullCapacity;

        public bool IsSuccess => resultType == RepairResultType.Success;

        public static RepairResult Create(
            RepairResultType resultType,
            int hullRepaired,
            int totalPrice,
            int currentHull,
            int hullCapacity)
        {
            return new RepairResult
            {
                resultType = resultType,
                hullRepaired = hullRepaired,
                totalPrice = totalPrice,
                currentHull = currentHull,
                hullCapacity = hullCapacity
            };
        }
    }