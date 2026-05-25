using System;
using System.Collections.Generic;

[Serializable]
public class MarketRuntimeData
{
    public string SystemId;
    public string PlanetId;
    public List<MarketItemEntry> Items = new();
}