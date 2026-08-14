using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SystemNpcRuntimeState
{
    [Header("Identity")]
    public string RuntimeNpcId;
    public SystemNpcType NpcType;
    public string DisplayName;

    public string ConfigId;
    public string SpawnRuleId;
    public string GroupRuntimeId;

    [Header("Identity Details")]
    public AllyRole2A AllyRole;
    public int Level = 1;

    [Header("System Location")]
    public string OriginSystemId;
    public string CurrentSystemId;
    public string TargetSystemId;
    public Vector3 TargetSystemExitPoint;
    public Vector3 TargetSystemEntryPoint;

    [Header("Planet Location")]
    public string CurrentPlanetId;
    public string TargetPlanetId;
    public bool IsOnPlanet;

    [Header("Position")]
    public Vector3 CurrentPosition;
    public Vector3 StartPosition;
    public Vector3 TargetPosition;
    public Vector3 CurrentMovementTargetPosition;

    public Vector3 TickMovementTargetPosition;
    public Vector3 TickMovementDirection = Vector3.up;
    public int TickMovementDirectionTick = -1;
    public bool TickMovementArrived;

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

    public int Speed;

    [Header("Life")]
    public SystemNpcLifeState LifeState;
    public bool IsAlive;
    public int DestroyedAtTick;
    public int NextRespawnTick;

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

    public void ApplyDamageResult(
        int currentShield,
        int currentHull)
    {
        if (!IsAlive)
            return;

        CurrentShield = Mathf.Max(0, currentShield);
        CurrentHull = Mathf.Max(0, currentHull);

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
        float distance = 0f;

        foreach (SystemNpcWeaponRuntimeState item in Weapons)
            distance = Math.Max(distance, item.ShotDistance);

        return distance;
    }
}
