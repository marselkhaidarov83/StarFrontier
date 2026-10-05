using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CombatWaveRuntimeState2A
{
    public string WaveId;
    public string SystemId;

    public CombatShooterType ShooterType;
    public string ShooterNpcId;

    public CombatTargetType TargetType;
    public string PrimaryTargetNpcId;
    public SystemNpcType PrimaryTargetNpcType;

    public string WeaponConfigId;

    public Vector3 CenterPosition;

    public int Damage;
    public int CreatedTick;

    public float ElapsedSeconds;
    public float DurationSeconds;
    public float FinalRadius;
    public float CurrentRadius;

    public bool PlayerDamaged;
    public bool IsResolved;

    public readonly HashSet<string> DamagedNpcIds = new();

    public float Progress01 =>
        Mathf.Clamp01(ElapsedSeconds / Mathf.Max(0.01f, DurationSeconds));
}