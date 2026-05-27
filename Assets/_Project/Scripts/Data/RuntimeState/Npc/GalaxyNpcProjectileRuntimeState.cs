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

    public Vector3 StartPosition;
    public Vector3 CurrentPosition;
    public Vector3 LastKnownTargetPosition;

    public int Damage;

    public int CreatedTick;
    public int ImpactTick;

    public float ElapsedSeconds;
    public float LifetimeSeconds;

    public bool IsResolved;
}