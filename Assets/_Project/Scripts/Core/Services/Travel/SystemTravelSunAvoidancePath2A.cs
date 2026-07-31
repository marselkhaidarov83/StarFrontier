using System.Collections.Generic;
using UnityEngine;

public static class SystemTravelSunAvoidancePath2A
{
    private const float Epsilon = 0.001f;
    private const float PushOutsideOffset = 8f;

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

        if (!intersectsCircle)
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
            destinationTangents
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
        Vector2 fromCenter = point - center;
        float distance = fromCenter.magnitude;

        if (distance > radius + PushOutsideOffset)
            return point;

        Vector2 direction;

        if (distance > Epsilon)
        {
            direction = fromCenter.normalized;
        }
        else
        {
            Vector2 fallbackDirection = point - fallbackDirectionPoint;

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
        Vector2[] destinationTangents
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
                    clockwise: true
                );

                TryCandidate(
                    ref best,
                    start,
                    destination,
                    center,
                    radius,
                    startTangent,
                    endTangent,
                    clockwise: false
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
        bool clockwise
    )
    {
        float lineToStartTangent = Vector2.Distance(start, startTangent);
        float lineFromEndTangent = Vector2.Distance(endTangent, destination);

        float arcLength = GetArcLength(
            center,
            radius,
            startTangent,
            endTangent,
            clockwise
        );

        float totalLength = lineToStartTangent + arcLength + lineFromEndTangent;

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

    private static Vector3 ToVector3(Vector2 point, float z)
    {
        return new Vector3(point.x, point.y, z);
    }
}