using System.Collections.Generic;
using UnityEngine;

public static class TurnRadiusRouteMath2A
{
    private const float DirectionThresholdSqrMagnitude = 0.0001f;
    private const float MinRadius = 0.01f;
    private const float PreviewLookAheadStepMultiplier = 4f;
    private const float PreviewLookAheadRadiusMultiplier = 0.4f;
    private const float PreviewMaxLookAheadDistance = 80f;

    public static Vector2 RotateTowardsByTravelDistance(
        Vector2 currentDirection,
        Vector2 targetDirection,
        float movementDistance,
        float turnRadius)
    {
        currentDirection = NormalizeDirectionOrUp(currentDirection);
        targetDirection = NormalizeDirectionOrFallback(
            targetDirection,
            currentDirection);

        if (turnRadius <= MinRadius)
            return targetDirection;

        float maxDegreesDelta =
            Mathf.Max(0f, movementDistance) /
            Mathf.Max(MinRadius, turnRadius) *
            Mathf.Rad2Deg;

        return RotateTowards(
            currentDirection,
            targetDirection,
            maxDegreesDelta);
    }

    public static Vector3 MoveWithTurnRadius(
        Vector3 currentPosition,
        Vector3 destinationPosition,
        ref Vector2 facingDirection,
        float movementDistance,
        float turnRadius,
        float arrivalDistanceThreshold,
        out bool arrived)
    {
        return MoveWithTurnRadius(
            currentPosition,
            destinationPosition,
            ref facingDirection,
            movementDistance,
            turnRadius,
            arrivalDistanceThreshold,
            true,
            out arrived);
    }

    public static Vector3 MoveWithTurnRadius(
        Vector3 currentPosition,
        Vector3 destinationPosition,
        ref Vector2 facingDirection,
        float movementDistance,
        float turnRadius,
        float arrivalDistanceThreshold,
        bool allowOvershootSnap,
        out bool arrived)
    {
        currentPosition.z = -2f;
        destinationPosition.z = -2f;

        Vector3 toDestination =
            destinationPosition - currentPosition;

        float distanceToDestination =
            toDestination.magnitude;

        if (distanceToDestination <= arrivalDistanceThreshold)
        {
            arrived = true;
            return destinationPosition;
        }

        Vector2 targetDirection =
            new Vector2(
                toDestination.x,
                toDestination.y);

        Vector2 newFacingDirection =
            RotateTowardsByTravelDistance(
                facingDirection,
                targetDirection,
                movementDistance,
                turnRadius);

        facingDirection = newFacingDirection;

        Vector3 moveDirection =
            new Vector3(
                newFacingDirection.x,
                newFacingDirection.y,
                0f);

        float safeMovementDistance =
            Mathf.Max(0f, movementDistance);

        if (allowOvershootSnap &&
            IsPointAhead(
                currentPosition,
                destinationPosition,
                moveDirection) &&
            safeMovementDistance >= distanceToDestination)
        {
            arrived = true;
            return destinationPosition;
        }

        Vector3 nextPosition =
            currentPosition +
            moveDirection.normalized * safeMovementDistance;

        if (allowOvershootSnap &&
            SegmentPassesNearPoint(
                currentPosition,
                nextPosition,
                destinationPosition,
                arrivalDistanceThreshold))
        {
            arrived = true;
            return destinationPosition;
        }

        arrived = false;
        return nextPosition;
    }

    public static void BuildWaypointPreviewPath(
        List<Vector3> result,
        IReadOnlyList<Vector3> waypoints,
        Vector2 startFacingDirection,
        float movementDistancePerStep,
        float turnRadius,
        float arrivalDistanceThreshold,
        int maxSteps)
    {
        TryBuildWaypointPreviewPath(
            result,
            waypoints,
            startFacingDirection,
            movementDistancePerStep,
            turnRadius,
            arrivalDistanceThreshold,
            maxSteps);
    }

