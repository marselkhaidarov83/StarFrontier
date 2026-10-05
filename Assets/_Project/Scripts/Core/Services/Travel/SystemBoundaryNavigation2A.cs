using UnityEngine;

public static class SystemBoundaryNavigation2A
{
    private const float Epsilon = 0.001f;
    private const float MinUsableBoundsHalfSize = 100f;

    public enum BoundaryNavigationEdge
    {
        None,
        Left,
        Right,
        Bottom,
        Top
    }

    public sealed class BoundaryNavigationState
    {
        public bool IsActive;
        public BoundaryNavigationEdge Edge = BoundaryNavigationEdge.None;
        public Vector2 TangentDirection = Vector2.zero;
        public int LockedUntilTick = -1;

        public bool IsProtectiveRadiusActive;
        public bool HasProtectiveRadiusDestination;
        public Vector2 ProtectiveRadiusDestination = Vector2.zero;
        public float ProtectiveRadiusBlend;
        public int ProtectiveRadiusLockedUntilTick = -1;

        public void Clear()
        {
            IsActive = false;
            Edge = BoundaryNavigationEdge.None;
            TangentDirection = Vector2.zero;
            LockedUntilTick = -1;

            IsProtectiveRadiusActive = false;
            HasProtectiveRadiusDestination = false;
            ProtectiveRadiusDestination = Vector2.zero;
            ProtectiveRadiusBlend = 0f;
            ProtectiveRadiusLockedUntilTick = -1;
        }
    }

    public static Vector3 GetRouteDestinationInsideSystemBounds(
        string systemId,
        Vector3 startPosition,
        Vector3 destinationPosition,
        Vector2 startFacingDirection,
        ShipMovementConfig shipMovementConfig,
        BoundaryNavigationState boundaryState,
        int currentTick,
        out bool isBoundaryAdjusted)
    {
        return GetRouteDestinationInsideSystemBounds(
            systemId,
            startPosition,
            destinationPosition,
            startFacingDirection,
            shipMovementConfig,
            boundaryState,
            currentTick,
            0f,
            out isBoundaryAdjusted);
    }

    public static Vector3 GetRouteDestinationInsideSystemBounds(
        string systemId,
        Vector3 startPosition,
        Vector3 destinationPosition,
        Vector2 startFacingDirection,
        ShipMovementConfig shipMovementConfig,
        BoundaryNavigationState boundaryState,
        int currentTick,
        float boundaryStepDistance,
        out bool isBoundaryAdjusted)
    {
        return GetRouteDestinationInsideSystemBounds(
            systemId,
            startPosition,
            destinationPosition,
            startFacingDirection,
            shipMovementConfig,
            boundaryState,
            currentTick,
            boundaryStepDistance,
            false,
            out isBoundaryAdjusted);
    }

