    public interface IRefuelService
    {
        RefuelResult RefuelToFull();
        int GetFuelUnitPrice();
        bool CanRefuel(int fuelCount);
        RefuelResult Refuel(int fuelCount);
    }