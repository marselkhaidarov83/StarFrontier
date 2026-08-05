using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SystemNpcSaveData
{
    public string RuntimeNpcId;
    public SystemNpcType NpcType;

    public string ConfigId;
    public string SpawnRuleId;
    public string GroupRuntimeId;
    public AllyRole2A AllyRole;
    public int Level;

    public string OriginSystemId;
    public string CurrentSystemId;
    public string TargetSystemId;
    public Vector3 TargetSystemExitPoint;
    public Vector3 TargetSystemEntryPoint;

    public string CurrentPlanetId;
    public string TargetPlanetId;
    public bool IsOnPlanet;

    public Vector3 CurrentPosition;
    public Vector3 StartPosition;
    public Vector3 TargetPosition;

    public SystemNpcTravelState TravelState;
    public float TravelProgress01;
    public int TravelStartTick;
    public int TravelEndTick;

    public SystemNpcBehaviorType PrevBehavior;
    public SystemNpcBehaviorType CurrentBehavior;
    public int BehaviorStartedTick;
    public int BehaviorEndsTick;
    public bool HasActiveBehavior;
    public bool CanChangeLocationOnRestore;

    public int DaysToStayOnPlanet;
    public int DaysStayedOnPlanet;
    public string BehaviorTargetRuntimeNpcId;

    public SystemNpcCombatState CombatState;
    public string CurrentTargetRuntimeNpcId;
    public bool IsFighting;
    public bool IsAggressiveToPlayer;
    public bool WasDamagedByPlayer;

    public int MaxHull;
    public int CurrentHull;

    public int MaxShield;
    public int CurrentShield;

    public int MaxEnergy;
    public int CurrentEnergy;

    public float Speed;

    public SystemNpcLifeState LifeState;
    public bool IsAlive;
    public int DestroyedAtTick;
    public int NextRespawnTick;

    public bool WasKilledByPlayer;
    public int CreditReward;
    public int XpReward;
    public int DangerTier;

    public List<SystemNpcWeaponSaveData> Weapons = new();
}
