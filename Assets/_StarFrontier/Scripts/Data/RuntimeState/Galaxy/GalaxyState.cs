using System;
using System.Collections.Generic;

[Serializable]
public class GalaxyState
{
    public bool IsGenerated;
    public string CurrentSectorId;
    public string CurrentSystemId;

    public List<SectorState> Sectors = new List<SectorState>();
    public List<StarSystemState> Systems = new List<StarSystemState>();
    public List<RouteState> Routes = new List<RouteState>();
}