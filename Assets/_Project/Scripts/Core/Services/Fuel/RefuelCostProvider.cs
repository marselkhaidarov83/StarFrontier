public class RefuelCostProvider : IRefuelCostProvider
    {
        private readonly int _costPerFuelUnit = 25;

        public RefuelCostProvider(int costPerFuelUnit)
        {
            _costPerFuelUnit = costPerFuelUnit;
        }

        public int GetCostPerFuelUnit()
        {
            return _costPerFuelUnit;
        }
    }