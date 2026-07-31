using System;

[Serializable]
public sealed class MetaStateU
{
    public int saveVersion = 1;
    public string createdAtUtc;
    public string lastSavedAtUtc;
    public long totalTicks;
}