using UnityEngine;

/// <summary>
/// Создание боевого снаряда NPC; событие для отображения его полёта.
/// </summary>
public readonly struct GalaxyNpcProjectileCreatedEvent
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

    /// <summary>Идентификатор конфига оружия.</summary>
    public readonly string WeaponConfigId;
    /// <summary>Начальная позиция воздействия или снаряда.</summary>
    public readonly Vector3 StartPosition;
    /// <summary>Позиция назначенной цели.</summary>
    public readonly Vector3 TargetPosition;
    /// <summary>Скорость движения снаряда.</summary>
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