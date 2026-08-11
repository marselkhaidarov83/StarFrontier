public readonly struct CombatDamageResult2A
{
    public readonly int AppliedDamage;
    public readonly int ShieldDamage;
    public readonly int HullDamage;
    public readonly int CurrentShield;
    public readonly int CurrentHull;
    public readonly bool IsDestroyed;

    public CombatDamageResult2A(
        int appliedDamage,
        int shieldDamage,
        int hullDamage,
        int currentShield,
        int currentHull,
        bool isDestroyed)
    {
        AppliedDamage = appliedDamage;
        ShieldDamage = shieldDamage;
        HullDamage = hullDamage;
        CurrentShield = currentShield;
        CurrentHull = currentHull;
        IsDestroyed = isDestroyed;
    }
}