    public static Vector3 GetRouteDestinationInsideSystemBounds(
        string systemId,
        Vector3 startPosition,
        Vector3 destinationPosition,
        Vector2 startFacingDirection,
        ShipMovementConfig shipMovementConfig,
        BoundaryNavigationState boundaryState,
        int currentTick,
        float boundaryStepDistance,
        bool useProtectiveRadiusNavigation,
        out bool isBoundaryAdjusted)
    {
        isBoundaryAdjusted = false;

        if (!TryGetNavigationBounds(
                systemId,
                shipMovementConfig,
                out NavigationBounds bounds))
        {
            boundaryState?.Clear();
            return destinationPosition;
        }

        if (useProtectiveRadiusNavigation &&
            TryGetProtectiveRadiusRouteDestination(
                systemId,
                startPosition,
                destinationPosition,
                startFacingDirection,
                shipMovementConfig,
                boundaryState,
                currentTick,
                bounds,
                boundaryStepDistance,
                out Vector3 protectiveDestination))
        {
            isBoundaryAdjusted = true;
            return protectiveDestination;
        }

        ClearProtectiveRadiusState(boundaryState);

        Vector2 start =
            new Vector2(
                startPosition.x,
                startPosition.y);

        Vector2 destination =
            new Vector2(
                destinationPosition.x,
                destinationPosition.y);

        bool startInsideSoftBounds =
            bounds.ContainsSoft(start);

        bool destinationInsideSoftBounds =
            bounds.ContainsSoft(destination);

        bool startNearSoftBoundary =
            startInsideSoftBounds &&
            IsPointNearSoftBoundary(
                start,
                bounds);

        bool destinationNearSoftBoundary =
            destinationInsideSoftBounds &&
            IsPointNearSoftBoundary(
                destination,
                bounds);

        bool hasActiveBoundaryState =
            boundaryState != null &&
            boundaryState.IsActive &&
            boundaryState.Edge != BoundaryNavigationEdge.None;

        bool shouldContinueBoundaryRoute =
            hasActiveBoundaryState &&
            startNearSoftBoundary;

        if (startInsideSoftBounds &&
            destinationInsideSoftBounds &&
            !shouldContinueBoundaryRoute)
        {
            if (!destinationNearSoftBoundary)
                boundaryState?.Clear();

            return destinationPosition;
        }

        isBoundaryAdjusted = true;

        Vector2 desiredDirection =
            destination - start;

        if (desiredDirection.sqrMagnitude <= Epsilon)
        {
            desiredDirection =
                startFacingDirection.sqrMagnitude > Epsilon
                    ? startFacingDirection.normalized
                    : GetDirectionFromCenter(bounds, start);
        }

        if (desiredDirection.sqrMagnitude <= Epsilon)
            desiredDirection = Vector2.up;

        desiredDirection.Normalize();

        BoundaryNavigationEdge requestedEdge;
        Vector2 requestedTangentDirection;

        if (shouldContinueBoundaryRoute)
        {
            requestedEdge =
                boundaryState.Edge;

            requestedTangentDirection =
                GetEdgeTangentDirection(
                    requestedEdge,
                    desiredDirection,
                    startFacingDirection);
        }
        else
        {
            Vector2 rayStart =
                startInsideSoftBounds
                    ? start
                    : bounds.ClampToSoft(start);

            Vector2 edgePoint;

            if (!TryGetSoftBoundsRayExitPoint(
                    rayStart,
                    desiredDirection,
                    bounds,
                    out edgePoint))
            {
                edgePoint =
                    bounds.ClampToSoft(destination);
            }

            requestedEdge =
                GetClosestBoundaryEdge(
                    edgePoint,
                    bounds);

            requestedTangentDirection =
                GetEdgeTangentDirection(
                    requestedEdge,
                    desiredDirection,
                    startFacingDirection);
        }

        BoundaryNavigationEdge activeEdge =
            requestedEdge;

        Vector2 activeTangentDirection =
            requestedTangentDirection;

        if (ShouldKeepBoundaryNavigationState(
                boundaryState,
                currentTick,
                requestedEdge))
        {
            activeEdge =
                boundaryState.Edge;

            activeTangentDirection =
                boundaryState.TangentDirection.sqrMagnitude > Epsilon
                    ? boundaryState.TangentDirection.normalized
                    : requestedTangentDirection;
        }

        Vector2 activeEdgePoint =
            ProjectPointToBoundaryEdge(
                bounds.ClampToSoft(start),
                activeEdge,
                bounds);

        float edgeDistance =
            boundaryStepDistance > Epsilon
                ? boundaryStepDistance
                : GetEdgeCruiseDistance(bounds);

        Vector2 edgeDestination =
            bounds.ClampToSoft(
                activeEdgePoint + activeTangentDirection * edgeDistance);

        SaveBoundaryNavigationState(
            boundaryState,
            activeEdge,
            activeTangentDirection,
            currentTick);

        return new Vector3(
            edgeDestination.x,
            edgeDestination.y,
            destinationPosition.z);
    }

