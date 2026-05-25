using System;

[Serializable]
public sealed class SystemNpcWeaponRuntimeState
{
    public string WeaponConfigId;

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