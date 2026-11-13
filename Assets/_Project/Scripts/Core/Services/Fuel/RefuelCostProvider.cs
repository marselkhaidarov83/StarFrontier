using System;

public class RefuelCostProvider : IRefuelCostProvider
{
    private readonly Func<int> _cost;
    public RefuelCostProvider(int costPerFuelUnit) : this(() => costPerFuelUnit) { }
    public RefuelCostProvider(Func<int> cost) { _cost = cost; }
    public int GetCostPerFuelUnit() => _cost();
}
