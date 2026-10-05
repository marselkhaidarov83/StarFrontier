using System;
using System.Collections.Generic;
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

    public readonly List<Vector3> RoutePoints = new();
    public int RouteSegmentIndex;

    public int Damage;

    public int ShotIndex;
    public int ShotCount;

    public int CreatedTick;
    public int ImpactTick;

    public float ElapsedSeconds;
    public float StartDelaySeconds;
    public float LifetimeSeconds;

    public float InitialSpeedUnitsPerSecond;
    public float FinalSpeedUnitsPerSecond;
    public float TravelledDistance;

    public bool RequiresArrivalToDamage;
    public float ArrivalDistance;

    public float MissileCurveOffset;

    public bool HasReleased;
    public bool ShouldDamageOnArrival;

    public bool ShouldSpawnHitFxOnMiss;
    public bool ResolveAfterTargetLost;
    public float ResolveAtElapsedSeconds;

    public bool IsResolved;
}