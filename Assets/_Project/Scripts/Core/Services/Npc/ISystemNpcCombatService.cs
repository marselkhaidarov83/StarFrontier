public interface ISystemNpcCombatService
{
    void Tick(StarSystemConfig starSystem, int quantTick);
    void TickProjectiles(float deltaTime);

    void ForceAttackOnce(string shooterNpcId, int quantTick);

    bool TryCreatePlayerProjectile(
        string targetNpcId,
        string weaponConfigId,
        int quantTick);

    bool TryGetProjectile(
        string projectileId,
        out GalaxyNpcProjectileRuntimeState projectile);
}