    private static bool TryGetProtectiveRadiusRouteDestination(
        string systemId,
        Vector3 startPosition,
        Vector3 destinationPosition,
        Vector2 startFacingDirection,
        ShipMovementConfig shipMovementConfig,
        BoundaryNavigationState boundaryState,
        int currentTick,
        NavigationBounds bounds,
        float boundaryStepDistance,
        out Vector3 protectiveDestination)
    {
        protectiveDestination = destinationPosition;

        if (shipMovementConfig == null ||
            shipMovementConfig.BoundaryProtectionRadiusWorld <= Epsilon)
        {
            ClearProtectiveRadiusState(boundaryState);
            return false;
        }

        Vector2 protectionCenter =
            TryGetBoundaryProtectionCenter(
                systemId,
                out Vector2 resolvedCenter)
                ? resolvedCenter
                : bounds.Center;

        Vector2 start =
            new Vector2(
                startPosition.x,
                startPosition.y);

        Vector2 startFromCenter =
            start - protectionCenter;

        float startRadius =
            startFromCenter.magnitude;

        float protectionRadius =
            shipMovementConfig.BoundaryProtectionRadiusWorld;

        float pullStep =
            shipMovementConfig.BoundaryProtectionPullStepWorld > Epsilon
                ? shipMovementConfig.BoundaryProtectionPullStepWorld
                : 80f;

        bool hasActiveProtectiveState =
            boundaryState != null &&
            boundaryState.IsProtectiveRadiusActive;

        float releaseRadius =
            Mathf.Max(
                0f,
                protectionRadius - pullStep);

        if (hasActiveProtectiveState &&
            startRadius < releaseRadius)
        {
            ClearProtectiveRadiusState(boundaryState);
            return false;
        }

        if (!hasActiveProtectiveState &&
            startRadius <= protectionRadius)
        {
            return false;
        }

        Vector2 destination =
            new Vector2(
                destinationPosition.x,
                destinationPosition.y);

        Vector2 destinationFromCenter =
            destination - protectionCenter;

        float destinationRadius =
            destinationFromCenter.magnitude;

        if (destinationRadius <= Epsilon)
            return false;

        float pulledDestinationRadius =
            Mathf.MoveTowards(
                destinationRadius,
                protectionRadius,
                pullStep);

        float outsideDistance =
            Mathf.Max(
                0f,
                startRadius - protectionRadius);

        float targetBlend =
            Mathf.Clamp01(
                outsideDistance / Mathf.Max(1f, pullStep));

        float previousBlend =
            hasActiveProtectiveState && boundaryState != null
                ? Mathf.Clamp01(boundaryState.ProtectiveRadiusBlend)
                : 0f;

        float blendStep =
            0.15f;

        float protectionBlend =
            Mathf.MoveTowards(
                previousBlend,
                targetBlend,
                blendStep);

        float adjustedDestinationRadius =
            Mathf.Lerp(
                destinationRadius,
                pulledDestinationRadius,
                protectionBlend);

        Vector2 adjustedDestination =
            protectionCenter +
            destinationFromCenter.normalized * adjustedDestinationRadius;

        adjustedDestination =
            bounds.ClampToSoft(adjustedDestination);

        if (boundaryState != null &&
            boundaryState.HasProtectiveRadiusDestination)
        {
            float maxTargetShift =
                Mathf.Max(
                    1f,
                    pullStep * Mathf.Lerp(0.2f, 1f, protectionBlend));

            adjustedDestination =
                Vector2.MoveTowards(
                    boundaryState.ProtectiveRadiusDestination,
                    adjustedDestination,
                    maxTargetShift);
        }

        adjustedDestination =
            bounds.ClampToSoft(adjustedDestination);

        SaveProtectiveRadiusState(
            boundaryState,
            adjustedDestination,
            protectionBlend,
            currentTick);

        protectiveDestination =
            new Vector3(
                adjustedDestination.x,
                adjustedDestination.y,
                destinationPosition.z);

        return true;
    }

    private static void SaveProtectiveRadiusState(
        BoundaryNavigationState boundaryState,
        Vector2 destination,
        float blend,
        int currentTick)
    {
        if (boundaryState == null)
            return;

        boundaryState.IsProtectiveRadiusActive = true;
        boundaryState.HasProtectiveRadiusDestination = true;
        boundaryState.ProtectiveRadiusDestination = destination;
        boundaryState.ProtectiveRadiusBlend = Mathf.Clamp01(blend);
        boundaryState.ProtectiveRadiusLockedUntilTick =
            currentTick >= 0
                ? currentTick + 2
                : -1;
    }

    private static void ClearProtectiveRadiusState(
    BoundaryNavigationState boundaryState)
    {
        if (boundaryState == null)
            return;

        boundaryState.IsProtectiveRadiusActive = false;
        boundaryState.HasProtectiveRadiusDestination = false;
        boundaryState.ProtectiveRadiusDestination = Vector2.zero;
        boundaryState.ProtectiveRadiusBlend = 0f;
        boundaryState.ProtectiveRadiusLockedUntilTick = -1;
    }

