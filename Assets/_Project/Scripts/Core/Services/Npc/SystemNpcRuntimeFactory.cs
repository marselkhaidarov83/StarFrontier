using System;
using UnityEngine;

public static class SystemNpcRuntimeFactory
{
    public static SystemNpcRuntimeState CreateEnemy(
        EnemyConfig config,
        string originSystemId,
        string currentSystemId,
        Vector3 position,
        string spawnRuleId = null,
        string groupRuntimeId = null)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var npc = new SystemNpcRuntimeState
        {
            RuntimeNpcId = Guid.NewGuid().ToString("N"),
            NpcType = SystemNpcType.Enemy,

            ConfigId = config.Id,
            SpawnRuleId = spawnRuleId,
            GroupRuntimeId = groupRuntimeId,

            OriginSystemId = originSystemId,
            CurrentSystemId = currentSystemId,

            CurrentPosition = position,
            TargetPosition = position,

            TravelState = SystemNpcTravelState.Idle,
            CurrentBehavior = SystemNpcBehaviorType.EngageEnemies,
            CombatState = SystemNpcCombatState.SearchingTarget,

            MaxHull = config.BaseHull,
            CurrentHull = config.BaseHull,

            MaxShield = config.BaseShield,
            CurrentShield = config.BaseShield,

            MaxEnergy = config.BaseEnergy,
            CurrentEnergy = config.BaseEnergy,

            Speed = config.BaseSpeed,

            LifeState = SystemNpcLifeState.Alive,
            IsAlive = true,

            CreditReward = config.CreditReward,
            XpReward = config.XpReward,
            DangerTier = config.DangerTier
        };

        if (config.WeaponConfig != null)
        {
            npc.Weapons.Add(new SystemNpcWeaponRuntimeState
            {
                WeaponConfigId = config.WeaponConfig.Id,
                ShotDistance = config.WeaponConfig.Range
            });
        }

        return npc;
    }

    public static SystemNpcRuntimeState CreatePirate(
        PirateConfig config,
        string originSystemId,
        string currentSystemId,
        Vector3 position,
        string spawnRuleId = null,
        string groupRuntimeId = null)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var npc = new SystemNpcRuntimeState
        {
            RuntimeNpcId = Guid.NewGuid().ToString("N"),
            NpcType = SystemNpcType.Pirate,

            ConfigId = config.Id,
            SpawnRuleId = spawnRuleId,
            GroupRuntimeId = groupRuntimeId,

            OriginSystemId = originSystemId,
            CurrentSystemId = currentSystemId,

            CurrentPosition = position,
            TargetPosition = position,

            TravelState = SystemNpcTravelState.OnPlanet,
            CurrentBehavior = SystemNpcBehaviorType.StayOnPlanetForDays,
            CombatState = SystemNpcCombatState.None,

            MaxHull = config.BaseHull,
            CurrentHull = config.BaseHull,

            MaxShield = config.BaseShield,
            CurrentShield = config.BaseShield,

            MaxEnergy = config.BaseEnergy,
            CurrentEnergy = config.BaseEnergy,

            Speed = config.BaseSpeed,

            LifeState = SystemNpcLifeState.Alive,
            IsAlive = true,

            CreditReward = config.CreditReward,
            XpReward = config.XpReward,
            DangerTier = config.DangerTier
        };

        if (config.WeaponConfig != null)
        {
            npc.Weapons.Add(new SystemNpcWeaponRuntimeState
            {
                WeaponConfigId = config.WeaponConfig.Id,
                ShotDistance = config.WeaponConfig.Range
            });
        }

        return npc;
    }

    public static SystemNpcRuntimeState CreateAlly(
        AllyConfig config,
        string originSystemId,
        string currentSystemId,
        string currentPlanetId,
        Vector3 position,
        string spawnRuleId = null)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var npc = new SystemNpcRuntimeState
        {
            RuntimeNpcId = Guid.NewGuid().ToString("N"),
            NpcType = SystemNpcType.Ally,

            ConfigId = config.Id,
            SpawnRuleId = spawnRuleId,

            OriginSystemId = originSystemId,
            CurrentSystemId = currentSystemId,
            CurrentPlanetId = currentPlanetId,

            StartPosition = position,
            CurrentPosition = position,
            TargetPosition = position,
            TravelProgress01 = 1f,

            TravelState = SystemNpcTravelState.OnPlanet,
            CurrentBehavior = SystemNpcBehaviorType.StayOnPlanetForDays,
            CombatState = SystemNpcCombatState.None,

            MaxHull = config.BaseHull,
            CurrentHull = config.BaseHull,

            MaxShield = config.BaseShield,
            CurrentShield = config.BaseShield,

            MaxEnergy = config.BaseEnergy,
            CurrentEnergy = config.BaseEnergy,

            Speed = config.BaseSpeed,

            LifeState = SystemNpcLifeState.Alive,
            IsAlive = true,

            IsOnPlanet = true,

            CreditReward = 0,
            XpReward = 0,
            DangerTier = 1
        };

        if (config.WeaponConfig != null)
        {
            npc.Weapons.Add(new SystemNpcWeaponRuntimeState
            {
                WeaponConfigId = config.WeaponConfig.Id,
                ShotDistance = config.WeaponConfig.Range
            });
        }

        return npc;
    }
}