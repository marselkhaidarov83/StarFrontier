using System;
using UnityEngine;

[Serializable] public sealed class MarketPlanetVisual2A { public PlanetType Type; public Sprite Illustration; }
[CreateAssetMenu(menuName = "StarFrontier/Configs/Market/Visual Catalog")]
public sealed class MarketVisualCatalog2A : ScriptableObject
{
    public GovernmentMissionVisualCatalog BasePlanetIllustrations;
    public MarketPlanetVisual2A[] Planets = Array.Empty<MarketPlanetVisual2A>();
    public Sprite EquipmentIcon;
    public Sprite MissileIcon;
    public Sprite Get(PlanetConfig planet, StationConfig station)
    {
        if (station != null) return station.StationTypeConfig?.MarketIllustration ?? station.StationTypeConfig?.MissionIllustration;
        if (planet == null) return null;
        if (planet.MarketIllustration != null) return planet.MarketIllustration;
        foreach (var entry in Planets)
            if (entry != null && entry.Type == planet.PlanetType && entry.Illustration != null) return entry.Illustration;
        return BasePlanetIllustrations != null ? BasePlanetIllustrations.Get(planet.PlanetType, null) : planet.BackgroundSprite;
    }
}
