using System.Collections.Generic;
using UnityEngine;

public interface ISystemShipRouteService2A
{
    bool TryBuildRoute(
        SystemShipRouteRequest2A request,
        SystemShipRouteResult2A result);

    bool TryBuildPreview(
        SystemShipRouteRequest2A request,
        TravelRoutePreview2A preview,
        float smallDotSpacing,
        int maxBigDots,
        int maxSmallDots,
        float secondsPerTick,
        float distanceTravelled = 0f);

    bool FillPreviewFromPath(
        IReadOnlyList<Vector3> path,
        float speed,
        TravelRoutePreview2A preview,
        float smallDotSpacing,
        int maxBigDots,
        int maxSmallDots,
        float secondsPerTick,
        float distanceTravelled);

    float GetPathLength(
        IReadOnlyList<Vector3> path);

    Vector3 GetPointOnPathAtDistance(
        IReadOnlyList<Vector3> path,
        float distance);

    Vector2 GetDirectionOnPathAtDistance(
        IReadOnlyList<Vector3> path,
        float distance);
}