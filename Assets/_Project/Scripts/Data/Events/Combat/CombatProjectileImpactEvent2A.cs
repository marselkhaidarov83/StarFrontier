using UnityEngine;

/// <summary>
/// Завершение полёта снаряда: попадание или промах, координата воздействия и урон.
/// </summary>
public struct CombatProjectileImpactEvent2A
{
    /// <summary>Идентификатор снаряда.</summary>
    public string ProjectileId { get; }
    /// <summary>Идентификатор звёздной системы.</summary>
    public string SystemId { get; }
    /// <summary>Идентификатор цели события.</summary>
    public string TargetId { get; }
    /// <summary>Нанесённый или полученный урон.</summary>
    public int Damage { get; }
    /// <summary>Позиция попадания или завершения полёта снаряда.</summary>
    public Vector3 HitPosition { get; }
    /// <summary>Признак попадания снаряда в цель.</summary>
    public bool DidHit { get; }

    public CombatProjectileImpactEvent2A(
        string projectileId,
        string systemId,
        string targetId,
        int damage,
        Vector3 hitPosition,
        bool didHit)
    {
        ProjectileId = projectileId;
        SystemId = systemId;
        TargetId = targetId;
        Damage = damage;
        HitPosition = hitPosition;
        DidHit = didHit;
    }
}
