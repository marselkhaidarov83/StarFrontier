using System;
using UnityEngine;

[Serializable]
public sealed class GalaxyNpcProjectileRuntimeState
{
    public string ProjectileId;

    public string SystemId;

    public CombatShooterType ShooterType;
    public string ShooterNpcId;

    public CombatTargetType TargetType;
    public string TargetNpcId;

    public string WeaponConfigId;
    public WeaponShotType2A ShotType;

    public Vector3 StartPosition;
    public Vector3 CurrentPosition;
    public Vector3 LastKnownTargetPosition;
    public Vector3 PathOffset;

    public int Damage;

    public int ShotIndex;
    public int ShotCount;

    public int CreatedTick;
    public int ImpactTick;

    public float ElapsedSeconds;
    public float StartDelaySeconds;
    public float LifetimeSeconds;

    public bool IsResolved;
}