    public static bool TryBuildWaypointPreviewPath(
        List<Vector3> result,
        IReadOnlyList<Vector3> waypoints,
        Vector2 startFacingDirection,
        float movementDistancePerStep,
        float turnRadius,
        float arrivalDistanceThreshold,
        int maxSteps,
        float intermediateWaypointArrivalDistanceThreshold = -1f)
    {
        if (result == null)
            return false;

        result.Clear();

        if (waypoints == null || waypoints.Count == 0)
            return false;

        Vector3 currentPosition = waypoints[0];
        currentPosition.z = -2f;
        result.Add(currentPosition);

        if (waypoints.Count == 1)
            return true;

        Vector2 facingDirection =
            NormalizeDirectionOrUp(startFacingDirection);

        float pathLength =
            GetPathLength(waypoints);

        if (pathLength <= arrivalDistanceThreshold)
        {
            result.Add(waypoints[waypoints.Count - 1]);
            return true;
        }

        float lookAheadDistance =
            GetPreviewLookAheadDistance(
                movementDistancePerStep,
                turnRadius,
                intermediateWaypointArrivalDistanceThreshold);

        float lastPathProgress =
            0f;

        int safeMaxSteps =
            Mathf.Max(1, maxSteps);

        for (int i = 0; i < safeMaxSteps; i++)
        {
            float pathProgress =
                Mathf.Max(
                    lastPathProgress,
                    GetClosestDistanceOnPath(
                        waypoints,
                        currentPosition));

            lastPathProgress =
                pathProgress;

            float targetDistance =
                Mathf.Min(
                    pathLength,
                    pathProgress + lookAheadDistance);

            bool isFinalTarget =
                targetDistance >= pathLength - DirectionThresholdSqrMagnitude;

            Vector3 targetPoint =
                GetPointOnPathAtDistance(
                    waypoints,
                    targetDistance);

            bool arrivedAtWaypoint;
            currentPosition =
                MoveWithTurnRadius(
                    currentPosition,
                    targetPoint,
                    ref facingDirection,
                    movementDistancePerStep,
                    turnRadius,
                    isFinalTarget
                        ? arrivalDistanceThreshold
                        : 0f,
                    isFinalTarget,
                    out arrivedAtWaypoint);

            result.Add(currentPosition);

            if (arrivedAtWaypoint)
                return true;

            float currentPathProgress =
                GetClosestDistanceOnPath(
                    waypoints,
                    currentPosition);

            if (currentPathProgress > lastPathProgress)
                lastPathProgress = currentPathProgress;

            if (pathLength - lastPathProgress <= arrivalDistanceThreshold &&
                Vector3.Distance(
                    currentPosition,
                    waypoints[waypoints.Count - 1]) <= arrivalDistanceThreshold)
            {
                result.Add(waypoints[waypoints.Count - 1]);
                return true;
            }
        }

        return false;
    }

    private static float GetPreviewLookAheadDistance(
        float movementDistancePerStep,
        float turnRadius,
        float intermediateWaypointArrivalDistanceThreshold)
    {
        float threshold =
            Mathf.Max(
                0f,
                intermediateWaypointArrivalDistanceThreshold);

        return Mathf.Clamp(
            Mathf.Max(
                movementDistancePerStep * PreviewLookAheadStepMultiplier,
                turnRadius * PreviewLookAheadRadiusMultiplier,
                threshold),
            Mathf.Max(0.01f, movementDistancePerStep),
            PreviewMaxLookAheadDistance);
    }

    private static float GetPathLength(
        IReadOnlyList<Vector3> path)
    {
        if (path == null ||
            path.Count <= 1)
        {
            return 0f;
        }

        float length =
            0f;

        for (int i = 1; i < path.Count; i++)
        {
            length +=
                Vector3.Distance(
                    path[i - 1],
                    path[i]);
        }

        return length;
    }

    private static float GetClosestDistanceOnPath(
        IReadOnlyList<Vector3> path,
        Vector3 point)
    {
        if (path == null ||
            path.Count <= 1)
        {
            return 0f;
        }

        Vector2 point2 =
            new Vector2(point.x, point.y);

        float bestDistanceSqr =
            float.MaxValue;

        float bestPathDistance =
            0f;

        float travelledDistance =
            0f;

        for (int i = 1; i < path.Count; i++)
        {
            Vector2 from =
                new Vector2(
                    path[i - 1].x,
                    path[i - 1].y);

            Vector2 to =
                new Vector2(
                    path[i].x,
                    path[i].y);

            Vector2 segment =
                to - from;

            float segmentLength =
                segment.magnitude;

            if (segmentLength <= DirectionThresholdSqrMagnitude)
                continue;

            float projection =
                Mathf.Clamp01(
                    Vector2.Dot(
                        point2 - from,
                        segment) /
                    segment.sqrMagnitude);

            Vector2 closestPoint =
                from + segment * projection;

            float distanceSqr =
                (point2 - closestPoint).sqrMagnitude;

            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr =
                    distanceSqr;

                bestPathDistance =
                    travelledDistance +
                    segmentLength * projection;
            }

            travelledDistance +=
                segmentLength;
        }

