public interface IDamageService2A
{
    CombatDamageResult2A ApplyDamage(
        int currentShield,
        int currentHull,
        int damage);
}