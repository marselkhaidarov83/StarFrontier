using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GalaxyConfig", menuName = "StarFrontier/Configs/Galaxy")]
public class GalaxyConfig : BaseConfig
{
    public List<SectorConfig> Sectors = new();
}