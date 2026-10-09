using UnityEngine;

/// <summary>
/// Попадание или завершение полёта снаряда NPC; обновление визуальных эффектов.
/// </summary>
public readonly struct GalaxyNpcProjectileImpactEvent
{
    /// <summary>Идентификатор снаряда.</summary>
    public readonly string ProjectileId;
    /// <summary>Идентификатор звёздной системы.</summary>
    public readonly string SystemId;
    /// <summary>Идентификатор NPC, выполнившего выстрел.</summary>
    public readonly string ShooterNpcId;

    /// <summary>Тип цели, к которой относится событие.</summary>
    public readonly CombatTargetType TargetType;
    /// <summary>Идентификатор NPC, выбранного целью.</summary>
    public readonly string TargetNpcId;

    /// <summary>Нанесённый или полученный урон.</summary>
    public readonly int Damage;
    /// <summary>Позиция попадания или завершения полёта снаряда.</summary>
    public readonly Vector3 HitPosition;
    /// <summary>Признак попадания снаряда в цель.</summary>
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