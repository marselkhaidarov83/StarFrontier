using System;
using UnityEngine;

[Serializable] public sealed class MarketPlanetVisual2A { public PlanetType Type; public Sprite Illustration; }
[CreateAssetMenu(menuName = "StarFrontier/Configs/Market/Visual Catalog")]
public sealed class MarketVisualCatalog2A : ScriptableObject
{
    public MarketPlanetVisual2A[] Planets = Array.Empty<MarketPlanetVisual2A>();
    public Sprite EquipmentIcon;
    public Sprite MissileIcon;
    public Sprite Get(PlanetConfig planet, StationConfig station)
    {
        if (station != null) return station.MarketIllustration ?? station.MissionIllustration;
        if (planet == null) return null;
        var illustration = planet.PlanetTypeConfig?.MarketIllustration;
        if (illustration != null) return illustration;
        foreach (var entry in Planets)
            if (entry != null && entry.Type == planet.PlanetType && entry.Illustration != null) return entry.Illustration;
        return planet.PlanetTypeConfig?.MissionIllustration;
    }
}
