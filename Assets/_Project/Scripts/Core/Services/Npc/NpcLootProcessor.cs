using System;
using System.Collections.Generic;
using UnityEngine;

// Resolves ship-owned and generated drops at a damage threshold or destruction.
// The ship owns the trigger flags; space loot never has health thresholds.
public static class NpcLootProcessor
{
    public static void ProcessDamage(
        SystemNpcRuntimeState npc,
        int hullBefore,
        int quantTick,
        IConfigService configs,
        SystemSpaceLootService spaceLoot)
    {
        if (npc == null || configs == null || spaceLoot == null)
            return;

        NpcLootTableConfig table = null;
        EnemyFaction2A species = EnemyFaction2A.AI;
        if (npc.IsAlly)
            table = configs.GetAllyConfigById(npc.ConfigId)?.LootTable;
        else if (npc.IsEnemy)
        {
            EnemyConfig config = configs.GetEnemyConfigById(npc.ConfigId);
            table = config?.LootTable;
            if (config != null)
                species = config.LootSpecies;
        }

        if (table == null || table.Rules == null)
            return;

        npc.ProcessedLootRuleIndexes ??= new List<int>();
        bool destroyed = !npc.IsAlive || npc.CurrentHull <= 0;
        for (int index = 0; index < table.Rules.Length; index++)
        {
            NpcLootTriggerRule rule = table.Rules[index];
            if (rule == null)
                continue;

            if (rule.IsDeath)
            {
                if (!destroyed || npc.DeathLootProcessed)
                    continue;
            }
            else
            {
                if (npc.ProcessedLootRuleIndexes.Contains(index))
                    continue;

                if (!CrossedThreshold(hullBefore, npc.CurrentHull,
                        npc.MaxHull, rule.HullThresholdPercent))
                    continue;

                npc.ProcessedLootRuleIndexes.Add(index);
            }

            AppLog.Info(DebugLogChannel.Loot,
                "[LOOT_TRIGGER] Npc=" + npc.RuntimeNpcId +
                " | Rule=" + index + " | Death=" + rule.IsDeath +
                " | Hull=" + npc.CurrentHull + "/" + npc.MaxHull);
            DropCargo(npc, rule, quantTick, spaceLoot);
            DropEquipment(npc, rule, quantTick, spaceLoot);
            Generate(npc, rule, table, species, quantTick, spaceLoot);
        }

        if (destroyed)
            npc.DeathLootProcessed = true;
    }

    private static void DropCargo(SystemNpcRuntimeState npc, NpcLootTriggerRule rule,
        int tick, SystemSpaceLootService spaceLoot)
    {
        if (npc.CargoBatches == null || rule.CargoBatches == null)
            return;

        foreach (NpcLootCargoChance chance in rule.CargoBatches)
        {
            if (chance == null || !Roll(chance.ChancePercent))
                continue;

            NpcCargoBatchRuntimeState batch = npc.CargoBatches.Find(
                entry => entry != null && entry.BatchNumber == chance.BatchNumber);
            if (batch == null || batch.Quantity <= 0)
                continue;

            SpaceLootRuntimeState drop = NewDrop(npc, SpaceLootKind.Item,
                batch.ItemConfigId, null, batch.Quantity, tick);
            drop.SourceBatchId = batch.BatchId;
            spaceLoot.Add(drop);
            npc.CargoBatches.Remove(batch);
        }
    }

    private static void DropEquipment(SystemNpcRuntimeState npc, NpcLootTriggerRule rule,
        int tick, SystemSpaceLootService spaceLoot)
    {
        if (npc.Weapons == null || rule.Equipment == null)
            return;

        foreach (NpcLootEquipmentChance chance in rule.Equipment)
        {
            if (chance == null || chance.SlotIndex < 0 || !Roll(chance.ChancePercent))
                continue;

            SystemNpcWeaponRuntimeState weapon = npc.Weapons.Find(
                entry => entry != null && entry.SlotIndex == chance.SlotIndex);
            if (weapon == null || !npc.Weapons.Remove(weapon))
                continue;

            SpaceLootRuntimeState drop = NewDrop(npc, SpaceLootKind.Equipment,
                null, weapon.WeaponConfigId, 1, tick);
            drop.Equipment = weapon;
            spaceLoot.Add(drop);
        }
    }

