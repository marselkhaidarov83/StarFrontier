using System;
using UnityEngine;

[Serializable]
public sealed class SystemNpcWeaponRuntimeState
{
    // Instance identity survives transfer into a space-loot object.
    public string InstanceId;
    public WeaponRuntimeStats RolledStats;
    public int CurrentDurability;
    public int MaximumDurability;
    [SerializeField, HideInInspector] private int DurabilityPercent = -1;
    // The same cargo, trade and loot record carries typed ship components.
    public string ComponentConfigId;
    public int StoredFuel;
    public bool IsComponent => !string.IsNullOrEmpty(ComponentConfigId);
    public string EquipmentConfigId => IsComponent ? ComponentConfigId : WeaponConfigId;

    public void MigrateDurability()
    {
        if (MaximumDurability != 0 || IsComponent) return;
        MaximumDurability = 100;
        CurrentDurability = DurabilityPercent == -1 ? 100 : DurabilityPercent;
        DurabilityPercent = -1;
    }

    public static SystemNpcWeaponRuntimeState NewComponent(ShipComponentConfig config, string instanceId, int slot = -1) =>
        new SystemNpcWeaponRuntimeState
        {
            InstanceId = instanceId, ComponentConfigId = config.Id, SlotIndex = slot,
            CurrentDurability = config.MaximumDurability, MaximumDurability = config.MaximumDurability,
            RolledStats = new WeaponRuntimeStats { CargoSize = config.CargoSize }
        };
    public int SlotIndex = -1;
    public string WeaponConfigId;
    // Preserve the first loot source through cargo, saves, and repeated ejection.
    public bool HasLootOrigin;
    public SystemNpcType LootOriginNpcType;
    public string LootOriginNpcConfigId;

    public int LastShotTick = -1;
    public int NextAllowedShotTick = 0;

    public float CooldownRemainingSeconds;
    public float ShotDistance;

    public bool CanShootAtTick(int currentTick)
    {
        return LastShotTick != currentTick && currentTick >= NextAllowedShotTick;
    }

    public void MarkShotAtTick(int currentTick, int cooldownTicks)
    {
        LastShotTick = currentTick;
        NextAllowedShotTick = currentTick + Math.Max(1, cooldownTicks);
    }

    public bool CanShootOnTick(int currentTick)
    {
        return CanShootAtTick(currentTick);
    }

    public void MarkShot(int currentTick, float cooldownSeconds)
    {
        LastShotTick = currentTick;
        CooldownRemainingSeconds = cooldownSeconds;
    }

    public void TickCooldown(float deltaTime)
    {
        if (CooldownRemainingSeconds <= 0f)
            return;

        CooldownRemainingSeconds -= deltaTime;

        if (CooldownRemainingSeconds < 0f)
            CooldownRemainingSeconds = 0f;
    }
}