    private static float GetMaxCircleRadiusInsideSoftBounds(
        Vector2 center,
        NavigationBounds bounds)
    {
        float distanceToLeft =
            Mathf.Abs(center.x - bounds.SoftMin.x);

        float distanceToRight =
            Mathf.Abs(bounds.SoftMax.x - center.x);

        float distanceToBottom =
            Mathf.Abs(center.y - bounds.SoftMin.y);

        float distanceToTop =
            Mathf.Abs(bounds.SoftMax.y - center.y);

        return Mathf.Max(
            0f,
            Mathf.Min(
                Mathf.Min(distanceToLeft, distanceToRight),
                Mathf.Min(distanceToBottom, distanceToTop)));
    }

    private static bool TryGetBoundaryProtectionCenter(
        string systemId,
        out Vector2 center)
    {
        center = Vector2.zero;

        if (string.IsNullOrWhiteSpace(systemId))
            return false;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return false;
        }

        if (!Bootstrapper.Instance.ServiceRegistry.TryGet(
                out IConfigService configService) ||
            configService == null)
        {
            return false;
        }

        StarSystemConfig system =
            configService.GetStarSystemConfigById(systemId);

        if (system == null ||
            system.Sun == null)
        {
            return false;
        }

        center =
            new Vector2(
                system.Sun.LocalOffset.x,
                system.Sun.LocalOffset.y);

