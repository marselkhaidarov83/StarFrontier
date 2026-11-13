using System;
using UnityEngine;

public enum PlanetServiceKind2A { Refuel, Repair, Market }
[Serializable]
public sealed class PlanetServiceVisualSet2A
{
    public PlanetType Type;
    public Sprite Refuel, Repair, Market;
}

[Serializable]
public sealed class StationServiceVisualSet2A
{
    public StationType Type;
    public Sprite Refuel, Repair, Market;
}

[CreateAssetMenu(menuName = "StarFrontier/Configs/Planet/Service Visual Catalog")]
public sealed class PlanetServiceVisualCatalog2A : ScriptableObject
{
    public PlanetServiceVisualSet2A[] Planets = Array.Empty<PlanetServiceVisualSet2A>();
    public StationServiceVisualSet2A[] Stations = Array.Empty<StationServiceVisualSet2A>();

    public static StationConfig GetDockedStation(PlayerState player, IConfigService config)
    {
        if (string.IsNullOrEmpty(player?.CurrentStationId)) return null;
        var station = config.GetStarSystemConfigById(player.CurrentSystemId)?.Station;
        return station != null && station.Id == player.CurrentStationId ? station : null;
    }

    public Sprite Get(PlanetConfig planet, PlanetServiceKind2A service, Sprite fallback, StationConfig station = null)
    {
        if (station != null)
        {
            if (station.StationTypeConfig == null) return fallback;
            foreach (var entry in Stations)
            {
                if (entry == null || entry.Type != station.StationType) continue;
                return Select(service, entry.Refuel, entry.Repair, entry.Market, fallback);
            }
            return fallback;
        }

        if (planet == null) return fallback;
        if (service == PlanetServiceKind2A.Market && planet.MarketIllustration != null) return planet.MarketIllustration;
        foreach (var entry in Planets)
        {
            if (entry == null || entry.Type != planet.PlanetType) continue;
            return Select(service, entry.Refuel, entry.Repair, entry.Market, fallback);
        }
        return fallback;
    }

    private static Sprite Select(PlanetServiceKind2A service, Sprite refuel, Sprite repair, Sprite market, Sprite fallback)
    {
        Sprite sprite;
        switch (service)
        {
            case PlanetServiceKind2A.Refuel: sprite = refuel; break;
            case PlanetServiceKind2A.Repair: sprite = repair; break;
            case PlanetServiceKind2A.Market: sprite = market; break;
            default: return fallback;
        }
        return sprite != null ? sprite : fallback;
    }
}
