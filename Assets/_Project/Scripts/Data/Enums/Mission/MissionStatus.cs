using System;

[Serializable]
public enum MissionStatus
{
    Available = 0,
    Accepted = 1,
    InProgress = 2,
    ReadyToComplete = 3,
    Completed = 4,
    Failed = 5,
    Expired = 6,
    Cancelled = 7
}
