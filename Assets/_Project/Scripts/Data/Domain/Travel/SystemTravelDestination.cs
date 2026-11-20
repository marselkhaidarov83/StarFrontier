using System;
using UnityEngine;

[Serializable]
public class SystemTravelDestination
{
    public TravelDestinationType Type = TravelDestinationType.None;

    public string PlanetId;
    public PlanetConfig PlanetData;

    public string StationId;
    public StationConfig StationData;

    public string RuntimeNpcId;

    public string TargetSystemId;
    public StarSystemConfig TargetSystemConfig;

    // Маршрут и его точки для текущего направления перелёта.
    public RouteConfig RouteConfig;
    public string FromSystemId;
    public string ToSystemId;
    public Vector3 ExitPoint;
    public Vector3 EntryPoint;

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

    public static SystemTravelDestination Station(StationConfig stationData)
    {
        return new SystemTravelDestination
        {
            Type = TravelDestinationType.Station,
            StationData = stationData,
            StationId = stationData != null ? stationData.Id : string.Empty,
            FixedMapPosition = stationData != null
                ? stationData.LocalOffset
                : Vector2.zero
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

    public static SystemTravelDestination Npc(
        string runtimeNpcId,
        Vector3 currentPosition)
    {
        return new SystemTravelDestination
        {
            Type = TravelDestinationType.Npc,
            RuntimeNpcId = runtimeNpcId ?? string.Empty,
            FixedMapPosition = currentPosition
        };
    }

    public static SystemTravelDestination SystemExit(RouteExitMapChangedEvent evt)
    {
        if (evt?.RouteConfig == null || !evt.RouteConfig.ConnectsSystems(evt.FromSystemId, evt.ToSystemId))
            return None();
        return new SystemTravelDestination
        {
            Type = TravelDestinationType.SystemExit,

            RouteConfig = evt.RouteConfig,
            FromSystemId = evt.FromSystemId,
            ToSystemId = evt.ToSystemId,

            TargetSystemConfig = evt.RouteConfig != null
                ? evt.RouteConfig.GetOtherSystem(evt.FromSystemId)
                : null,

            TargetSystemId = evt.ToSystemId,

            ExitPoint = evt.RouteConfig.GetExitPoint(evt.FromSystemId),
            EntryPoint = evt.RouteConfig.GetEntryPoint(evt.ToSystemId),

            FixedMapPosition = evt.RouteConfig.GetExitPoint(evt.FromSystemId)
        };
    }
}
