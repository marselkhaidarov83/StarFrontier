using System;
using UnityEngine;

public enum SpaceLootKind
{
    Item,
    Equipment,
    Credits,
    Diamonds,
    Astralite
}

[Serializable]
public sealed class SpaceLootRuntimeState
{
    public string LootId;
    public string SourceNpcId;
    public SystemNpcType SourceNpcType;
    public string SourceNpcConfigId;
    public string SourceBatchId;
    public string SystemId;
    public Vector3 Position;
    public Vector3 EjectionTargetPosition;
    public float EjectionRemainingSeconds;
    public SpaceLootKind Kind;
    public string ItemConfigId;
    public string EquipmentConfigId;
    public SystemNpcWeaponRuntimeState Equipment;
    public int Quantity;
    public int CurrentHealth;
    public EnemyFaction2A AstraliteSpecies;
    public int CreatedAtTick;
    public string PullCollectorId;

    public SpaceLootRuntimeState Copy()
    {
        return new SpaceLootRuntimeState
        {
            LootId = LootId,
            SourceNpcId = SourceNpcId,
            SourceNpcType = SourceNpcType,
            SourceNpcConfigId = SourceNpcConfigId,
            SourceBatchId = SourceBatchId,
            SystemId = SystemId,
            Position = Position,
            EjectionTargetPosition = EjectionTargetPosition,
            EjectionRemainingSeconds = EjectionRemainingSeconds,
            Kind = Kind,
            ItemConfigId = ItemConfigId,
            EquipmentConfigId = EquipmentConfigId,
            Equipment = Equipment == null ? null : new SystemNpcWeaponRuntimeState
            {
                InstanceId = Equipment.InstanceId,
                RolledStats = Equipment.RolledStats,
                DurabilityPercent = Equipment.DurabilityPercent,
                SlotIndex = Equipment.SlotIndex,
                WeaponConfigId = Equipment.WeaponConfigId,
                HasLootOrigin = Equipment.HasLootOrigin,
                LootOriginNpcType = Equipment.LootOriginNpcType,
                LootOriginNpcConfigId = Equipment.LootOriginNpcConfigId,
                LastShotTick = Equipment.LastShotTick,
                NextAllowedShotTick = Equipment.NextAllowedShotTick,
                CooldownRemainingSeconds = Equipment.CooldownRemainingSeconds,
                ShotDistance = Equipment.ShotDistance
            },
            Quantity = Quantity,
            CurrentHealth = CurrentHealth,
            AstraliteSpecies = AstraliteSpecies,
            CreatedAtTick = CreatedAtTick,
            PullCollectorId = PullCollectorId
        };
    }
}
