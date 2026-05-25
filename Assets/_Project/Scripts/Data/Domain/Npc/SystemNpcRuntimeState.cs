using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SystemNpcRuntimeState
{
    [Header("Identity")]
    public string RuntimeNpcId;
    public SystemNpcType NpcType;

    public string ConfigId;
    public string SpawnRuleId;
    public string GroupRuntimeId;

    [Header("System Location")]
    public string OriginSystemId;
    public string CurrentSystemId;
    // public string TargetSystemId;
    public StarSystemLink TargetSystemLink;

    [Header("Planet Location")]
    public string CurrentPlanetId;
    public string TargetPlanetId;
    public bool IsOnPlanet;

    [Header("Position")]
    public Vector3 CurrentPosition;
    public Vector3 StartPosition;
    public Vector3 TargetPosition;

    [Header("Travel")]
    public SystemNpcTravelState TravelState;
    public float TravelProgress01;
    public int TravelStartTick;
    public int TravelEndTick;

    [Header("Behavior")]
    public SystemNpcBehaviorType PrevBehavior;
    public SystemNpcBehaviorType CurrentBehavior;
    public int BehaviorStartedTick;
    public int BehaviorEndsTick;
    public bool HasActiveBehavior;
    public bool CanChangeLocationOnRestore;

    [Header("Behavior Context")]
    public int DaysToStayOnPlanet;
    public int DaysStayedOnPlanet;
    public string BehaviorTargetRuntimeNpcId;

    [Header("Combat")]
    public SystemNpcCombatState CombatState;
    public string CurrentTargetRuntimeNpcId;
    public bool IsFighting;
    public bool IsAggressiveToPlayer;
    public bool WasDamagedByPlayer;
    
    [Header("Stats")]
    public int MaxHull;
    public int CurrentHull;

    public int MaxShield;
    public int CurrentShield;

    public int MaxEnergy;
    public int CurrentEnergy;

    public float Speed;

    [Header("Life")]
    public SystemNpcLifeState LifeState;
    public bool IsAlive;

    [Header("Rewards / Contribution")]
    public bool WasKilledByPlayer;
    public int CreditReward;
    public int XpReward;
    public int DangerTier;

    [Header("Weapons")]
    public List<SystemNpcWeaponRuntimeState> Weapons = new();

    public bool IsEnemy => NpcType == SystemNpcType.Enemy;
    public bool IsAlly => NpcType == SystemNpcType.Ally;
    public bool IsPirate => NpcType == SystemNpcType.Pirate;
    public bool IsHostileToPlayer => IsEnemy || IsPirate;

    public bool IsInSystem(string systemId)
    {
        return IsAlive && CurrentSystemId == systemId;
    }

    public bool IsAvailableForCombat()
    {
        return IsAlive && !IsOnPlanet && LifeState == SystemNpcLifeState.Alive;
    }

    public void ApplyDamage(int damage)
    {
        if (!IsAlive)
            return;

        if (damage <= 0)
            return;

        int remainingDamage = damage;

        if (CurrentShield > 0)
        {
            int shieldDamage = Mathf.Min(CurrentShield, remainingDamage);
            CurrentShield -= shieldDamage;
            remainingDamage -= shieldDamage;
        }

        if (remainingDamage > 0)
            CurrentHull -= remainingDamage;

        if (CurrentHull <= 0)
        {
            CurrentHull = 0;
            IsAlive = false;
            LifeState = SystemNpcLifeState.Destroyed;
        }
    }

    public void Annihilate()
    {
        IsAlive = false;
        LifeState = SystemNpcLifeState.Annihilated;
        IsOnPlanet = true;
        TravelState = SystemNpcTravelState.OnPlanet;
        CombatState = SystemNpcCombatState.None;
        IsFighting = false;
        HasActiveBehavior = false;
    }

    public float getShotDistance()
    {
        float distance = 0;
        foreach (SystemNpcWeaponRuntimeState item in Weapons)
            distance = Math.Max(distance, item.ShotDistance);

        return distance;
    }
}