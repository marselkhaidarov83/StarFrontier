using System;

[Serializable]
public class RouteState
{
    public string Id;

    public string FromSystemId;
    public string ToSystemId;

    public bool IsUnlocked;
    public bool IsDiscovered;

    public int FuelCost;
    public int Difficulty;
}
