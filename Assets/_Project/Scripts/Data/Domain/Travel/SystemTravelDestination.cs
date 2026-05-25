using System;
using UnityEngine;

[Serializable]
public class SystemTravelDestination
{
    public TravelDestinationType Type = TravelDestinationType.None;

    public string PlanetId;
    public PlanetConfig PlanetData;

    public string TargetSystemId;
    public StarSystemConfig TargetSystemConfig;
    public StarSystemLink SystemLink;

    public Vector2 FixedMapPosition;

    public bool HasDestination => Type != TravelDestinationType.None;

    public static SystemTravelDestination None()
    {
        return new SystemTravelDestination
        {
            Type = TravelDestinationType.None
        };
    }

    public static SystemTravelDestination Planet(PlanetConfig planetData)
    {
        return new SystemTravelDestination
        {
            Type = TravelDestinationType.Planet,
            PlanetData = planetData,
            PlanetId = planetData != null ? planetData.Id : string.Empty
        };
    }

    public static SystemTravelDestination MapPoint(Vector2 position)
    {
        return new SystemTravelDestination
        {
            Type = TravelDestinationType.MapPoint,
            FixedMapPosition = position
        };
    }

    public static SystemTravelDestination SystemExit(StarSystemLink link)
    {
        return new SystemTravelDestination
        {
            Type = TravelDestinationType.SystemExit,
            SystemLink = link,
            TargetSystemConfig = link != null ? link.LinkedSystem : null,
            TargetSystemId = link != null && link.LinkedSystem != null ? link.LinkedSystem.Id : string.Empty,
            FixedMapPosition = link != null ? link.ExitPoint : Vector2.zero
        };
    }
}