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

        int hull =
            UnityEngine.Random.Range(
                config.BaseHullMin,
                config.BaseHullMax + 1);

        int shield =
            UnityEngine.Random.Range(
                config.BaseShieldMin,
                config.BaseShieldMax + 1);

        int energy =
            UnityEngine.Random.Range(
                config.BaseEnergyMin,
                config.BaseEnergyMax + 1);

        float speed =
            UnityEngine.Random.Range(
                config.BaseSpeedMin,
                config.BaseSpeedMax);

        int creditReward =
            UnityEngine.Random.Range(
                config.CreditRewardMin,
                config.CreditRewardMax + 1);

        int xpReward =
            UnityEngine.Random.Range(
                config.XpRewardMin,
                config.XpRewardMax + 1);

        var npc = new SystemNpcRuntimeState
        {
            RuntimeNpcId = Guid.NewGuid().ToString("N"),
            NpcType = SystemNpcType.Enemy,

            ConfigId = config.Id,
            SpawnRuleId = spawnRuleId,
            GroupRuntimeId = groupRuntimeId,
            Level = config.Level,

            OriginSystemId = originSystemId,
            CurrentSystemId = currentSystemId,

            CurrentPosition = position,
            TargetPosition = position,

            TravelState = SystemNpcTravelState.Idle,
            CurrentBehavior = SystemNpcBehaviorType.EngageEnemies,
            CombatState = SystemNpcCombatState.SearchingTarget,

            MaxHull = hull,
            CurrentHull = hull,

            MaxShield = shield,
            CurrentShield = shield,

            MaxEnergy = energy,
            CurrentEnergy = energy,

            Speed = speed,

            LifeState = SystemNpcLifeState.Alive,
            IsAlive = true,
            DestroyedAtTick = 0,
            NextRespawnTick = 0,

            CreditReward = creditReward,
            XpReward = xpReward,
            DangerTier = config.DangerTier
        };

        AddRandomEnemyWeaponGroup(
            npc,
            config);

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
            Level = 1,

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
            DestroyedAtTick = 0,
            NextRespawnTick = 0,

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
            AllyRole = config.Role,
            Level = config.Level,

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
            DestroyedAtTick = 0,
            NextRespawnTick = 0,

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

    private static void AddRandomEnemyWeaponGroup(
        SystemNpcRuntimeState npc,
        EnemyConfig config)
    {
        if (npc == null)
            return;

        if (config == null)
            return;

        WeaponGroupConfig weaponGroup =
            PickRandomValidWeaponGroup(
                config.WeaponGroups);

        if (weaponGroup != null)
        {
            AddWeapons(
                npc,
                weaponGroup.WeaponConfigs,
                null);

            return;
        }

        /*
         * Safety fallback для старых или ещё не заполненных EnemyConfig:
         * если WeaponGroups пустой, но legacy WeaponConfigs ещё есть,
         * добавляем их как прежний плоский набор оружия.
         */
        AddWeapons(
            npc,
            config.WeaponConfigs,
            config.WeaponConfig);
    }

    private static WeaponGroupConfig PickRandomValidWeaponGroup(
        IReadOnlyList<WeaponGroupConfig> weaponGroups)
    {
        if (weaponGroups == null)
            return null;

        List<WeaponGroupConfig> validGroups =
            new List<WeaponGroupConfig>();

        for (int i = 0; i < weaponGroups.Count; i++)
        {
            WeaponGroupConfig group =
                weaponGroups[i];

            if (group == null)
                continue;

            if (!group.IsValid())
                continue;

            validGroups.Add(group);
        }

        if (validGroups.Count == 0)
            return null;

        int index =
            UnityEngine.Random.Range(
                0,
                validGroups.Count);

        return validGroups[index];
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
         * Если список WeaponConfigs пустой, но старое свойство WeaponConfig
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

        WeaponRuntimeStats weaponStats =
            weaponConfig.RollRuntimeStats(
                BuildWeaponRollSeed(
                    npc.RuntimeNpcId,
                    weaponConfig.Id,
                    npc.Weapons.Count.ToString()
                )
            );

        npc.Weapons.Add(
            new SystemNpcWeaponRuntimeState
            {
                WeaponConfigId = weaponConfig.Id,

                LastShotTick = -1,
                NextAllowedShotTick = 0,
                CooldownRemainingSeconds = 0f,

                /*
                 * В WeaponConfig v0.6 больше нет weaponConfig.Range.
                 * Дальность выбирается из диапазона rangeMin/rangeMax
                 * при создании runtime-оружия.
                 */
                ShotDistance = weaponStats.Range
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

    private static int BuildWeaponRollSeed(
        params string[] parts)
    {
        unchecked
        {
            int hash = 17;

            if (parts == null)
                return hash;

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];

                if (string.IsNullOrEmpty(part))
                {
                    hash = hash * 31;
                    continue;
                }

                for (int j = 0; j < part.Length; j++)
                    hash = hash * 31 + part[j];
            }

            return hash;
        }
    }
}