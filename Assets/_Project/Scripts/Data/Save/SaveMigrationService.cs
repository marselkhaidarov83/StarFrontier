using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Приводит старые save-state к актуальной структуре данных.
/// </summary>
public static class SaveMigrationService
{
    public static bool Migrate(GameRuntimeState state)
    {
        if (state == null)
            return false;

        EnsureRequiredBlocks(state);

        int sourceVersion =
            state.Meta.SaveDataVersion;

        if (sourceVersion <= SaveDataVersions.Unknown)
            sourceVersion = SaveDataVersions.LegacyStage2A;

        bool changed = false;

        if (sourceVersion < SaveDataVersions.ShipDirection)
        {
            MigrateToShipDirection(state);
            changed = true;
            sourceVersion = SaveDataVersions.ShipDirection;
        }

        NormalizeShipDirection(state);

        if (sourceVersion < SaveDataVersions.IntegrityChecksum)
        {
            MigrateToIntegrityChecksum(state);
            changed = true;
            sourceVersion = SaveDataVersions.IntegrityChecksum;
        }

        if (sourceVersion < SaveDataVersions.SystemSecurity)
        {
            MigrateToSystemSecurity(state);
            changed = true;
            sourceVersion = SaveDataVersions.SystemSecurity;
        }

        if (sourceVersion < SaveDataVersions.SystemNpcPersistentState)
        {
            MigrateToSystemNpcPersistentState(state);
            changed = true;
            sourceVersion = SaveDataVersions.SystemNpcPersistentState;
        }

        if (sourceVersion < SaveDataVersions.SystemWarState)
        {
            MigrateToSystemWarState(state);
            changed = true;
            sourceVersion = SaveDataVersions.SystemWarState;
        }

        if (state.Meta.SaveDataVersion != SaveDataVersions.Current)
        {
            state.Meta.SaveDataVersion = SaveDataVersions.Current;
            changed = true;
        }

        return changed;
    }

    private static void EnsureRequiredBlocks(
        GameRuntimeState state)
    {
        state.Meta ??= new GameRuntimeMetaState();
        state.Player ??= new PlayerState();
        state.Galaxy ??= new GalaxyRuntimeState();
        state.Settings ??= new GameSettingsState();

        state.Galaxy.EnsureWarStateCollections();

        state.Markets ??=
            new List<MarketRuntimeData>();

        state.MissionBlock ??=
            new RuntimeMissionSaveBlock();

        state.SystemEncounter ??=
            new SystemEncounterSaveData();

        state.SystemNpcSimulation ??=
            new SystemNpcSimulationSaveData();

        state.SystemNpcSimulation.Npcs ??=
            new List<SystemNpcSaveData>();

        state.SystemNpcSimulation.PopulationTimers ??=
            new List<SystemPopulationRuleTimerState>();

        state.Player.PlayerShipState ??=
            new ShipRuntimeState();

        state.Player.PlayerShipState.OwnedShips ??=
            new List<ShipRuntimeData>();
    }

    private static void MigrateToSystemWarState(
        GameRuntimeState state)
    {
        if (state == null)
            return;

        state.Galaxy ??= new GalaxyRuntimeState();
        state.Galaxy.EnsureWarStateCollections();

        if (state.Galaxy.EnemyGroups != null)
        {
            foreach (EnemyGroupState groupState in state.Galaxy.EnemyGroups)
            {
                if (groupState == null)
                    continue;

                groupState.Level =
                    Mathf.Clamp(groupState.Level, 1, 10);

                if (!Enum.IsDefined(
                        typeof(EnemyGroupLifecycleState),
                        groupState.LifecycleState))
                {
                    groupState.LifecycleState =
                        EnemyGroupLifecycleState.None;
                }

                groupState.MemberRuntimeNpcIds ??=
                    new List<string>();

                groupState.MemberConfigIds ??=
                    new List<string>();
            }
        }

        if (state.Galaxy.Invasions != null)
        {
            foreach (InvasionState invasionState in state.Galaxy.Invasions)
            {
                if (invasionState == null)
                    continue;

                invasionState.Level =
                    Mathf.Clamp(invasionState.Level, 1, 10);

                if (!Enum.IsDefined(
                        typeof(InvasionLifecycleState),
                        invasionState.LifecycleState))
                {
                    invasionState.LifecycleState =
                        InvasionLifecycleState.None;
                }

                invasionState.EnemyGroupRuntimeIds ??=
                    new List<string>();

                invasionState.ApplyEscalation(
                    invasionState.FactionId,
                    invasionState.Level);
            }
        }

        if (state.Galaxy.EnemyFactions != null)
        {
            foreach (EnemyFactionState factionState in state.Galaxy.EnemyFactions)
            {
                if (factionState == null)
                    continue;

                factionState.OwnedSystemIds ??=
                    new List<string>();

                factionState.TerritorySystemIds ??=
                    new List<string>();

                factionState.FrontlineSystemIds ??=
                    new List<string>();

                factionState.FrontierSystemIds ??=
                    new List<string>();

                factionState.ActiveGroupRuntimeIds ??=
                    new List<string>();

                factionState.ActiveInvasionIds ??=
                    new List<string>();
            }
        }
    }

