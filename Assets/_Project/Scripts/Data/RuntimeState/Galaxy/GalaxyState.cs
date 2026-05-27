using System;
using System.Collections.Generic;

[Serializable]
public class GalaxyState
{
    public string CurrentSystemId;

    public List<SectorState> Sectors = new();
    public List<StarSystemState> Systems = new();
    public List<RouteState> Routes = new();
}