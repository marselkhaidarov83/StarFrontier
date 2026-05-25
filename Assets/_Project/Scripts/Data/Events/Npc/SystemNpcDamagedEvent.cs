public readonly struct SystemNpcDamagedEvent
{
    public readonly string RuntimeNpcId;
    public readonly int Damage;
    public readonly int CurrentHull;
    public readonly int CurrentShield;

    public SystemNpcDamagedEvent(
        string runtimeNpcId,
        int damage,
        int currentHull,
        int currentShield)
    {
        RuntimeNpcId = runtimeNpcId;
        Damage = damage;
        CurrentHull = currentHull;
        CurrentShield = currentShield;
    }
}