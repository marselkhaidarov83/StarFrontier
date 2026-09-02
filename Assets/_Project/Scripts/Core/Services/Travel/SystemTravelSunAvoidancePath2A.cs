using System.Collections.Generic;
using UnityEngine;

public static class SystemTravelSunAvoidancePath2A
{
    private const float Epsilon = 0.001f;
    private const float PushOutsideOffset = 8f;
    private const float HeadingPenaltyMultiplier = 3f;

    private struct TangentOption
    {
        public Vector2 StartTangent;
        public Vector2 EndTangent;
        public bool Clockwise;
        public float TotalLength;
    }

    public static void BuildPath(
        List<Vector3> result,
        Vector3 start,
        Vector3 destination,
        Vector3 sunCenter,
        float avoidanceRadius,
        int arcSegments
    )
    {
        BuildPath(
            result,
            start,
            destination,
            sunCenter,
            avoidanceRadius,
            arcSegments,
            false
        );
    }

    public static void BuildPath(
        List<Vector3> result,
        Vector3 start,
        Vector3 destination,
        Vector3 sunCenter,
        float avoidanceRadius,
        int arcSegments,
        bool forceAvoidance
    )
    {
        BuildPath(
            result,
            start,
            destination,
            sunCenter,
            avoidanceRadius,
            arcSegments,
            forceAvoidance,
            Vector2.up,
            0f,
            false
        );
    }

    public static void BuildPath(
        List<Vector3> result,
        Vector3 start,
        Vector3 destination,
        Vector3 sunCenter,
        float avoidanceRadius,
        int arcSegments,
        bool forceAvoidance,
        Vector2 startFacingDirection,
        float turnRadius
    )
    {
        BuildPath(
            result,
            start,
            destination,
            sunCenter,
            avoidanceRadius,
            arcSegments,
            forceAvoidance,
            startFacingDirection,
            turnRadius,
            true
        );
    }

    private static void BuildPath(
        List<Vector3> result,
        Vector3 start,
        Vector3 destination,
        Vector3 sunCenter,
        float avoidanceRadius,
        int arcSegments,
        bool forceAvoidance,
        Vector2 startFacingDirection,
        float turnRadius,
        bool useStartFacingDirection
    )
    {
        if (result == null)
            return;

        result.Clear();

        result.Add(start);

        if (avoidanceRadius <= 0f)
        {
            result.Add(destination);
            return;
        }

        Vector2 start2 = new Vector2(start.x, start.y);
        Vector2 destination2 = new Vector2(destination.x, destination.y);
        Vector2 center2 = new Vector2(sunCenter.x, sunCenter.y);

        Vector2 safeStart2 = PushPointOutsideCircle(
            start2,
            destination2,
            center2,
            avoidanceRadius
        );

        Vector2 safeDestination2 = PushPointOutsideCircle(
            destination2,
            safeStart2,
            center2,
            avoidanceRadius
        );

        Vector3 safeStart3 = ToVector3(safeStart2, start.z);
        Vector3 safeDestination3 = ToVector3(safeDestination2, destination.z);

        if (Vector2.Distance(start2, safeStart2) > Epsilon)
            result.Add(safeStart3);

        bool intersectsCircle = SegmentIntersectsCircle(
            safeStart2,
            safeDestination2,
            center2,
            avoidanceRadius
        );

        if (!intersectsCircle &&
            !forceAvoidance)
        {
            if (Vector2.Distance(safeDestination2, destination2) > Epsilon)
                result.Add(safeDestination3);

            result.Add(destination);
            return;
        }

        Vector2[] startTangents = GetTangents(
            safeStart2,
            center2,
            avoidanceRadius
        );

        Vector2[] destinationTangents = GetTangents(
            safeDestination2,
            center2,
            avoidanceRadius
        );

        TangentOption bestOption = FindBestOption(
            safeStart2,
            safeDestination2,
            center2,
            avoidanceRadius,
            startTangents,
            destinationTangents,
            startFacingDirection,
            turnRadius,
            useStartFacingDirection
        );

        Vector3 startTangent3 = ToVector3(bestOption.StartTangent, start.z);
        Vector3 endTangent3 = ToVector3(bestOption.EndTangent, start.z);

        result.Add(startTangent3);

        AddArcPoints(
            result,
            center2,
            avoidanceRadius,
            bestOption.StartTangent,
            bestOption.EndTangent,
            bestOption.Clockwise,
            Mathf.Max(2, arcSegments),
            start.z
        );

        result.Add(endTangent3);

        if (Vector2.Distance(safeDestination2, destination2) > Epsilon)
            result.Add(safeDestination3);

        result.Add(destination);
    }

