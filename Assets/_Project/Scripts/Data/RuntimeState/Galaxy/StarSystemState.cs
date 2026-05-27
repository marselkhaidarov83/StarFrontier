using System;

[Serializable]
public class StarSystemState
{
    public string Id;
    public string SectorId;

    public bool IsDiscovered;
    public bool IsUnlocked;
    public bool IsCaptured;

    public int ThreatLevel;
}