        return bestPathDistance;
    }

    private static Vector3 GetPointOnPathAtDistance(
        IReadOnlyList<Vector3> path,
        float distance)
    {
        if (path == null ||
            path.Count == 0)
        {
            return Vector3.zero;
        }

        if (path.Count == 1)
            return path[0];

        float remainingDistance =
            Mathf.Max(0f, distance);

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 from =
                path[i - 1];

            Vector3 to =
                path[i];

            float segmentDistance =
                Vector3.Distance(
                    from,
                    to);

            if (segmentDistance <= DirectionThresholdSqrMagnitude)
                continue;

            if (remainingDistance <= segmentDistance)
            {
                return Vector3.Lerp(
                    from,
                    to,
                    remainingDistance / segmentDistance);
            }

            remainingDistance -=
                segmentDistance;
        }

        return path[path.Count - 1];
    }

    private static bool HasPassedWaypoint(
        Vector3 currentPosition,
        Vector3 previousWaypoint,
        Vector3 waypoint,
        Vector3 nextWaypoint)
    {
        Vector2 current2 =
            new Vector2(
                currentPosition.x,
                currentPosition.y);

        Vector2 previousWaypoint2 =
            new Vector2(
                previousWaypoint.x,
                previousWaypoint.y);

        Vector2 waypoint2 =
            new Vector2(
                waypoint.x,
                waypoint.y);

        Vector2 nextWaypoint2 =
            new Vector2(
                nextWaypoint.x,
                nextWaypoint.y);

        Vector2 nextSegment =
            nextWaypoint2 - waypoint2;

        Vector2 fromWaypointToCurrent =
            current2 - waypoint2;

        Vector2 previousSegment =
            waypoint2 - previousWaypoint2;

        if (previousSegment.sqrMagnitude > DirectionThresholdSqrMagnitude &&
            Vector2.Dot(
                fromWaypointToCurrent,
                previousSegment.normalized) > 0f)
        {
            return true;
        }

        if (nextSegment.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            return false;

        return Vector2.Dot(
            fromWaypointToCurrent,
            nextSegment.normalized) > 0f;
    }

    public static Vector2 NormalizeDirectionOrUp(
        Vector2 direction)
    {
        return NormalizeDirectionOrFallback(
            direction,
            Vector2.up);
    }

    public static Vector2 NormalizeDirectionOrFallback(
        Vector2 direction,
        Vector2 fallback)
    {
        if (!IsFinite(direction) ||
            direction.sqrMagnitude < DirectionThresholdSqrMagnitude)
        {
            direction = fallback;
        }

        if (!IsFinite(direction) ||
            direction.sqrMagnitude < DirectionThresholdSqrMagnitude)
        {
            direction = Vector2.up;
        }

        return direction.normalized;
    }

    private static Vector2 RotateTowards(
        Vector2 currentDirection,
        Vector2 targetDirection,
        float maxDegreesDelta)
    {
        float currentAngle =
            -Vector2.SignedAngle(
                Vector2.up,
                currentDirection.normalized);

        float targetAngle =
            -Vector2.SignedAngle(
                Vector2.up,
                targetDirection.normalized);

        float newAngle =
            Mathf.MoveTowardsAngle(
                currentAngle,
                targetAngle,
                Mathf.Max(0f, maxDegreesDelta));

        float radians =
            newAngle * Mathf.Deg2Rad;

        return new Vector2(
            Mathf.Sin(radians),
            Mathf.Cos(radians)).normalized;
    }

    private static bool IsPointAhead(
        Vector3 currentPosition,
        Vector3 destinationPosition,
        Vector3 moveDirection)
    {
        Vector3 toDestination =
            destinationPosition - currentPosition;

        toDestination.z = 0f;
        moveDirection.z = 0f;

        if (toDestination.sqrMagnitude < DirectionThresholdSqrMagnitude ||
            moveDirection.sqrMagnitude < DirectionThresholdSqrMagnitude)
        {
            return false;
        }

        return Vector3.Dot(
            toDestination.normalized,
            moveDirection.normalized) > 0.999f;
    }

    private static bool SegmentPassesNearPoint(
        Vector3 from,
        Vector3 to,
        Vector3 point,
        float maxDistance)
    {
        if (maxDistance <= 0f)
            return false;

        Vector2 from2 =
            new Vector2(from.x, from.y);

        Vector2 to2 =
            new Vector2(to.x, to.y);

        Vector2 point2 =
            new Vector2(point.x, point.y);

        Vector2 segment =
            to2 - from2;

        if (segment.sqrMagnitude <= DirectionThresholdSqrMagnitude)
            return Vector2.Distance(from2, point2) <= maxDistance;

        float projection =
            Vector2.Dot(
                point2 - from2,
                segment) /
            segment.sqrMagnitude;

        if (projection < 0f ||
            projection > 1f)
        {
            return false;
        }

        Vector2 closestPoint =
            from2 + segment * projection;

        return Vector2.Distance(
            closestPoint,
            point2) <= maxDistance;
    }

    private static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }
}
