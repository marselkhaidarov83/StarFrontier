using System.Collections.Generic;
using UnityEngine;

public sealed class SystemNpcSimulationSaveService : CustomService, ISystemNpcSimulationSaveService
{
    private readonly ISystemNpcRuntimeService _npcRuntimeService;
    private readonly ISystemNpcPopulationService _populationService;
    private readonly IConfigService _configService;

    public SystemNpcSimulationSaveService()
    {
        _debugStop = true;
        _npcRuntimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _populationService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcPopulationService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
    }

    public SystemNpcSimulationSaveData Capture()
    {
        var saveData = new SystemNpcSimulationSaveData();

        foreach (SystemNpcRuntimeState npc in _npcRuntimeService.Npcs)
        {
            if (npc == null)
                continue;

            saveData.Npcs.Add(CaptureNpc(npc));
        }

        foreach (SystemPopulationRuleTimerState timer in _populationService.RuntimeState.Timers)
        {
            saveData.PopulationTimers.Add(new SystemPopulationRuleTimerState(
                timer.SystemId,
                timer.RuleId)
            {
                TimerSeconds = timer.TimerSeconds,
                NextSpawnTick = timer.NextSpawnTick
            });
        }

        LogCustom(
            $"[SystemNpcSimulationSaveService] Captured. " +
            $"NPCs: {saveData.Npcs.Count}, Timers: {saveData.PopulationTimers.Count}"
        );

        return saveData;
    }

    public void Restore(SystemNpcSimulationSaveData saveData)
    {
        _npcRuntimeService.ClearAll();
        _populationService.ClearRuntimeState();

        if (saveData == null)
        {
            LogCustom("Restore skipped: saveData is null.");
            return;
        }

        var restoredNpcs = new List<SystemNpcRuntimeState>();

        if (saveData.Npcs != null)
        {
            foreach (SystemNpcSaveData npcSave in saveData.Npcs)
            {
                if (npcSave != null)
                    restoredNpcs.Add(RestoreNpc(npcSave));
            }
        }

        _npcRuntimeService.RestoreNpcs(restoredNpcs);

        if (saveData.PopulationTimers != null)
        {
            foreach (SystemPopulationRuleTimerState timer in saveData.PopulationTimers)
            {
                if (timer == null)
                    continue;

                var restoredTimer = _populationService.RuntimeState.GetOrCreateTimer(
                    timer.SystemId,
                    timer.RuleId
                );

                restoredTimer.TimerSeconds = timer.TimerSeconds;
                restoredTimer.NextSpawnTick = timer.NextSpawnTick;
            }
        }

        LogCustom(
            $"[SystemNpcSimulationSaveService] Restored. " +
            $"NPCs: {restoredNpcs.Count}, Timers: {saveData.PopulationTimers.Count}"
        );
    }

    private SystemNpcSaveData CaptureNpc(SystemNpcRuntimeState npc)
    {
        var save = new SystemNpcSaveData
        {
            RuntimeNpcId = npc.RuntimeNpcId,
            NpcType = npc.NpcType,

            ConfigId = npc.ConfigId,
            SpawnRuleId = npc.SpawnRuleId,
            GroupRuntimeId = npc.GroupRuntimeId,
            AllyRole = npc.AllyRole,
            Level = npc.Level,

            OriginSystemId = npc.OriginSystemId,
            CurrentSystemId = npc.CurrentSystemId,

            TargetSystemId = null,
            TargetSystemExitPoint = Vector3.zero,
            TargetSystemEntryPoint = Vector3.zero,

            CurrentPlanetId = npc.IsOnPlanet ? npc.CurrentPlanetId : null,
            TargetPlanetId = null,
            IsOnPlanet = npc.IsOnPlanet,

            CurrentPosition = npc.CurrentPosition,
            StartPosition = npc.CurrentPosition,
            TargetPosition = npc.CurrentPosition,
            FacingDirection = npc.FacingDirection,
            TurnRadius = npc.TurnRadius,

            TravelState = npc.IsOnPlanet
                ? SystemNpcTravelState.OnPlanet
                : SystemNpcTravelState.Idle,

            TravelProgress01 = 0f,
            TravelStartTick = 0,
            TravelEndTick = 0,

            PrevBehavior = npc.PrevBehavior,
            CurrentBehavior = npc.CurrentBehavior,
            BehaviorStartedTick = npc.BehaviorStartedTick,
            BehaviorEndsTick = npc.BehaviorEndsTick,
            HasActiveBehavior = npc.HasActiveBehavior,
            CanChangeLocationOnRestore = npc.CanChangeLocationOnRestore,

            DaysToStayOnPlanet = npc.DaysToStayOnPlanet,
            DaysStayedOnPlanet = npc.DaysStayedOnPlanet,
            BehaviorTargetRuntimeNpcId = null,

            CombatState = SystemNpcCombatState.None,
            CurrentTargetRuntimeNpcId = null,
            IsFighting = false,
            IsAggressiveToPlayer = npc.IsAggressiveToPlayer,
            WasDamagedByPlayer = npc.WasDamagedByPlayer,

            MaxHull = npc.MaxHull,
            CurrentHull = npc.CurrentHull,

            MaxShield = npc.MaxShield,
            CurrentShield = npc.CurrentShield,

            MaxEnergy = npc.MaxEnergy,
            CurrentEnergy = npc.CurrentEnergy,

            Speed = npc.Speed,

            LifeState = npc.LifeState,
            IsAlive = npc.IsAlive,
            DestroyedAtTick = npc.DestroyedAtTick,
            NextRespawnTick = npc.NextRespawnTick,

            WasKilledByPlayer = npc.WasKilledByPlayer,
            CreditReward = npc.CreditReward,
            XpReward = npc.XpReward,
            DangerTier = npc.DangerTier
        };

        foreach (SystemNpcWeaponRuntimeState weapon in npc.Weapons)
        {
            save.Weapons.Add(new SystemNpcWeaponSaveData
            {
                WeaponConfigId = weapon.WeaponConfigId,
                LastShotTick = weapon.LastShotTick,
                CooldownRemainingSeconds = weapon.CooldownRemainingSeconds,
                ShotDistance = weapon.ShotDistance
            });
        }

        return save;
    }

