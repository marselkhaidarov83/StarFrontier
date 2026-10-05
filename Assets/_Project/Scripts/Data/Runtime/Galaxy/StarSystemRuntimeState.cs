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

    public bool IsSecured(int aliveEnemyGroupsCount)
    {
        return SystemStatus == StarSystemStatus.Stable
               && aliveEnemyGroupsCount <= 0;
    }
}