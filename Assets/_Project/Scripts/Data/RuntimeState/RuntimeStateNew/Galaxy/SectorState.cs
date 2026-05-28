using System;
using System.Collections.Generic;

[Serializable]
public class SectorStateU
{
    public string Id;
    public string DisplayName;

    public bool IsUnlocked;
    public bool IsCompleted;

    public int SectorIndex;

    public List<string> SystemIds = new List<string>();
}