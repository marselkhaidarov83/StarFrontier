using System;
using System.Collections.Generic;
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

        AddWeapons(
            npc,
            config.WeaponConfigs,
            config.WeaponConfig);

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

        AddWeapon(
            npc,
            config.WeaponConfig);

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

        AddWeapons(
            npc,
            config.WeaponConfigs,
            config.WeaponConfig);

        return npc;
    }

    private static void AddWeapons(
        SystemNpcRuntimeState npc,
        IReadOnlyList<WeaponConfig> weaponConfigs,
        WeaponConfig fallbackWeaponConfig)
    {
        bool addedAnyWeapon = false;

        if (weaponConfigs != null)
        {
            for (int i = 0; i < weaponConfigs.Count; i++)
            {
                WeaponConfig weaponConfig =
                    weaponConfigs[i];

                if (AddWeapon(npc, weaponConfig))
                    addedAnyWeapon = true;
            }
        }

        /*
         * Совместимость со старыми конфигами.
         * Если массив WeaponConfigs пустой, но старое свойство WeaponConfig
         * что-то возвращает, добавляем это одно оружие.
         */
        if (!addedAnyWeapon)
            AddWeapon(npc, fallbackWeaponConfig);
    }

    private static bool AddWeapon(
        SystemNpcRuntimeState npc,
        WeaponConfig weaponConfig)
    {
        if (npc == null)
            return false;

        if (weaponConfig == null)
            return false;

        if (string.IsNullOrWhiteSpace(weaponConfig.Id))
            return false;

        if (npc.Weapons == null)
            npc.Weapons = new List<SystemNpcWeaponRuntimeState>();

        if (HasWeapon(npc, weaponConfig.Id))
            return false;

        npc.Weapons.Add(
            new SystemNpcWeaponRuntimeState
            {
                WeaponConfigId = weaponConfig.Id,
                LastShotTick = -1,
                NextAllowedShotTick = 0,
                CooldownRemainingSeconds = 0f,
                ShotDistance = weaponConfig.Range
            });

        return true;
    }

    private static bool HasWeapon(
        SystemNpcRuntimeState npc,
        string weaponConfigId)
    {
        if (npc == null)
            return false;

        if (npc.Weapons == null)
            return false;

        if (string.IsNullOrWhiteSpace(weaponConfigId))
            return false;

        for (int i = 0; i < npc.Weapons.Count; i++)
        {
            SystemNpcWeaponRuntimeState weapon =
                npc.Weapons[i];

            if (weapon == null)
                continue;

            if (weapon.WeaponConfigId == weaponConfigId)
                return true;
        }

        return false;
    }
}