    private SystemNpcRuntimeState RestoreNpc(SystemNpcSaveData save)
    {
        var npc = new SystemNpcRuntimeState
        {
            RuntimeNpcId = save.RuntimeNpcId,
            NpcType = save.NpcType,

            ConfigId = save.ConfigId,
            SpawnRuleId = save.SpawnRuleId,
            GroupRuntimeId = save.GroupRuntimeId,
            AllyRole = save.AllyRole,
            Level = Mathf.Max(1, save.Level),

            OriginSystemId = save.OriginSystemId,
            CurrentSystemId = save.CurrentSystemId,

            TargetSystemId = null,
            TargetSystemExitPoint = Vector3.zero,
            TargetSystemEntryPoint = Vector3.zero,

            CurrentPlanetId = save.IsOnPlanet ? save.CurrentPlanetId : null,
            TargetPlanetId = null,
            IsOnPlanet = save.IsOnPlanet,

            CurrentPosition = save.CurrentPosition,
            StartPosition = save.CurrentPosition,
            TargetPosition = save.CurrentPosition,
            FacingDirection = save.FacingDirection,
            TurnRadius = save.TurnRadius,

            TravelState = save.IsOnPlanet
                ? SystemNpcTravelState.OnPlanet
                : SystemNpcTravelState.Idle,

            TravelProgress01 = 0f,
            TravelStartTick = 0,
            TravelEndTick = 0,

            PrevBehavior = save.PrevBehavior,
            CurrentBehavior = save.CurrentBehavior,
            BehaviorStartedTick = save.BehaviorStartedTick,
            BehaviorEndsTick = save.BehaviorEndsTick,
            HasActiveBehavior = save.HasActiveBehavior,
            CanChangeLocationOnRestore = save.CanChangeLocationOnRestore,

            DaysToStayOnPlanet = save.DaysToStayOnPlanet,
            DaysStayedOnPlanet = save.DaysStayedOnPlanet,
            BehaviorTargetRuntimeNpcId = null,

            CombatState = SystemNpcCombatState.None,
            CurrentTargetRuntimeNpcId = null,
            IsFighting = false,
            IsAggressiveToPlayer = save.IsAggressiveToPlayer,
            WasDamagedByPlayer = save.WasDamagedByPlayer,

            MaxHull = save.MaxHull,
            CurrentHull = save.CurrentHull,

            MaxShield = save.MaxShield,
            CurrentShield = save.CurrentShield,

            MaxEnergy = save.MaxEnergy,
            CurrentEnergy = save.CurrentEnergy,

            Speed = save.Speed,

            LifeState = save.LifeState,
            IsAlive = save.IsAlive,
            DestroyedAtTick = Mathf.Max(0, save.DestroyedAtTick),
            NextRespawnTick = Mathf.Max(0, save.NextRespawnTick),

            WasKilledByPlayer = save.WasKilledByPlayer,
            CreditReward = save.CreditReward,
            XpReward = save.XpReward,
            DangerTier = save.DangerTier
        };

        NormalizeRestoredNpcActivity(npc);

        RestoreRuntimeDisplayName(npc);
        ValidateRuntimeStatsFromConfig(npc);

        if (save.Weapons == null)
            return npc;

        foreach (SystemNpcWeaponSaveData weaponSave in save.Weapons)
        {
            if (weaponSave == null)
                continue;

            npc.Weapons.Add(new SystemNpcWeaponRuntimeState
            {
                WeaponConfigId = weaponSave.WeaponConfigId,
                LastShotTick = weaponSave.LastShotTick,
                CooldownRemainingSeconds = weaponSave.CooldownRemainingSeconds,
                ShotDistance = ResolveRestoredWeaponShotDistance(weaponSave)
            });
        }

        return npc;
    }

