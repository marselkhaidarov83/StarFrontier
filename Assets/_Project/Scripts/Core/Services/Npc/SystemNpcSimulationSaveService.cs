using System.Collections.Generic;
using UnityEngine;

public sealed class SystemNpcSimulationSaveService : CustomService, ISystemNpcSimulationSaveService
{
    private readonly ISystemNpcRuntimeService _npcRuntimeService;
    private readonly ISystemNpcPopulationService _populationService;

    public SystemNpcSimulationSaveService()
    {
        _debugStop = true;
        _npcRuntimeService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>();
        _populationService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcPopulationService>();
    }

    public SystemNpcSimulationSaveData Capture()
    {
        var saveData = new SystemNpcSimulationSaveData();

        foreach (SystemNpcRuntimeState npc in _npcRuntimeService.Npcs)
        {
            saveData.Npcs.Add(CaptureNpc(npc));
        }

        foreach (SystemPopulationRuleTimerState timer in _populationService.RuntimeState.Timers)
        {
            saveData.PopulationTimers.Add(new SystemPopulationRuleTimerState(
                timer.SystemId,
                timer.RuleId)
            {
                TimerSeconds = timer.TimerSeconds
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

        foreach (SystemNpcSaveData npcSave in saveData.Npcs)
        {
            restoredNpcs.Add(RestoreNpc(npcSave));
        }

        _npcRuntimeService.RestoreNpcs(restoredNpcs);

        foreach (SystemPopulationRuleTimerState timer in saveData.PopulationTimers)
        {
            var restoredTimer = _populationService.RuntimeState.GetOrCreateTimer(
                timer.SystemId,
                timer.RuleId
            );

            restoredTimer.TimerSeconds = timer.TimerSeconds;
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

            OriginSystemId = npc.OriginSystemId,
            CurrentSystemId = npc.CurrentSystemId,
            // TargetSystemId = npc.TargetSystemId,
            TargetSystemLink = npc.TargetSystemLink,

            CurrentPlanetId = npc.CurrentPlanetId,
            TargetPlanetId = npc.TargetPlanetId,
            IsOnPlanet = npc.IsOnPlanet,

            CurrentPosition = npc.CurrentPosition,
            StartPosition = npc.StartPosition,
            TargetPosition = npc.TargetPosition,

            TravelState = npc.TravelState,
            TravelProgress01 = npc.TravelProgress01,
            TravelStartTick = npc.TravelStartTick,
            TravelEndTick = npc.TravelEndTick,

            CurrentBehavior = npc.CurrentBehavior,
            BehaviorStartedTick = npc.BehaviorStartedTick,
            BehaviorEndsTick = npc.BehaviorEndsTick,
            HasActiveBehavior = npc.HasActiveBehavior,
            CanChangeLocationOnRestore = npc.CanChangeLocationOnRestore,

            DaysToStayOnPlanet = npc.DaysToStayOnPlanet,
            DaysStayedOnPlanet = npc.DaysStayedOnPlanet,
            BehaviorTargetRuntimeNpcId = npc.BehaviorTargetRuntimeNpcId,

            CombatState = npc.CombatState,
            CurrentTargetRuntimeNpcId = npc.CurrentTargetRuntimeNpcId,
            IsFighting = npc.IsFighting,

            MaxHull = npc.MaxHull,
            CurrentHull = npc.CurrentHull,

            MaxShield = npc.MaxShield,
            CurrentShield = npc.CurrentShield,

            MaxEnergy = npc.MaxEnergy,
            CurrentEnergy = npc.CurrentEnergy,

            Speed = npc.Speed,

            LifeState = npc.LifeState,
            IsAlive = npc.IsAlive,

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
                CooldownRemainingSeconds = weapon.CooldownRemainingSeconds
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

            OriginSystemId = save.OriginSystemId,
            CurrentSystemId = save.CurrentSystemId,
            // TargetSystemId = save.TargetSystemId,
            TargetSystemLink = save.TargetSystemLink,

            CurrentPlanetId = save.CurrentPlanetId,
            TargetPlanetId = save.TargetPlanetId,
            IsOnPlanet = save.IsOnPlanet,

            CurrentPosition = save.CurrentPosition,
            StartPosition = save.StartPosition,
            TargetPosition = save.TargetPosition,

            TravelState = save.TravelState,
            TravelProgress01 = save.TravelProgress01,
            TravelStartTick = save.TravelStartTick,
            TravelEndTick = save.TravelEndTick,

            CurrentBehavior = save.CurrentBehavior,
            BehaviorStartedTick = save.BehaviorStartedTick,
            BehaviorEndsTick = save.BehaviorEndsTick,
            HasActiveBehavior = save.HasActiveBehavior,
            CanChangeLocationOnRestore = save.CanChangeLocationOnRestore,

            DaysToStayOnPlanet = save.DaysToStayOnPlanet,
            DaysStayedOnPlanet = save.DaysStayedOnPlanet,
            BehaviorTargetRuntimeNpcId = save.BehaviorTargetRuntimeNpcId,

            CombatState = save.CombatState,
            CurrentTargetRuntimeNpcId = save.CurrentTargetRuntimeNpcId,
            IsFighting = save.IsFighting,

            MaxHull = save.MaxHull,
            CurrentHull = save.CurrentHull,

            MaxShield = save.MaxShield,
            CurrentShield = save.CurrentShield,

            MaxEnergy = save.MaxEnergy,
            CurrentEnergy = save.CurrentEnergy,

            Speed = save.Speed,

            LifeState = save.LifeState,
            IsAlive = save.IsAlive,

            WasKilledByPlayer = save.WasKilledByPlayer,
            CreditReward = save.CreditReward,
            XpReward = save.XpReward,
            DangerTier = save.DangerTier
        };

        foreach (SystemNpcWeaponSaveData weaponSave in save.Weapons)
        {
            npc.Weapons.Add(new SystemNpcWeaponRuntimeState
            {
                WeaponConfigId = weaponSave.WeaponConfigId,
                LastShotTick = weaponSave.LastShotTick,
                CooldownRemainingSeconds = weaponSave.CooldownRemainingSeconds
            });
        }

        // ApplyRestoreLocationMutationIfNeeded(npc);

        return npc;
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