    private static Vector2 PushPointOutsideCircle(
    Vector2 point,
    Vector2 fallbackDirectionPoint,
    Vector2 center,
    float radius
)
    {
        Vector2 fromCenter =
            point - center;

        float distance =
            fromCenter.magnitude;

        if (distance >= radius + Epsilon)
            return point;

        Vector2 direction;

        if (distance > Epsilon)
        {
            direction =
                fromCenter.normalized;
        }
        else
        {
            Vector2 fallbackDirection =
                point - fallbackDirectionPoint;

            if (fallbackDirection.sqrMagnitude <= Epsilon)
                direction = Vector2.right;
            else
                direction = fallbackDirection.normalized;
        }

        return center + direction * (radius + PushOutsideOffset);
    }

    private static bool SegmentIntersectsCircle(
        Vector2 start,
        Vector2 end,
        Vector2 center,
        float radius
    )
    {
        Vector2 segment = end - start;
        float segmentLengthSqr = segment.sqrMagnitude;

        if (segmentLengthSqr <= Epsilon)
            return Vector2.Distance(start, center) <= radius;

        float t = Vector2.Dot(center - start, segment) / segmentLengthSqr;
        t = Mathf.Clamp01(t);

        Vector2 closestPoint = start + segment * t;
        float distance = Vector2.Distance(closestPoint, center);

        return distance < radius;
    }

    private static Vector2[] GetTangents(
        Vector2 point,
        Vector2 center,
        float radius
    )
    {
        Vector2 fromCenter = point - center;
        float distance = fromCenter.magnitude;

        if (distance <= radius + Epsilon)
        {
            Vector2 direction = fromCenter.sqrMagnitude <= Epsilon
                ? Vector2.right
                : fromCenter.normalized;

            point = center + direction * (radius + PushOutsideOffset);
            fromCenter = point - center;
            distance = fromCenter.magnitude;
        }

        float baseAngle = Mathf.Atan2(fromCenter.y, fromCenter.x);
        float safeRatio = Mathf.Clamp(radius / distance, -1f, 1f);
        float angleOffset = Mathf.Acos(safeRatio);

        float angleA = baseAngle + angleOffset;
        float angleB = baseAngle - angleOffset;

        Vector2 tangentA = center + new Vector2(
            Mathf.Cos(angleA),
            Mathf.Sin(angleA)
        ) * radius;

        Vector2 tangentB = center + new Vector2(
            Mathf.Cos(angleB),
            Mathf.Sin(angleB)
        ) * radius;

        return new[] { tangentA, tangentB };
    }

    private static TangentOption FindBestOption(
        Vector2 start,
        Vector2 destination,
        Vector2 center,
        float radius,
        Vector2[] startTangents,
        Vector2[] destinationTangents,
        Vector2 startFacingDirection,
        float turnRadius,
        bool useStartFacingDirection
    )
    {
        TangentOption best = new TangentOption
        {
            TotalLength = float.MaxValue
        };

        foreach (Vector2 startTangent in startTangents)
        {
            foreach (Vector2 endTangent in destinationTangents)
            {
                TryCandidate(
                    ref best,
                    start,
                    destination,
                    center,
                    radius,
                    startTangent,
                    endTangent,
                    clockwise: true,
                    startFacingDirection,
                    turnRadius,
                    useStartFacingDirection
                );

                TryCandidate(
                    ref best,
                    start,
                    destination,
                    center,
                    radius,
                    startTangent,
                    endTangent,
                    clockwise: false,
                    startFacingDirection,
                    turnRadius,
                    useStartFacingDirection
                );
            }
        }

        return best;
    }