    private void NormalizeRestoredNpcActivity(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return;

        ClearRestoredTargets(npc);
        ResetRestoredMovementTargetsToCurrentPosition(npc);

        if (!npc.IsAlive)
        {
            npc.HasActiveBehavior = false;
            npc.PrevBehavior = SystemNpcBehaviorType.None;
            npc.CurrentBehavior = SystemNpcBehaviorType.None;
            npc.TravelState = SystemNpcTravelState.Idle;
            return;
        }

        if (npc.IsOnPlanet && string.IsNullOrWhiteSpace(npc.CurrentPlanetId))
        {
            npc.IsOnPlanet = false;
            npc.CurrentPlanetId = null;
        }

        if (ShouldDropRestoredBehavior(npc.CurrentBehavior))
        {
            npc.PrevBehavior = SystemNpcBehaviorType.None;
            npc.CurrentBehavior = SystemNpcBehaviorType.None;
            npc.HasActiveBehavior = false;
            npc.BehaviorStartedTick = 0;
            npc.BehaviorEndsTick = 0;
            npc.DaysToStayOnPlanet = 0;
            npc.DaysStayedOnPlanet = 0;
        }

        npc.TravelState = npc.IsOnPlanet
            ? SystemNpcTravelState.OnPlanet
            : SystemNpcTravelState.Idle;
    }

    private static bool ShouldDropRestoredBehavior(
    SystemNpcBehaviorType behavior)
    {
        switch (behavior)
        {
            case SystemNpcBehaviorType.StayOnPlanetForDays:
            case SystemNpcBehaviorType.AnnihilateOnPlanet:
                return false;

            default:
                return true;
        }
    }

    private static void ClearRestoredTargets(SystemNpcRuntimeState npc)
    {
        npc.TargetSystemId = null;
        npc.TargetSystemExitPoint = Vector3.zero;
        npc.TargetSystemEntryPoint = Vector3.zero;

        npc.TargetPlanetId = null;
        npc.CurrentTargetRuntimeNpcId = null;
        npc.BehaviorTargetRuntimeNpcId = null;

        npc.CombatState = SystemNpcCombatState.None;
        npc.IsFighting = false;
    }
    private static void ResetRestoredMovementTargetsToCurrentPosition(
        SystemNpcRuntimeState npc)
    {
        npc.StartPosition = npc.CurrentPosition;
        npc.TargetPosition = npc.CurrentPosition;
        npc.CurrentMovementTargetPosition = npc.CurrentPosition;
        npc.TickMovementTargetPosition = npc.CurrentPosition;
        npc.TickMovementArrived = true;
        npc.TickMovementDirectionTick = -1;

        if (npc.FacingDirection.sqrMagnitude <= 0.0001f)
            npc.FacingDirection = Vector3.up;

        npc.FacingDirection.Normalize();
        npc.TickMovementDirection = npc.FacingDirection;
    }

    private float ResolveRestoredWeaponShotDistance(
        SystemNpcWeaponSaveData weaponSave)
    {
        if (weaponSave == null)
            return 0f;

        if (weaponSave.ShotDistance > 0f)
            return weaponSave.ShotDistance;

        if (string.IsNullOrWhiteSpace(weaponSave.WeaponConfigId))
            return 0f;

        WeaponConfig weaponConfig =
            _configService.GetWeaponConfigById(
                weaponSave.WeaponConfigId);

        if (weaponConfig == null)
            return 0f;

        return weaponConfig.RangeMax;
    }

