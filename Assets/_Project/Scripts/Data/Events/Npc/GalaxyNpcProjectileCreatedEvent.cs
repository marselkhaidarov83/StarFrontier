using UnityEngine;

public readonly struct GalaxyNpcProjectileCreatedEvent
{
    public readonly string ProjectileId;
    public readonly string SystemId;
    public readonly string ShooterNpcId;

    public readonly CombatTargetType TargetType;
    public readonly string TargetNpcId;

    public readonly string WeaponConfigId;
    public readonly Vector3 StartPosition;
    public readonly Vector3 TargetPosition;
    public readonly float ProjectileSpeed;

    public GalaxyNpcProjectileCreatedEvent(
        string projectileId,
        string systemId,
        string shooterNpcId,
        CombatTargetType targetType,
        string targetNpcId,
        string weaponConfigId,
        Vector3 startPosition,
        Vector3 targetPosition,
        float projectileSpeed)
    {
        ProjectileId = projectileId;
        SystemId = systemId;
        ShooterNpcId = shooterNpcId;
        TargetType = targetType;
        TargetNpcId = targetNpcId;
        WeaponConfigId = weaponConfigId;
        StartPosition = startPosition;
        TargetPosition = targetPosition;
        ProjectileSpeed = projectileSpeed;
    }
}