using System;
using UnityEngine;

public enum PlanetServiceKind2A { Refuel, Repair, Market }
[Serializable]
public sealed class PlanetServiceVisualSet2A
{
    public PlanetType Type;
    public Sprite Repair, Market;
}

[Serializable]
public sealed class StationServiceVisualSet2A
{
    public StationType Type;
    public Sprite Repair;
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
        if (service == PlanetServiceKind2A.Refuel) return GetRefuelIllustration(planet, station) ?? fallback;

        if (station != null)
        {
            if (service == PlanetServiceKind2A.Market) return station.MarketIllustration ?? fallback;
            if (station.StationTypeConfig == null) return fallback;
            foreach (var entry in Stations)
            {
                if (entry == null || entry.Type != station.StationType) continue;
                return Select(service, entry.Repair, null, fallback);
            }
            return fallback;
        }

        if (planet == null) return fallback;
        var illustration = planet.PlanetTypeConfig?.MarketIllustration;
        if (service == PlanetServiceKind2A.Market && illustration != null) return illustration;
        foreach (var entry in Planets)
        {
            if (entry == null || entry.Type != planet.PlanetType) continue;
            return Select(service, entry.Repair, entry.Market, fallback);
        }
        return fallback;
    }

    public static Sprite GetRefuelIllustration(PlanetConfig planet, StationConfig station)
    {
        return station != null
            ? station.StationTypeConfig?.RefuelIllustration
            : planet?.PlanetTypeConfig?.RefuelIllustration;
    }

    private static Sprite Select(PlanetServiceKind2A service, Sprite repair, Sprite market, Sprite fallback)
    {
        Sprite sprite;
        switch (service)
        {
            case PlanetServiceKind2A.Repair: sprite = repair; break;
            case PlanetServiceKind2A.Market: sprite = market; break;
            default: return fallback;
        }
        return sprite != null ? sprite : fallback;
    }
}
