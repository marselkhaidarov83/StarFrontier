using UnityEngine;

/// <summary>
/// Создание боевого снаряда; содержит отправителя, цель, оружие и начальную/конечную позиции.
/// </summary>
public struct CombatProjectileCreatedEvent2A
{
    /// <summary>Идентификатор снаряда.</summary>
    public string ProjectileId { get; }
    /// <summary>Идентификатор звёздной системы.</summary>
    public string SystemId { get; }
    /// <summary>Идентификатор стрелявшего объекта.</summary>
    public string ShooterId { get; }
    /// <summary>Идентификатор цели события.</summary>
    public string TargetId { get; }
    /// <summary>Идентификатор конфига оружия.</summary>
    public string WeaponConfigId { get; }
    /// <summary>Начальная позиция воздействия или снаряда.</summary>
    public Vector3 StartPosition { get; }
    /// <summary>Позиция назначенной цели.</summary>
    public Vector3 TargetPosition { get; }

    public CombatProjectileCreatedEvent2A(
        string projectileId,
        string systemId,
        string shooterId,
        string targetId,
        string weaponConfigId,
        Vector3 startPosition,
        Vector3 targetPosition)
    {
        ProjectileId = projectileId;
        SystemId = systemId;
        ShooterId = shooterId;
        TargetId = targetId;
        WeaponConfigId = weaponConfigId;
        StartPosition = startPosition;
        TargetPosition = targetPosition;
    }
}
