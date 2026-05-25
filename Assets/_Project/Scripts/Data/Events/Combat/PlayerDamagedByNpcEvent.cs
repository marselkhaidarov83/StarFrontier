public readonly struct PlayerDamagedByNpcEvent
{
    public readonly int Damage;
    public readonly int CurrentShield;
    public readonly int CurrentHull;

    public PlayerDamagedByNpcEvent(
        int damage,
        int currentShield,
        int currentHull)
    {
        Damage = damage;
        CurrentShield = currentShield;
        CurrentHull = currentHull;
    }
}