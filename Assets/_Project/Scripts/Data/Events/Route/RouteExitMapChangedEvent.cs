using UnityEngine;

public class RouteExitMapChangedEvent
{
    public RouteConfig RouteConfig { get; }
    public string FromSystemId { get; }
    public string ToSystemId { get; }
    public Vector3 ExitPoint { get; }
    public Vector3 EntryPoint { get; }

    public RouteExitMapChangedEvent(
        RouteConfig routeConfig,
        string fromSystemId,
        string toSystemId,
        Vector3 exitPoint,
        Vector3 entryPoint)
    {
        RouteConfig = routeConfig;
        FromSystemId = fromSystemId;
        ToSystemId = toSystemId;
        ExitPoint = exitPoint;
        EntryPoint = entryPoint;
    }
}