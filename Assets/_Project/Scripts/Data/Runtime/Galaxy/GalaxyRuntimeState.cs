using System;
using System.Collections.Generic;

[Serializable]
public class GalaxyRuntimeState
{
    public int GalaxyDay;
    public string CurrentSystemId;

    public List<SectorRuntimeState> Sectors = new();
    public List<StarSystemRuntimeState> Systems = new();
    public List<RouteRuntimeState> Routes = new();
}