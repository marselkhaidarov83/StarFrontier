using System;

[Serializable]
public enum MissionStatus
{
    Available,
    Accepted,
    InProgress,
    ReadyToComplete,
    Completed,
    Failed,
    Expired
}