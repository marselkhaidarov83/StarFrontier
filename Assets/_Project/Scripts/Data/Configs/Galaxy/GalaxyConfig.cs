using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "StarFrontier/Galaxy/Galaxy Config")]
public class GalaxyConfig : ScriptableObject
{
    public string Id = "galaxy_default";

    public string StartSystemId = "system_01_01";

    public List<SectorConfig> Sectors = new();
    public List<RouteConfig> Routes = new();
}