    private static void TryCandidate(
    ref TangentOption best,
    Vector2 start,
    Vector2 destination,
    Vector2 center,
    float radius,
    Vector2 startTangent,
    Vector2 endTangent,
    bool clockwise,
    Vector2 startFacingDirection,
    float turnRadius,
    bool useStartFacingDirection
)
    {
        Vector2 lineToStartTangent =
            startTangent - start;

        Vector2 lineFromEndTangent =
            destination - endTangent;

        if (!IsDirectionContinuous(
                lineToStartTangent,
                GetCircleTangentDirection(
                    center,
                    startTangent,
                    clockwise)))
        {
            return;
        }

        if (!IsDirectionContinuous(
                GetCircleTangentDirection(
                    center,
                    endTangent,
                    clockwise),
                lineFromEndTangent))
        {
            return;
        }

        float lineToStartTangentLength =
            lineToStartTangent.magnitude;

        float lineFromEndTangentLength =
            lineFromEndTangent.magnitude;

        float arcLength =
            GetArcLength(
                center,
                radius,
                startTangent,
                endTangent,
                clockwise);

        float totalLength =
            lineToStartTangentLength +
            arcLength +
            lineFromEndTangentLength;

        if (useStartFacingDirection &&
            turnRadius > 0f)
        {
            totalLength +=
                GetHeadingPenalty(
                    start,
                    startTangent,
                    startFacingDirection,
                    turnRadius);
        }

        if (totalLength >= best.TotalLength)
            return;

        best = new TangentOption
        {
            StartTangent = startTangent,
            EndTangent = endTangent,
            Clockwise = clockwise,
            TotalLength = totalLength
        };
    }

    private static Vector2 GetCircleTangentDirection(
    Vector2 center,
    Vector2 pointOnCircle,
    bool clockwise)
    {
        Vector2 radial =
            pointOnCircle - center;

        if (radial.sqrMagnitude <= Epsilon)
            return Vector2.right;

        radial =
            radial.normalized;

        if (clockwise)
        {
            return new Vector2(
                radial.y,
                -radial.x);
        }

        return new Vector2(
            -radial.y,
            radial.x);
    }

    private static bool IsDirectionContinuous(
    Vector2 fromDirection,
    Vector2 toDirection)
    {
        if (fromDirection.sqrMagnitude <= Epsilon ||
            toDirection.sqrMagnitude <= Epsilon)
        {
            return true;
        }

        return Vector2.Dot(
                   fromDirection.normalized,
                   toDirection.normalized) > 0.15f;
    }

    private static float GetArcLength(
        Vector2 center,
        float radius,
        Vector2 from,
        Vector2 to,
        bool clockwise
    )
    {
        float fromAngle = Mathf.Atan2(from.y - center.y, from.x - center.x) * Mathf.Rad2Deg;
        float toAngle = Mathf.Atan2(to.y - center.y, to.x - center.x) * Mathf.Rad2Deg;

        float delta = GetAngleDelta(fromAngle, toAngle, clockwise);

        return Mathf.Abs(delta) * Mathf.Deg2Rad * radius;
    }

    private static float GetAngleDelta(
        float fromAngle,
        float toAngle,
        bool clockwise
    )
    {
        float deltaCounterClockwise = Mathf.Repeat(toAngle - fromAngle, 360f);

        if (!clockwise)
            return deltaCounterClockwise;

        if (deltaCounterClockwise <= 0f)
            return 0f;

        return deltaCounterClockwise - 360f;
    }

    private static void AddArcPoints(
        List<Vector3> result,
        Vector2 center,
        float radius,
        Vector2 from,
        Vector2 to,
        bool clockwise,
        int arcSegments,
        float z
    )
    {
        float fromAngle = Mathf.Atan2(from.y - center.y, from.x - center.x) * Mathf.Rad2Deg;
        float toAngle = Mathf.Atan2(to.y - center.y, to.x - center.x) * Mathf.Rad2Deg;

        float delta = GetAngleDelta(fromAngle, toAngle, clockwise);

        for (int i = 1; i < arcSegments; i++)
        {
            float t = i / (float)arcSegments;
            float angle = fromAngle + delta * t;
            float angleRad = angle * Mathf.Deg2Rad;

            Vector2 point = center + new Vector2(
                Mathf.Cos(angleRad),
                Mathf.Sin(angleRad)
            ) * radius;

            result.Add(ToVector3(point, z));
        }
    }

    private static float GetHeadingPenalty(
        Vector2 start,
        Vector2 startTangent,
        Vector2 startFacingDirection,
        float turnRadius)
    {
        if (startFacingDirection.sqrMagnitude <= Epsilon ||
            turnRadius <= 0f)
        {
            return 0f;
        }

        Vector2 toStartTangent =
            startTangent - start;

        if (toStartTangent.sqrMagnitude <= Epsilon)
            return 0f;

        return Vector2.Angle(
                   startFacingDirection.normalized,
                   toStartTangent.normalized) *
               Mathf.Deg2Rad *
               turnRadius *
               HeadingPenaltyMultiplier;
    }

