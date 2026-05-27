using System;

[Serializable]
public class RouteState
{
    public string Id;

    public string FromSystemId;
    public string ToSystemId;

    public bool IsUnlocked;

    public int FuelCost;
}