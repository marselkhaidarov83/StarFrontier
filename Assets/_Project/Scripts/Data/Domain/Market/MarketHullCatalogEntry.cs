using System;
using UnityEngine;

[Serializable]
public class MarketHullCatalogEntry
{
    public HullConfig Hull;
    [Min(1)] public int NewPrice = 1000;
}
