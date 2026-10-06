using System;

[Serializable]
public class StarSystemRuntimeState
{
    public string SystemId;

    public bool IsDiscovered;
    public bool IsVisited;

    public int DevelopmentLevel;
    public int DangerLevel;
    public int Stability;

    public SystemInfrastructureDamageState InfrastructureDamageState =
    SystemInfrastructureDamageState.Intact;

    public int InfrastructureDamage;

    public bool HasPendingRecoveryHook;
    public int RecoveryHookCreatedAtTick;
    public string RecoveryHookReason = string.Empty;

    public StarSystemStatus SystemStatus =
        StarSystemStatus.Stable;

    public void SetSystemStatus(StarSystemStatus newStatus)
    {
        SystemStatus = newStatus;
    }

    public void MarkThreat()
    {
        SetSystemStatus(StarSystemStatus.Threat);
    }

    public void MarkInvasion()
    {
        SetSystemStatus(StarSystemStatus.Invasion);
    }

    public void MarkCaptured()
    {
        SetSystemStatus(StarSystemStatus.Captured);

        HasPendingRecoveryHook = false;
        RecoveryHookCreatedAtTick = 0;
        RecoveryHookReason = string.Empty;

        Stability =
            Math.Max(
                0,
                Stability - 25);

        DevelopmentLevel =
            Math.Max(
                0,
                DevelopmentLevel - 1);

        ApplyInfrastructureDamageFromWar(
            35,
            false);
    }

    public void ApplyInfrastructureDamageFromWar(
    int damageAmount,
    bool forceDestroyed)
    {
        int normalizedDamage =
            Math.Max(
                0,
                damageAmount);

        InfrastructureDamage =
            Math.Min(
                100,
                InfrastructureDamage + normalizedDamage);

        if (forceDestroyed ||
            InfrastructureDamage >= 100)
        {
            InfrastructureDamage = 100;
            InfrastructureDamageState =
                SystemInfrastructureDamageState.Destroyed;

            return;
        }

        if (InfrastructureDamage > 0)
        {
            InfrastructureDamageState =
                SystemInfrastructureDamageState.Damaged;

            return;
        }

        InfrastructureDamageState =
            SystemInfrastructureDamageState.Intact;
    }

    public bool HasDamagedInfrastructure()
    {
        return InfrastructureDamageState == SystemInfrastructureDamageState.Damaged ||
               InfrastructureDamageState == SystemInfrastructureDamageState.Destroyed;
    }

    public bool HasDestroyedInfrastructure()
    {
        return InfrastructureDamageState == SystemInfrastructureDamageState.Destroyed;
    }

    public void MarkRecoveryReady()
    {
        SetSystemStatus(StarSystemStatus.RecoveryReady);
    }

    public bool IsUnderWarPressure()
    {
        return SystemStatus == StarSystemStatus.Threat ||
               SystemStatus == StarSystemStatus.Invasion ||
               SystemStatus == StarSystemStatus.Captured;
    }

    public bool IsRecoveryReady()
    {
        return SystemStatus == StarSystemStatus.RecoveryReady;
    }

    public bool IsSecured(int aliveEnemyGroupsCount)
    {
        return SystemStatus == StarSystemStatus.Stable &&
               aliveEnemyGroupsCount <= 0;
    }

    public void ApplyOfflineWarDegradation(
        int offlineTicks)
    {
        if (offlineTicks <= 0)
            return;

        if (SystemStatus == StarSystemStatus.Threat)
        {
            MarkInvasion();
            return;
        }

        if (SystemStatus == StarSystemStatus.Captured)
        {
            Stability =
                Math.Max(
                    0,
                    Stability - Math.Min(offlineTicks, 25));
        }
    }

    public void MarkLiberatedByPlayer()
    {
        SetSystemStatus(StarSystemStatus.Liberated);

        Stability =
            Math.Max(
                Stability,
                10);
    }

    public void MarkRecoveryHookPending(
    int currentTick,
    string reason)
    {
        HasPendingRecoveryHook = true;

        RecoveryHookCreatedAtTick =
            Math.Max(
                1,
                currentTick);

        RecoveryHookReason =
            string.IsNullOrWhiteSpace(reason)
                ? "player_liberation"
                : reason.Trim();
    }

    public void ClearRecoveryHook()
    {
        HasPendingRecoveryHook = false;
        RecoveryHookCreatedAtTick = 0;
        RecoveryHookReason = string.Empty;
    }

    public bool CanBeTargetedByInvasion()
    {
        return SystemStatus == StarSystemStatus.Stable ||
               SystemStatus == StarSystemStatus.RecoveryReady;
    }
}