    private static void Generate(SystemNpcRuntimeState npc, NpcLootTriggerRule rule,
        NpcLootTableConfig table, EnemyFaction2A species, int tick,
        SystemSpaceLootService spaceLoot)
    {
        if (rule.Generated == null)
            return;

        foreach (NpcLootGeneratedEntry entry in rule.Generated)
        {
            if (entry == null || !Roll(entry.ChancePercent))
                continue;

            int amount = UnityEngine.Random.Range(
                Mathf.Max(0, entry.MinAmount),
                Mathf.Max(entry.MinAmount, entry.MaxAmount) + 1);
            if (amount <= 0)
                continue;

            if (entry.Kind == NpcLootGeneratedKind.Item && entry.Item != null)
                spaceLoot.Add(NewDrop(npc, SpaceLootKind.Item, entry.Item.Id, null, amount, tick));
            else if (entry.Kind == NpcLootGeneratedKind.Credits)
                spaceLoot.Add(NewDrop(npc, SpaceLootKind.Credits, null, null, amount, tick));
            else if (entry.Kind == NpcLootGeneratedKind.Diamonds)
                spaceLoot.Add(NewDrop(npc, SpaceLootKind.Diamonds, null, null, amount, tick));
            else if (entry.Kind == NpcLootGeneratedKind.Astralite && npc.IsEnemy &&
                table.AstraliteCatalog != null)
            {
                ItemConfig item = table.AstraliteCatalog.GetItem(species);
                if (item != null)
                {
                    SpaceLootRuntimeState drop = NewDrop(npc, SpaceLootKind.Astralite,
                        item.Id, null, amount, tick);
                    drop.AstraliteSpecies = species;
                    spaceLoot.Add(drop);
                }
            }
            else if (entry.Kind == NpcLootGeneratedKind.Equipment &&
                entry.EquipmentConfig != null)
            {
                for (int i = 0; i < amount; i++)
                {
                    WeaponConfig config = entry.EquipmentConfig;
                    WeaponRuntimeStats stats = config.RollRuntimeStats();
                    var weapon = new SystemNpcWeaponRuntimeState
                    {
                        InstanceId = Guid.NewGuid().ToString("N"),
                        WeaponConfigId = config.Id,
                        RolledStats = stats,
                        DurabilityPercent = 100,
                        ShotDistance = stats.Range
                    };
                    SpaceLootRuntimeState drop = NewDrop(npc, SpaceLootKind.Equipment,
                        null, config.Id, 1, tick);
                    drop.Equipment = weapon;
                    spaceLoot.Add(drop);
                }
            }
        }
    }

    private static SpaceLootRuntimeState NewDrop(SystemNpcRuntimeState npc,
        SpaceLootKind kind, string itemId, string equipmentId, int quantity, int tick)
    {
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float distance = Bootstrapper.Instance != null
            ? Bootstrapper.Instance.SpaceLootEjectionDistance : 100f;
        return new SpaceLootRuntimeState
        {
            LootId = Guid.NewGuid().ToString("N"),
            SourceNpcId = npc.RuntimeNpcId,
            SourceNpcType = npc.NpcType,
            SourceNpcConfigId = npc.ConfigId,
            SystemId = npc.CurrentSystemId,
            Position = npc.CurrentPosition,
            EjectionTargetPosition = ComputeEjectionPosition(npc.CurrentPosition, distance, angle),
            EjectionRemainingSeconds = 0.6f,
            Kind = kind,
            ItemConfigId = itemId,
            EquipmentConfigId = equipmentId,
            Quantity = quantity,
            CreatedAtTick = tick
        };
    }

    public static Vector3 ComputeEjectionPosition(Vector3 origin,
        float radius, float angleRadians)
    {
        float distance = Mathf.Max(0f, radius);
        return origin + new Vector3(Mathf.Cos(angleRadians) * distance,
            Mathf.Sin(angleRadians) * distance, 0f);
    }

    public static bool CrossedThreshold(int before, int after,
        int maximum, int percent)
    {
        if (maximum <= 0 || percent <= 0 || percent >= 100)
            return false;
        // A new flight that starts below a threshold does not create a drop.
        return (long)before * 100 > (long)maximum * percent &&
               (long)after * 100 <= (long)maximum * percent;
    }

    private static bool Roll(float chancePercent)
    {
        return chancePercent > 0f &&
            (chancePercent >= 100f || UnityEngine.Random.Range(0f, 100f) < chancePercent);
    }
}
