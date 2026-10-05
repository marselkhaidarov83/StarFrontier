using NUnit.Framework;
using UnityEngine;

public sealed class SystemNpcPersistentSaveEditModeTests
{
    [Test]
    public void SimulationSave_JsonRoundTrip_PreservesNpcAndRespawnData()
    {
        var source = new SystemNpcSimulationSaveData();

        source.Npcs.Add(new SystemNpcSaveData
        {
            RuntimeNpcId = "npc_enemy_01",
            NpcType = SystemNpcType.Enemy,
            ConfigId = "enemy_raider_04",
            SpawnRuleId = "rule_guard_01",
            GroupRuntimeId = "group_guard_01",
            Level = 4,
            CurrentSystemId = "system_heliosGate_01",
            CurrentHull = 0,
            CurrentShield = 0,
            CurrentEnergy = 12,
            LifeState = SystemNpcLifeState.Destroyed,
            IsAlive = false,
            DestroyedAtTick = 120,
            NextRespawnTick = 180
        });

        source.Npcs.Add(new SystemNpcSaveData
        {
            RuntimeNpcId = "npc_ally_01",
            NpcType = SystemNpcType.Ally,
            AllyRole = AllyRole2A.Medic,
            ConfigId = "ally_medic_03",
            SpawnRuleId = "rule_allies_01",
            Level = 3,
            CurrentSystemId = "system_heliosGate_01",
            CurrentHull = 80,
            CurrentShield = 25,
            CurrentEnergy = 60,
            LifeState = SystemNpcLifeState.Alive,
            IsAlive = true
        });

        source.PopulationTimers.Add(
            new SystemPopulationRuleTimerState(
                "system_heliosGate_01",
                "rule_guard_01")
            {
                TimerSeconds = 0f,
                NextSpawnTick = 180
            });

        string json = JsonUtility.ToJson(source);
        SystemNpcSimulationSaveData restored =
            JsonUtility.FromJson<SystemNpcSimulationSaveData>(json);

        SystemNpcSaveData enemy = restored.Npcs[0];
        SystemNpcSaveData ally = restored.Npcs[1];

        Assert.That(enemy.NpcType, Is.EqualTo(SystemNpcType.Enemy));
        Assert.That(enemy.Level, Is.EqualTo(4));
        Assert.That(enemy.CurrentHull, Is.Zero);
        Assert.That(enemy.LifeState, Is.EqualTo(SystemNpcLifeState.Destroyed));
        Assert.That(enemy.CurrentSystemId, Is.EqualTo("system_heliosGate_01"));
        Assert.That(enemy.GroupRuntimeId, Is.EqualTo("group_guard_01"));
        Assert.That(enemy.DestroyedAtTick, Is.EqualTo(120));
        Assert.That(enemy.NextRespawnTick, Is.EqualTo(180));

        Assert.That(ally.NpcType, Is.EqualTo(SystemNpcType.Ally));
        Assert.That(ally.AllyRole, Is.EqualTo(AllyRole2A.Medic));
        Assert.That(ally.Level, Is.EqualTo(3));
        Assert.That(ally.CurrentHull, Is.EqualTo(80));
        Assert.That(ally.IsAlive, Is.True);

        Assert.That(restored.PopulationTimers[0].NextSpawnTick,
            Is.EqualTo(180));
    }

    [Test]
    public void Migrate_VersionFourSave_NormalizesNpcPersistentFields()
    {
        var state = new GameRuntimeState();
        state.Meta.SaveDataVersion = SaveDataVersions.SystemSecurity;
        state.SystemNpcSimulation.Npcs.Add(new SystemNpcSaveData
        {
            RuntimeNpcId = "npc_legacy_01",
            NpcType = SystemNpcType.Ally,
            Level = 0,
            IsAlive = true,
            DestroyedAtTick = 10,
            NextRespawnTick = 20
        });

        bool changed = SaveMigrationService.Migrate(state);
        SystemNpcSaveData npc = state.SystemNpcSimulation.Npcs[0];

        Assert.That(changed, Is.True);
        Assert.That(state.Meta.SaveDataVersion,
            Is.EqualTo(SaveDataVersions.Current));
        Assert.That(npc.Level, Is.EqualTo(1));
        Assert.That(npc.DestroyedAtTick, Is.Zero);
        Assert.That(npc.NextRespawnTick, Is.Zero);
    }
}