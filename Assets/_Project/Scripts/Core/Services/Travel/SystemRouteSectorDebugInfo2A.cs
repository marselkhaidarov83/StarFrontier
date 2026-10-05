using UnityEngine;

public readonly struct SystemRouteSectorDebugInfo2A
{
    public SystemRouteSectorDebugInfo2A(
        string destinationCase,
        Vector3 shipPosition,
        Vector3 targetPosition,
        Vector2 shipFacingDirection,
        float directDistance,
        float bearingAngleDegrees,
        float nearDistanceThreshold,
        bool isNear,
        bool requiresSunAvoidance,
        bool startInsideSunSafety,
        bool targetInsideSunSafety,
        bool targetInsideSunBody,
        float startDistanceFromSun,
        float targetDistanceFromSun,
        bool hasSun,
        Vector3 sunCenter,
        float sunSafetyRadius,
        float sunForbiddenRadius,
        float forwardSectorAngleDegrees,
        float behindSectorAngleDegrees,
        float sunTangentToleranceAngleDegrees)
    {
        DestinationCase = destinationCase;
        ShipPosition = shipPosition;
        TargetPosition = targetPosition;
        ShipFacingDirection = shipFacingDirection;
        DirectDistance = directDistance;
        BearingAngleDegrees = bearingAngleDegrees;
        NearDistanceThreshold = nearDistanceThreshold;
        IsNear = isNear;
        RequiresSunAvoidance = requiresSunAvoidance;
        StartInsideSunSafety = startInsideSunSafety;
        TargetInsideSunSafety = targetInsideSunSafety;
        TargetInsideSunBody = targetInsideSunBody;
        StartDistanceFromSun = startDistanceFromSun;
        TargetDistanceFromSun = targetDistanceFromSun;
        HasSun = hasSun;
        SunCenter = sunCenter;
        SunSafetyRadius = sunSafetyRadius;
        SunForbiddenRadius = sunForbiddenRadius;
        ForwardSectorAngleDegrees = forwardSectorAngleDegrees;
        BehindSectorAngleDegrees = behindSectorAngleDegrees;
        SunTangentToleranceAngleDegrees = sunTangentToleranceAngleDegrees;
    }

    public string DestinationCase { get; }
    public Vector3 ShipPosition { get; }
    public Vector3 TargetPosition { get; }
    public Vector2 ShipFacingDirection { get; }
    public float DirectDistance { get; }
    public float BearingAngleDegrees { get; }
    public float NearDistanceThreshold { get; }
    public bool IsNear { get; }
    public bool RequiresSunAvoidance { get; }
    public bool StartInsideSunSafety { get; }
    public bool TargetInsideSunSafety { get; }
    public bool TargetInsideSunBody { get; }
    public float StartDistanceFromSun { get; }
    public float TargetDistanceFromSun { get; }
    public bool HasSun { get; }
    public Vector3 SunCenter { get; }
    public float SunSafetyRadius { get; }
    public float SunForbiddenRadius { get; }
    public float ForwardSectorAngleDegrees { get; }
    public float BehindSectorAngleDegrees { get; }
    public float SunTangentToleranceAngleDegrees { get; }
}