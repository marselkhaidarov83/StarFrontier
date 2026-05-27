using UnityEngine;

[CreateAssetMenu(menuName = "StarFrontier/Galaxy/Route Config")]
public class RouteConfig : ScriptableObject
{
    public string Id;

    public string FromSystemId;
    public string ToSystemId;

    public bool UnlockedAtStart;

    public int FuelCost = 1;
}