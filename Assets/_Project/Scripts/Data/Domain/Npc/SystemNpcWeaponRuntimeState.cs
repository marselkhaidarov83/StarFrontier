using System;

[Serializable]
public sealed class SystemNpcWeaponRuntimeState
{
    // Instance identity survives transfer into a space-loot object.
    public string InstanceId;
    public WeaponRuntimeStats RolledStats;
    public int DurabilityPercent = 100;
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
