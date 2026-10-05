using System;
using UnityEngine;

[Serializable]
public sealed class CombatBeamRuntimeState2A
{
    public string BeamId;
    public string SystemId;

    public CombatShooterType ShooterType;
    public string ShooterNpcId;

    public CombatTargetType TargetType;
    public string TargetNpcId;

    public string WeaponConfigId;

    public Vector3 StartPosition;
    public Vector3 TargetPosition;

    public int TotalDamage;
    public int ShotCount;
    public int AppliedShotCount;

    public int CreatedTick;
    public float ElapsedSeconds;
    public float DurationSeconds;

    public bool IsResolved;
}