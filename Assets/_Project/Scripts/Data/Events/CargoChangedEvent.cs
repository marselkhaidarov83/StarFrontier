/// <summary>
/// Изменение содержимого или количества груза игрока; используется для обновления инвентаря и торговли.
/// </summary>
public sealed class CargoChangedEvent
{
    /// <summary>Занятый объём грузового отсека.</summary>
    public int UsedCargo { get; }

    public CargoChangedEvent(int usedCargo)
    {
        UsedCargo = usedCargo;
    }
}