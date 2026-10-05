using System;

public sealed class DamageService2A : CustomService, IDamageService2A
{
    public CombatDamageResult2A ApplyDamage(
        int currentShield,
        int currentHull,
        int damage)
    {
        int safeShield = Math.Max(0, currentShield);
        int safeHull = Math.Max(0, currentHull);

        if (damage <= 0 || safeHull <= 0)
        {
            return new CombatDamageResult2A(
                0,
                0,
                0,
                safeShield,
                safeHull,
                safeHull <= 0);
        }

        int remainingDamage = damage;
        int shieldDamage = Math.Min(safeShield, remainingDamage);
        safeShield -= shieldDamage;
        remainingDamage -= shieldDamage;

        int hullDamage = Math.Min(safeHull, Math.Max(0, remainingDamage));
        safeHull -= hullDamage;

        return new CombatDamageResult2A(
            shieldDamage + hullDamage,
            shieldDamage,
            hullDamage,
            safeShield,
            safeHull,
            safeHull <= 0);
    }
}