    private void RestoreRuntimeDisplayName(SystemNpcRuntimeState npc)
    {
        if (npc == null || string.IsNullOrWhiteSpace(npc.ConfigId))
            return;

        if (npc.IsEnemy)
        {
            EnemyConfig config =
                _configService.GetEnemyConfigById(npc.ConfigId);

            if (config != null)
                npc.DisplayName = config.PickRuntimeDisplayName(npc.RuntimeNpcId);

            return;
        }

        if (npc.IsAlly)
        {
            AllyConfig config =
                _configService.GetAllyConfigById(npc.ConfigId);

            if (config != null)
                npc.DisplayName = config.PickRuntimeDisplayName(npc.RuntimeNpcId);
        }
    }

    private void ValidateRuntimeStatsFromConfig(SystemNpcRuntimeState npc)
    {
        if (npc == null || string.IsNullOrWhiteSpace(npc.ConfigId))
            return;

        if (npc.IsAlly)
        {
            AllyConfig config =
                _configService.GetAllyConfigById(npc.ConfigId);

            if (config == null)
                return;

            npc.MaxHull = ValidateIntRange(npc.MaxHull, config.BaseHullMin, config.BaseHullMax);
            npc.CurrentHull = ValidateIntRange(npc.CurrentHull, config.BaseHullMin, config.BaseHullMax);
            npc.MaxShield = ValidateIntRange(npc.MaxShield, config.BaseShieldMin, config.BaseShieldMax);
            npc.CurrentShield = ValidateIntRange(npc.CurrentShield, config.BaseShieldMin, config.BaseShieldMax);
            npc.MaxEnergy = ValidateIntRange(npc.MaxEnergy, config.BaseEnergyMin, config.BaseEnergyMax);
            npc.CurrentEnergy = ValidateIntRange(npc.CurrentEnergy, config.BaseEnergyMin, config.BaseEnergyMax);
            npc.Speed = ValidateIntRange(npc.Speed, config.BaseSpeedMin, config.BaseSpeedMax);
            npc.TurnRadius = ValidateTurnRadius(npc.TurnRadius, config.TurnRadius);
            return;
        }

        if (npc.IsEnemy)
        {
            EnemyConfig config =
                _configService.GetEnemyConfigById(npc.ConfigId);

            if (config == null)
                return;

            npc.MaxHull = ValidateIntRange(npc.MaxHull, config.BaseHullMin, config.BaseHullMax);
            npc.CurrentHull = ValidateIntRange(npc.CurrentHull, config.BaseHullMin, config.BaseHullMax);
            npc.MaxShield = ValidateIntRange(npc.MaxShield, config.BaseShieldMin, config.BaseShieldMax);
            npc.CurrentShield = ValidateIntRange(npc.CurrentShield, config.BaseShieldMin, config.BaseShieldMax);
            npc.MaxEnergy = ValidateIntRange(npc.MaxEnergy, config.BaseEnergyMin, config.BaseEnergyMax);
            npc.CurrentEnergy = ValidateIntRange(npc.CurrentEnergy, config.BaseEnergyMin, config.BaseEnergyMax);
            npc.Speed = ValidateIntRange(npc.Speed, config.BaseSpeedMin, config.BaseSpeedMax);
            npc.TurnRadius = ValidateTurnRadius(npc.TurnRadius, config.TurnRadius);
        }
    }

    private static float ValidateTurnRadius(
        float value,
        float fallback)
    {
        if (float.IsNaN(value) ||
            float.IsInfinity(value) ||
            value <= 0f)
        {
            return Mathf.Max(0f, fallback);
        }

        return value;
    }

    private static int ValidateIntRange(
        int value,
        int min,
        int max)
    {
        return value < min || value > max
            ? min
            : value;
    }

    private static float ValidateFloatRange(
        float value,
        float min,
        float max)
    {
        return value < min || value > max
            ? min
            : value;
    }

    private void ApplyRestoreLocationMutationIfNeeded(SystemNpcRuntimeState npc)
    {
        if (!npc.CanChangeLocationOnRestore)
            return;

        if (!npc.IsAlive)
            return;

        if (npc.IsOnPlanet)
            return;

        if (npc.TravelState == SystemNpcTravelState.TravelingInsideSystem ||
            npc.TravelState == SystemNpcTravelState.Patrolling)
        {
            Vector2 random = Random.insideUnitCircle * 2f;

            npc.CurrentPosition += new Vector3(random.x, random.y, 0f);
            npc.StartPosition = npc.CurrentPosition;

            LogCustom(
                $"[SystemNpcSimulationSaveService] Restore mutation applied to NPC: {npc.RuntimeNpcId}"
            );
        }
    }
}
