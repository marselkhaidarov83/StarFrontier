using UnityEngine;

/// <summary>
/// Начало лучевого выстрела; передача параметров для отображения луча.
/// </summary>
public readonly struct CombatBeamStartedEvent2A
{
    /// <summary>Идентификатор активного лучевого эффекта.</summary>
    public readonly string BeamId;
    /// <summary>Идентификатор звёздной системы.</summary>
    public readonly string SystemId;
    /// <summary>Тип стрелявшего объекта.</summary>
    public readonly CombatShooterType ShooterType;
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
    /// <summary>Продолжительность эффекта в секундах.</summary>
    public readonly float DurationSeconds;

    public CombatBeamStartedEvent2A(
        string beamId,
        string systemId,
        CombatShooterType shooterType,
        string shooterNpcId,
        CombatTargetType targetType,
        string targetNpcId,
        string weaponConfigId,
        Vector3 startPosition,
        Vector3 targetPosition,
        float durationSeconds)
    {
        BeamId = beamId;
        SystemId = systemId;
        ShooterType = shooterType;
        ShooterNpcId = shooterNpcId ?? string.Empty;
        TargetType = targetType;
        TargetNpcId = targetNpcId ?? string.Empty;
        WeaponConfigId = weaponConfigId ?? string.Empty;
        StartPosition = startPosition;
        TargetPosition = targetPosition;
        DurationSeconds = durationSeconds;
    }
}