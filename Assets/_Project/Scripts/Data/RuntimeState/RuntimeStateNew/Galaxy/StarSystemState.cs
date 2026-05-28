using System;

[Serializable]
public class StarSystemStateU
{
    public string Id;
    public string SectorId;
    public string DisplayName;

    public float MapX;
    public float MapY;

    public bool IsDiscovered;
    public bool IsVisited;
    public bool IsCurrent;

    public StarSystemDangerLevel DangerLevel;
    public StarSystemContentType ContentType;
}