    private static void MigrateToShipDirection(
        GameRuntimeState state)
    {
        /*
         * Старые сохранения не содержали направления корабля.
         * Безопасное значение по умолчанию — вверх по карте системы.
         */
        if (IsInvalidDirection(
                state.Player.SystemMapShipDirection))
        {
            state.Player.SystemMapShipDirection =
                Vector3.up;
        }
    }

    private static void NormalizeShipDirection(
        GameRuntimeState state)
    {
        Vector3 direction =
            state.Player.SystemMapShipDirection;

        if (IsInvalidDirection(direction))
        {
            state.Player.SystemMapShipDirection =
                Vector3.up;

            return;
        }

        direction.z = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            state.Player.SystemMapShipDirection =
                Vector3.up;

            return;
        }

        state.Player.SystemMapShipDirection =
            direction.normalized;
    }

    private static void MigrateToIntegrityChecksum(
        GameRuntimeState state)
    {
        state.Meta.IntegrityChecksum ??= string.Empty;
    }

    private static void MigrateToSystemSecurity(
        GameRuntimeState state)
    {
        if (state.Galaxy == null || state.Galaxy.Systems == null)
            return;

        foreach (StarSystemRuntimeState systemState in state.Galaxy.Systems)
        {
            if (systemState == null)
                continue;

            if (!Enum.IsDefined(
                    typeof(StarSystemStatus),
                    systemState.SystemStatus))
            {
                systemState.SystemStatus = StarSystemStatus.Stable;
            }
        }
    }

    private static void MigrateToSystemNpcPersistentState(
        GameRuntimeState state)
    {
        if (state.SystemNpcSimulation == null)
            return;

        if (state.SystemNpcSimulation.Npcs != null)
        {
            foreach (SystemNpcSaveData npc in state.SystemNpcSimulation.Npcs)
            {
                if (npc == null)
                    continue;

                npc.Level = Mathf.Max(1, npc.Level);

                if (!Enum.IsDefined(typeof(AllyRole2A), npc.AllyRole))
                    npc.AllyRole = AllyRole2A.Ranger;

                npc.DestroyedAtTick = Mathf.Max(0, npc.DestroyedAtTick);
                npc.NextRespawnTick = Mathf.Max(0, npc.NextRespawnTick);

                if (npc.IsAlive)
                {
                    npc.DestroyedAtTick = 0;
                    npc.NextRespawnTick = 0;
                }
            }
        }

        if (state.SystemNpcSimulation.PopulationTimers == null)
            return;

        foreach (SystemPopulationRuleTimerState timer
                 in state.SystemNpcSimulation.PopulationTimers)
        {
            if (timer == null)
                continue;

            timer.TimerSeconds = Mathf.Max(0f, timer.TimerSeconds);
            timer.NextSpawnTick = Mathf.Max(0, timer.NextSpawnTick);
        }
    }

    private static bool IsInvalidDirection(
        Vector3 direction)
    {
        return !IsFinite(direction.x)
            || !IsFinite(direction.y)
            || !IsFinite(direction.z)
            || direction.sqrMagnitude < 0.0001f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value)
            && !float.IsInfinity(value);
    }
}