        return true;
    }

    public static bool IsPositionNearSystemBounds(
        string systemId,
        Vector3 position,
        ShipMovementConfig shipMovementConfig)
    {
        if (!TryGetNavigationBounds(
                systemId,
                shipMovementConfig,
                out NavigationBounds bounds))
        {
            return false;
        }

        Vector2 point =
            new Vector2(
                position.x,
                position.y);

        if (!bounds.ContainsSoft(point))
            return true;

        return IsPointNearSoftBoundary(
            point,
            bounds);
    }

    private static bool ShouldKeepBoundaryNavigationState(
        BoundaryNavigationState boundaryState,
        int currentTick,
        BoundaryNavigationEdge requestedEdge)
    {
        if (boundaryState == null ||
            !boundaryState.IsActive ||
            boundaryState.Edge == BoundaryNavigationEdge.None)
        {
            return false;
        }

        if (currentTick < 0)
            return false;

        return currentTick <= boundaryState.LockedUntilTick;
    }

    private static void SaveBoundaryNavigationState(
        BoundaryNavigationState boundaryState,
        BoundaryNavigationEdge edge,
        Vector2 tangentDirection,
        int currentTick)
    {
        if (boundaryState == null)
            return;

        if (edge == BoundaryNavigationEdge.None ||
            tangentDirection.sqrMagnitude <= Epsilon)
        {
            boundaryState.Clear();
            return;
        }

        boundaryState.IsActive = true;
        boundaryState.Edge = edge;
        boundaryState.TangentDirection = tangentDirection.normalized;
        boundaryState.LockedUntilTick =
            currentTick >= 0
                ? currentTick + 2
                : -1;
    }

    private static bool IsPointNearSoftBoundary(
        Vector2 point,
        NavigationBounds bounds)
    {
        Vector2 min = bounds.SoftMin;
        Vector2 max = bounds.SoftMax;

        float distanceToLeft =
            Mathf.Abs(point.x - min.x);

        float distanceToRight =
            Mathf.Abs(max.x - point.x);

        float distanceToBottom =
            Mathf.Abs(point.y - min.y);

        float distanceToTop =
            Mathf.Abs(max.y - point.y);

        float distanceToEdge =
            Mathf.Min(
                Mathf.Min(distanceToLeft, distanceToRight),
                Mathf.Min(distanceToBottom, distanceToTop));

        float minHalfSize =
            Mathf.Min(
                bounds.SoftHalfSize.x,
                bounds.SoftHalfSize.y);

        float boundaryBand =
            Mathf.Clamp(
                minHalfSize * 0.08f,
                25f,
                200f);

        return distanceToEdge <= boundaryBand;
    }

    private static BoundaryNavigationEdge GetClosestBoundaryEdge(
        Vector2 point,
        NavigationBounds bounds)
    {
        float distanceToLeft =
            Mathf.Abs(point.x - bounds.SoftMin.x);

        float distanceToRight =
            Mathf.Abs(point.x - bounds.SoftMax.x);

        float distanceToBottom =
            Mathf.Abs(point.y - bounds.SoftMin.y);

        float distanceToTop =
            Mathf.Abs(point.y - bounds.SoftMax.y);

        float bestDistance = distanceToLeft;
        BoundaryNavigationEdge bestEdge = BoundaryNavigationEdge.Left;

        if (distanceToRight < bestDistance)
        {
            bestDistance = distanceToRight;
            bestEdge = BoundaryNavigationEdge.Right;
        }

        if (distanceToBottom < bestDistance)
        {
            bestDistance = distanceToBottom;
            bestEdge = BoundaryNavigationEdge.Bottom;
        }

        if (distanceToTop < bestDistance)
        {
            bestEdge = BoundaryNavigationEdge.Top;
        }

        return bestEdge;
    }

    private static Vector2 GetEdgeTangentDirection(
        BoundaryNavigationEdge edge,
        Vector2 desiredDirection,
        Vector2 startFacingDirection)
    {
        Vector2 positiveTangent =
            edge == BoundaryNavigationEdge.Left ||
            edge == BoundaryNavigationEdge.Right
                ? Vector2.up
                : Vector2.right;

        float score =
            desiredDirection.sqrMagnitude > Epsilon
                ? Vector2.Dot(
                    positiveTangent,
                    desiredDirection.normalized)
                : 0f;

        if (Mathf.Abs(score) <= Epsilon &&
            startFacingDirection.sqrMagnitude > Epsilon)
        {
            score =
                Vector2.Dot(
                    positiveTangent,
                    startFacingDirection.normalized);
        }

        return score >= 0f
            ? positiveTangent
            : -positiveTangent;
    }

    private static Vector2 ProjectPointToBoundaryEdge(
        Vector2 point,
        BoundaryNavigationEdge edge,
        NavigationBounds bounds)
    {
        Vector2 result =
            bounds.ClampToSoft(point);

        switch (edge)
        {
            case BoundaryNavigationEdge.Left:
                result.x = bounds.SoftMin.x;
                break;

            case BoundaryNavigationEdge.Right:
                result.x = bounds.SoftMax.x;
                break;

            case BoundaryNavigationEdge.Bottom:
                result.y = bounds.SoftMin.y;
                break;

            case BoundaryNavigationEdge.Top:
                result.y = bounds.SoftMax.y;
                break;
        }

        return bounds.ClampToSoft(result);
    }

    private static bool TryGetNavigationBounds(
        string systemId,
        ShipMovementConfig shipMovementConfig,
        out NavigationBounds bounds)
    {
        bounds = default;

        if (TryGetRuntimeNavigationBounds(
                shipMovementConfig,
                out bounds))
        {
            return true;
        }

        if (TryGetShipMovementConfigNavigationBounds(
                shipMovementConfig,
                out bounds))
        {
            return true;
        }

        if (TryGetConfigNavigationBounds(
                systemId,
                shipMovementConfig,
                out bounds))
        {
            return true;
        }

        return false;
    }


    private static bool TryGetShipMovementConfigNavigationBounds(
        ShipMovementConfig shipMovementConfig,
        out NavigationBounds bounds)
    {
        bounds = default;

        if (shipMovementConfig == null ||
            !shipMovementConfig.ClampToSystemBounds)
        {
            return false;
        }

        Vector2 halfSize =
            shipMovementConfig.SystemBoundsHalfSize;

        bounds =
            new NavigationBounds(
                Vector2.zero,
                halfSize,
                GetBoundaryNavigationInset(
                    halfSize,
                    shipMovementConfig));

        return bounds.IsValid;
    }

    private static bool TryGetRuntimeNavigationBounds(
        ShipMovementConfig shipMovementConfig,
        out NavigationBounds bounds)
    {
        bounds = default;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return false;
        }

        if (!Bootstrapper.Instance.ServiceRegistry.TryGet(
                out ISystemGameplayStateService gameplayStateService) ||
            gameplayStateService == null ||
            gameplayStateService.Bounds == null ||
            !gameplayStateService.Bounds.IsInitialized ||
            !gameplayStateService.Bounds.IsEnabled)
        {
            return false;
        }

        SystemBoundsRuntimeState runtimeBounds =
            gameplayStateService.Bounds;

        bounds =
            new NavigationBounds(
                runtimeBounds.Center,
                runtimeBounds.HalfSize,
                GetBoundaryNavigationInset(
                    runtimeBounds.HalfSize,
                    shipMovementConfig));

        return bounds.IsValid;
    }

    private static bool TryGetConfigNavigationBounds(
    string systemId,
    ShipMovementConfig shipMovementConfig,
    out NavigationBounds bounds)
    {
        bounds = default;

        if (string.IsNullOrWhiteSpace(systemId))
            return false;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return false;
        }

        if (!Bootstrapper.Instance.ServiceRegistry.TryGet(
                out IConfigService configService) ||
            configService == null)
        {
            return false;
        }

        StarSystemConfig system =
            configService.GetStarSystemConfigById(systemId);

        if (system == null)
            return false;

        SystemCameraConfig cameraConfig =
            configService.SystemCameraConfig;

        SystemVisualConfig visualConfig =
            configService.SystemVisualConfig;

        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;
        bool hasPoint = false;

        EncapsulateCircle(
            ref min,
            ref max,
            ref hasPoint,
            Vector2.zero,
            0f);

        if (system.Sun != null)
        {
            float sunRadius =
                visualConfig != null
                    ? visualConfig.GetSunWorldSize(system.Sun) * 0.5f
                    : system.Sun.VisualSize * 0.5f;

            float sunPadding =
                cameraConfig != null
                    ? cameraConfig.SunExtraPadding
                    : 220f;

            EncapsulateCircle(
                ref min,
                ref max,
                ref hasPoint,
                system.Sun.LocalOffset,
                sunRadius + sunPadding);
        }

        if (system.PlanetRefs != null)
        {
            for (int i = 0; i < system.PlanetRefs.Length; i++)
            {
                PlanetConfig planet =
                    system.PlanetRefs[i];

                if (planet == null ||
                    planet.PlanetOrbit == null)
                {
                    continue;
                }

                PlanetOrbitConfig orbit =
                    planet.PlanetOrbit;

                Vector2 orbitCenter =
                    new Vector2(
                        orbit.OrbitCenterOffset.x,
                        orbit.OrbitCenterOffset.y);

                float planetRadius =
                    visualConfig != null
                        ? visualConfig.GetPlanetWorldSize(planet) * 0.5f
                        : planet.VisualSize * 0.5f;

                float planetPadding =
                    cameraConfig != null
                        ? cameraConfig.PlanetExtraPadding
                        : 180f;

                EncapsulateCircle(
                    ref min,
                    ref max,
                    ref hasPoint,
                    orbitCenter,
                    Mathf.Max(0f, orbit.OrbitRadius) + planetRadius + planetPadding);
            }
        }

        if (system.Station != null)
        {
            float stationRadius =
                visualConfig != null
                    ? visualConfig.GetStationWorldSize(system.Station) * 0.5f
                    : system.Station.VisualSize * 0.5f;

            float stationPadding =
                cameraConfig != null
                    ? cameraConfig.StationExtraPadding
                    : 220f;

            EncapsulateCircle(
                ref min,
                ref max,
                ref hasPoint,
                system.Station.LocalOffset,
                stationRadius + stationPadding);
        }

        if (system.Routes != null)
        {
            for (int i = 0; i < system.Routes.Count; i++)
            {
                RouteConfig route =
                    system.Routes[i];

                if (route == null)
                    continue;

                RouteEndpointConfig endpoint =
                    route.GetEndPointForSystem(system.Id);

                if (endpoint == null)
                    continue;

                float exitRadius =
                    visualConfig != null
                        ? visualConfig.GetSystemExitWorldSize(endpoint) * 0.5f
                        : endpoint.VisualSize * 0.5f;

                float exitPadding =
                    cameraConfig != null
                        ? cameraConfig.ExitExtraPadding
                        : 220f;

                EncapsulateCircle(
                    ref min,
                    ref max,
                    ref hasPoint,
                    endpoint.ExitPoint,
                    exitRadius + exitPadding);

                EncapsulateCircle(
                    ref min,
                    ref max,
                    ref hasPoint,
                    endpoint.EntryPoint,
                    exitRadius + exitPadding);
            }
        }

        if (!hasPoint)
            return false;

        float boundsPadding =
            cameraConfig != null
                ? cameraConfig.BoundsPadding
                : 320f;

        min -= new Vector2(boundsPadding, boundsPadding);
        max += new Vector2(boundsPadding, boundsPadding);

        Vector2 center =
            (min + max) * 0.5f;

        Vector2 halfSize =
            (max - min) * 0.5f;

        bounds =
    new NavigationBounds(
        center,
        halfSize,
        GetBoundaryNavigationInset(
            halfSize,
            shipMovementConfig));

        return bounds.IsValid;
    }

    private static float GetBoundaryNavigationInset(
    Vector2 halfSize,
    ShipMovementConfig shipMovementConfig)
    {
        float minHalfSize =
            Mathf.Min(
                Mathf.Abs(halfSize.x),
                Mathf.Abs(halfSize.y));

        if (minHalfSize <= 1f)
            return 0f;

        float maxInset =
            Mathf.Max(0f, minHalfSize - 1f);

        if (shipMovementConfig != null &&
            shipMovementConfig.UseBoundaryNavigationInsetWorldUnits)
        {
            return Mathf.Clamp(
                shipMovementConfig.BoundaryNavigationInsetWorldUnits,
                0f,
                maxInset);
        }

        float percent =
            shipMovementConfig != null
                ? shipMovementConfig.BoundaryNavigationInsetPercent
                : 0.03f;

        return Mathf.Clamp(
            minHalfSize * percent,
            0f,
            maxInset);
    }

    private static void EncapsulateCircle(
        ref Vector2 min,
        ref Vector2 max,
        ref bool hasPoint,
        Vector2 center,
        float radius)
    {
        radius =
            Mathf.Max(0f, radius);

        Vector2 circleMin =
            center - new Vector2(radius, radius);

        Vector2 circleMax =
            center + new Vector2(radius, radius);

        if (!hasPoint)
        {
            min = circleMin;
            max = circleMax;
            hasPoint = true;
            return;
        }

        min =
            new Vector2(
                Mathf.Min(min.x, circleMin.x),
                Mathf.Min(min.y, circleMin.y));

        max =
            new Vector2(
                Mathf.Max(max.x, circleMax.x),
                Mathf.Max(max.y, circleMax.y));
    }

    private static bool TryGetSoftBoundsRayExitPoint(
        Vector2 start,
        Vector2 direction,
        NavigationBounds bounds,
        out Vector2 exitPoint)
    {
        exitPoint = bounds.ClampToSoft(start);

        if (direction.sqrMagnitude <= Epsilon)
            return false;

        direction.Normalize();

        float bestT = float.MaxValue;
        bool found = false;

        TryCheckVerticalEdge(start, direction, bounds.SoftMin.x, bounds, ref bestT, ref exitPoint, ref found);
        TryCheckVerticalEdge(start, direction, bounds.SoftMax.x, bounds, ref bestT, ref exitPoint, ref found);
        TryCheckHorizontalEdge(start, direction, bounds.SoftMin.y, bounds, ref bestT, ref exitPoint, ref found);
        TryCheckHorizontalEdge(start, direction, bounds.SoftMax.y, bounds, ref bestT, ref exitPoint, ref found);

        return found;
    }

    private static void TryCheckVerticalEdge(
        Vector2 start,
        Vector2 direction,
        float edgeX,
        NavigationBounds bounds,
        ref float bestT,
        ref Vector2 bestPoint,
        ref bool found)
    {
        if (Mathf.Abs(direction.x) <= Epsilon)
            return;

        float t =
            (edgeX - start.x) / direction.x;

        if (t <= Epsilon ||
            t >= bestT)
        {
            return;
        }

        Vector2 point =
            start + direction * t;

        if (point.y < bounds.SoftMin.y - Epsilon ||
            point.y > bounds.SoftMax.y + Epsilon)
        {
            return;
        }

        bestT = t;
        bestPoint = bounds.ClampToSoft(point);
        found = true;
    }

    private static void TryCheckHorizontalEdge(
        Vector2 start,
        Vector2 direction,
        float edgeY,
        NavigationBounds bounds,
        ref float bestT,
        ref Vector2 bestPoint,
        ref bool found)
    {
        if (Mathf.Abs(direction.y) <= Epsilon)
            return;

        float t =
            (edgeY - start.y) / direction.y;

        if (t <= Epsilon ||
            t >= bestT)
        {
            return;
        }

        Vector2 point =
            start + direction * t;

        if (point.x < bounds.SoftMin.x - Epsilon ||
            point.x > bounds.SoftMax.x + Epsilon)
        {
            return;
        }

        bestT = t;
        bestPoint = bounds.ClampToSoft(point);
        found = true;
    }

    private static Vector2 GetEdgeTangentDirection(
        Vector2 edgePoint,
        Vector2 desiredDirection,
        Vector2 startFacingDirection,
        NavigationBounds bounds)
    {
        bool useVerticalEdge =
            IsCloserToVerticalEdge(
                edgePoint,
                bounds);

        Vector2 positiveTangent =
            useVerticalEdge
                ? Vector2.up
                : Vector2.right;

        float score =
            Vector2.Dot(
                positiveTangent,
                desiredDirection.normalized);

        if (Mathf.Abs(score) <= Epsilon &&
            startFacingDirection.sqrMagnitude > Epsilon)
        {
            score =
                Vector2.Dot(
                    positiveTangent,
                    startFacingDirection.normalized);
        }

        return score >= 0f
            ? positiveTangent
            : -positiveTangent;
    }

    private static bool IsCloserToVerticalEdge(
        Vector2 point,
        NavigationBounds bounds)
    {
        float distanceToLeft =
            Mathf.Abs(point.x - bounds.SoftMin.x);

        float distanceToRight =
            Mathf.Abs(point.x - bounds.SoftMax.x);

        float distanceToBottom =
            Mathf.Abs(point.y - bounds.SoftMin.y);

        float distanceToTop =
            Mathf.Abs(point.y - bounds.SoftMax.y);

        float verticalDistance =
            Mathf.Min(distanceToLeft, distanceToRight);

        float horizontalDistance =
            Mathf.Min(distanceToBottom, distanceToTop);

        return verticalDistance <= horizontalDistance;
    }

    private static Vector2 GetDirectionFromCenter(
        NavigationBounds bounds,
        Vector2 point)
    {
        Vector2 direction =
            point - bounds.Center;

        return direction.sqrMagnitude > Epsilon
            ? direction.normalized
            : Vector2.up;
    }

    private static float GetEdgeCruiseDistance(
        NavigationBounds bounds)
    {
        float minHalfSize =
            Mathf.Min(
                bounds.SoftHalfSize.x,
                bounds.SoftHalfSize.y);

        return Mathf.Clamp(
            minHalfSize * 0.25f,
            50f,
            minHalfSize * 0.6f);
    }

    private readonly struct NavigationBounds
    {
        public NavigationBounds(
            Vector2 center,
            Vector2 halfSize,
            float inset)
        {
            Center = center;

            HalfSize =
                new Vector2(
                    Mathf.Abs(halfSize.x),
                    Mathf.Abs(halfSize.y));

            float safeInset =
                Mathf.Clamp(
                    inset,
                    0f,
                    Mathf.Max(0f, Mathf.Min(HalfSize.x, HalfSize.y) - 1f));

            SoftHalfSize =
                new Vector2(
                    Mathf.Max(1f, HalfSize.x - safeInset),
                    Mathf.Max(1f, HalfSize.y - safeInset));
        }

        public Vector2 Center { get; }

        public Vector2 HalfSize { get; }

        public Vector2 SoftHalfSize { get; }

        public bool IsValid =>
            HalfSize.x >= MinUsableBoundsHalfSize &&
            HalfSize.y >= MinUsableBoundsHalfSize &&
            SoftHalfSize.x > 0f &&
            SoftHalfSize.y > 0f;

        public Vector2 SoftMin =>
            Center - SoftHalfSize;

        public Vector2 SoftMax =>
            Center + SoftHalfSize;

        public bool ContainsSoft(
            Vector2 point)
        {
            Vector2 min = SoftMin;
            Vector2 max = SoftMax;

            return point.x >= min.x &&
                   point.x <= max.x &&
                   point.y >= min.y &&
                   point.y <= max.y;
        }

        public Vector2 ClampToSoft(
            Vector2 point)
        {
            Vector2 min = SoftMin;
            Vector2 max = SoftMax;

            return new Vector2(
                Mathf.Clamp(point.x, min.x, max.x),
                Mathf.Clamp(point.y, min.y, max.y));
        }
    }
}