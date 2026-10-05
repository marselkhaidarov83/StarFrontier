using System;
using System.Collections.Generic;
using UnityEngine;

public interface ISystemShipRoutePlanner2A
{
    bool TryBuildTurnRadiusPreviewRoute(
        SystemSharedShipRoutePlanRequest2A request,
        SystemSharedShipRoutePlanResult2A result);
}

public sealed class SystemSharedShipRoutePlanRequest2A
{
    public string SystemId;
    public IReadOnlyList<Vector3> Waypoints;
    public Vector2 StartFacingDirection;
    public float Speed;
    public float TurnRadius;
    public float ArrivalDistanceThreshold;
    public int RouteSubstepsPerTick;
    public int MaxRoutePlanSteps;
    public float StraightExitAngleDegrees;
    public float TurnRadiusAdjustmentStepPercent = 5f;
    public float SpeedAdjustmentStepPercent = 2.5f;
    public float MinTurnRadiusAdjustmentFactor = 0.05f;
    public float MinTurnRadiusAbsolute = 30f;
    public float SunAvoidanceSafetyMargin;
    public float SunAvoidanceTurnRouteReserveMultiplier = 1.5f;
    public Action<string> DebugLog;
    public string DebugPrefix;
}

public sealed class SystemSharedShipRoutePlanResult2A
{
    public readonly List<Vector3> Path = new();

    public bool Built;
    public float PathLength;
    public float StartTurnAngleDegrees;
    public float TurnRadiusFactor = 1f;
    public float SpeedFactor = 1f;
    public float EffectiveTurnRadius;
    public float EffectiveSpeed;
    public float MaxAllowedRouteLength;
    public int AttemptCount;
    public string Source;
    public string RejectReason;

    public void Clear()
    {
        Path.Clear();
        Built = false;
        PathLength = 0f;
        StartTurnAngleDegrees = 0f;
        TurnRadiusFactor = 1f;
        SpeedFactor = 1f;
        EffectiveTurnRadius = 0f;
        EffectiveSpeed = 0f;
        MaxAllowedRouteLength = 0f;
        AttemptCount = 0;
        Source = string.Empty;
        RejectReason = string.Empty;
    }
}