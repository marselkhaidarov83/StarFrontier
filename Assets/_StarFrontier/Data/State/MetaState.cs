using System;

[Serializable]
public sealed class MetaState
{
    public int saveVersion = 1;
    public string createdAtUtc;
    public string lastSavedAtUtc;
    public long totalTicks;
}