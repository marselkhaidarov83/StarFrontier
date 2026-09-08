using UnityEngine;

public readonly struct CombatBeamStartedEvent2A
{
    public readonly string BeamId;
    public readonly string SystemId;
    public readonly CombatShooterType ShooterType;
    public readonly string ShooterNpcId;
    public readonly CombatTargetType TargetType;
    public readonly string TargetNpcId;
    public readonly string WeaponConfigId;
    public readonly Vector3 StartPosition;
    public readonly Vector3 TargetPosition;
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