    private static Vector3 ToVector3(Vector2 point, float z)
    {
        return new Vector3(point.x, point.y, z);
    }

    public static bool TryBuildPathAroundDangerZoneFromRawRoute(
    List<Vector3> result,
    IReadOnlyList<Vector3> rawRoute,
    Vector3 sunCenter,
    float avoidanceRadius,
    int arcSegments,
    Vector2 startFacingDirection,
    float turnRadius)
    {
        if (result == null)
            return false;

        result.Clear();

        if (rawRoute == null ||
            rawRoute.Count == 0)
        {
            return false;
        }

        if (rawRoute.Count == 1 ||
            avoidanceRadius <= 0f)
        {
            CopyRoute(result, rawRoute);
            return false;
        }

        Vector2 center =
            new Vector2(
                sunCenter.x,
                sunCenter.y);

        int firstUnsafeSegmentIndex =
            -1;

        int lastUnsafeSegmentIndex =
            -1;

        for (int i = 1; i < rawRoute.Count; i++)
        {
            Vector2 from =
                new Vector2(
                    rawRoute[i - 1].x,
                    rawRoute[i - 1].y);

            Vector2 to =
                new Vector2(
                    rawRoute[i].x,
                    rawRoute[i].y);

            bool unsafeSegment =
                IsPointInsideCircle(from, center, avoidanceRadius) ||
                IsPointInsideCircle(to, center, avoidanceRadius) ||
                SegmentIntersectsCircle(
                    from,
                    to,
                    center,
                    avoidanceRadius);

            if (!unsafeSegment)
                continue;

            if (firstUnsafeSegmentIndex < 0)
                firstUnsafeSegmentIndex = i;

            lastUnsafeSegmentIndex = i;
        }

        if (firstUnsafeSegmentIndex < 0)
        {
            CopyRoute(result, rawRoute);
            return false;
        }

        int detourStartIndex =
            Mathf.Max(
                0,
                firstUnsafeSegmentIndex - 1);

        int detourEndIndex =
            Mathf.Clamp(
                lastUnsafeSegmentIndex,
                detourStartIndex + 1,
                rawRoute.Count - 1);

        for (int i = 0; i <= detourStartIndex; i++)
        {
            AddPointIfDifferent(
                result,
                rawRoute[i]);
        }

        Vector2 detourFacingDirection =
            startFacingDirection;

        if (detourStartIndex > 0)
        {
            Vector3 previous =
                rawRoute[detourStartIndex - 1];

            Vector3 current =
                rawRoute[detourStartIndex];

            Vector2 segmentDirection =
                new Vector2(
                    current.x - previous.x,
                    current.y - previous.y);

            if (segmentDirection.sqrMagnitude > Epsilon)
                detourFacingDirection = segmentDirection.normalized;
        }

        List<Vector3> detour =
            new List<Vector3>();

        BuildPath(
            detour,
            rawRoute[detourStartIndex],
            rawRoute[detourEndIndex],
            sunCenter,
            avoidanceRadius,
            arcSegments,
            true,
            detourFacingDirection,
            turnRadius,
            true);

        for (int i = 1; i < detour.Count; i++)
        {
            AddPointIfDifferent(
                result,
                detour[i]);
        }

        for (int i = detourEndIndex + 1; i < rawRoute.Count; i++)
        {
            AddPointIfDifferent(
                result,
                rawRoute[i]);
        }

        return true;
    }

    private static void CopyRoute(
        List<Vector3> result,
        IReadOnlyList<Vector3> route)
    {
        result.Clear();

        if (route == null)
            return;

        for (int i = 0; i < route.Count; i++)
        {
            AddPointIfDifferent(
                result,
                route[i]);
        }
    }

    private static void AddPointIfDifferent(
        List<Vector3> result,
        Vector3 point)
    {
        if (result.Count == 0 ||
            Vector3.Distance(
                result[result.Count - 1],
                point) > Epsilon)
        {
            result.Add(point);
        }
    }

    private static bool IsPointInsideCircle(
        Vector2 point,
        Vector2 center,
        float radius)
    {
        return Vector2.Distance(
            point,
            center) < radius;
    }
}
