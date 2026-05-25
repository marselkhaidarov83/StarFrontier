public sealed class CargoChangedEvent
{
    public int UsedCargo { get; }

    public CargoChangedEvent(int usedCargo)
    {
        UsedCargo = usedCargo;
    }
}