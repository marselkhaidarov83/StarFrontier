using UnityEngine;

public readonly struct GalaxyNpcProjectileImpactEvent
{
    public readonly string ProjectileId;
    public readonly string SystemId;
    public readonly string ShooterNpcId;

    public readonly CombatTargetType TargetType;
    public readonly string TargetNpcId;

    public readonly int Damage;
    public readonly Vector3 HitPosition;
    public readonly bool DidHit;

    public GalaxyNpcProjectileImpactEvent(
        string projectileId,
        string systemId,
        string shooterNpcId,
        CombatTargetType targetType,
        string targetNpcId,
        int damage,
        Vector3 hitPosition,
        bool didHit)
    {
        ProjectileId = projectileId;
        SystemId = systemId;
        ShooterNpcId = shooterNpcId;
        TargetType = targetType;
        TargetNpcId = targetNpcId;
        Damage = damage;
        HitPosition = hitPosition;
        DidHit = didHit;
    }
}