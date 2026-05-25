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

    public string OriginSystemId;
    public string CurrentSystemId;
    public StarSystemLink TargetSystemLink;

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

    public int MaxHull;
    public int CurrentHull;

    public int MaxShield;
    public int CurrentShield;

    public int MaxEnergy;
    public int CurrentEnergy;

    public float Speed;

    public SystemNpcLifeState LifeState;
    public bool IsAlive;

    public bool WasKilledByPlayer;
    public int CreditReward;
    public int XpReward;
    public int DangerTier;

    public List<SystemNpcWeaponSaveData> Weapons = new();
}