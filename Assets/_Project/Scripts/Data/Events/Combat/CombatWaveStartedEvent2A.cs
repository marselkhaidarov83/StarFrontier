using UnityEngine;

public readonly struct CombatWaveStartedEvent2A
{
    public readonly string WaveId;
    public readonly string SystemId;
    public readonly CombatShooterType ShooterType;
    public readonly string ShooterNpcId;
    public readonly CombatTargetType TargetType;
    public readonly string PrimaryTargetNpcId;
    public readonly string WeaponConfigId;
    public readonly Vector3 CenterPosition;
    public readonly float FinalRadius;
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