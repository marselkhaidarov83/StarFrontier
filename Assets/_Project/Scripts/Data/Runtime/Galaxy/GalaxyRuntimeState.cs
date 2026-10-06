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

    public List<EnemyFactionState> EnemyFactions = new();
    public List<EnemyGroupState> EnemyGroups = new();
    public List<InvasionState> Invasions = new();

    public void EnsureWarStateCollections()
    {
        EnemyFactions ??= new List<EnemyFactionState>();
        EnemyGroups ??= new List<EnemyGroupState>();
        Invasions ??= new List<InvasionState>();
    }
}