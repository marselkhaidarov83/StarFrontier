public class ShipStats
{
    public int MaxHull;
    public int MaxShield;
    public int MaxEnergy;
    public float Speed;
    public int CargoCapacity;

    public ShipStats Clone()
    {
        return new ShipStats
        {
            MaxHull = MaxHull,
            MaxShield = MaxShield,
            MaxEnergy = MaxEnergy,
            Speed = Speed,
            CargoCapacity = CargoCapacity
        };
    }
}