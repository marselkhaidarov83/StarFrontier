using UnityEngine;

/// <summary>
/// Запуск расширяющейся боевой волны и соответствующего визуального эффекта.
/// </summary>
public readonly struct CombatWaveStartedEvent2A
{
    /// <summary>Идентификатор волнового эффекта.</summary>
    public readonly string WaveId;
    /// <summary>Идентификатор звёздной системы.</summary>
    public readonly string SystemId;
    /// <summary>Тип стрелявшего объекта.</summary>
    public readonly CombatShooterType ShooterType;
    /// <summary>Идентификатор NPC, выполнившего выстрел.</summary>
    public readonly string ShooterNpcId;
    /// <summary>Тип цели, к которой относится событие.</summary>
    public readonly CombatTargetType TargetType;
    /// <summary>Идентификатор основной NPC-цели.</summary>
    public readonly string PrimaryTargetNpcId;
    /// <summary>Идентификатор конфига оружия.</summary>
    public readonly string WeaponConfigId;
    /// <summary>Позиция центра эффекта или области.</summary>
    public readonly Vector3 CenterPosition;
    /// <summary>Конечный радиус эффекта.</summary>
    public readonly float FinalRadius;
    /// <summary>Продолжительность эффекта в секундах.</summary>
    public readonly float DurationSeconds;

    public CombatWaveStartedEvent2A(
        string waveId,
        string systemId,
        CombatShooterType shooterType,
        string shooterNpcId,
        CombatTargetType targetType,
        string primaryTargetNpcId,
        string weaponConfigId,
        Vector3 centerPosition,
        float finalRadius,
        float durationSeconds)
    {
        WaveId = waveId;
        SystemId = systemId;
        ShooterType = shooterType;
        ShooterNpcId = shooterNpcId ?? string.Empty;
        TargetType = targetType;
        PrimaryTargetNpcId = primaryTargetNpcId ?? string.Empty;
        WeaponConfigId = weaponConfigId ?? string.Empty;
        CenterPosition = centerPosition;
        FinalRadius = Mathf.Max(0f, finalRadius);
        DurationSeconds = Mathf.Max(0.01f, durationSeconds);
    }
}