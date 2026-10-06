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
        return SystemStatus == StarSystemStatus.Stable
               && aliveEnemyGroupsCount <= 0;
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
}