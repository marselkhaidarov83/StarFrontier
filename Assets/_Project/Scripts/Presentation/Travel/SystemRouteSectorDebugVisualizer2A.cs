using System.Collections.Generic;
using UnityEngine;

public sealed class SystemRouteSectorDebugVisualizer2A : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool visible = true;
    [SerializeField] private bool rebuildEveryFrame = true;
    [SerializeField] private bool logDebug;
    [SerializeField] private float refreshInterval = 0.25f;

    [Header("Sampling")]
    [SerializeField] private float debugRadius = 700f;
    [SerializeField] private int rings = 7;
    [SerializeField] private int samplesPerRing = 96;
    [SerializeField] private float pointWorldSize = 10f;

    [Header("Lines")]
    [SerializeField] private bool showBoundaryLines = true;
    [SerializeField] private float lineWidth = 3f;
    [SerializeField] private int circleSegments = 128;

    [Header("Render")]
    [SerializeField] private string sortingLayerName = "System Object";
    [SerializeField] private int sortingOrder = 10000;
    [SerializeField] private float zOffset = -0.25f;

    private readonly List<SpriteRenderer> _points = new List<SpriteRenderer>();
    private readonly List<LineRenderer> _lines = new List<LineRenderer>();

    private ISystemTravelService _travelService;
    private Sprite _pointSprite;
    private Material _lineMaterial;
    private float _nextRefreshTime;
    private bool _loggedMissingService;

    private void Awake()
    {
        _pointSprite = CreatePointSprite();
        _lineMaterial = new Material(Shader.Find("Sprites/Default"));

        TryResolveServices();
    }

    private void Start()
    {
        Rebuild();
    }

    private void OnDestroy()
    {
        if (_lineMaterial != null)
            Destroy(_lineMaterial);

        if (_pointSprite != null)
            Destroy(_pointSprite.texture);
    }

    private void Update()
    {
        if (!visible)
        {
            HideAll();
            return;
        }

        if (_travelService == null)
            TryResolveServices();

        if (_travelService == null)
            return;

        if (!rebuildEveryFrame &&
            Time.unscaledTime < _nextRefreshTime)
        {
            return;
        }

        _nextRefreshTime =
            Time.unscaledTime + Mathf.Max(0.02f, refreshInterval);

        Rebuild();
    }

    private void TryResolveServices()
    {
        if (_travelService != null)
            return;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            if (logDebug && !_loggedMissingService)
            {
                Debug.LogWarning("[RouteSectorDebug] Bootstrapper is not ready.");
                _loggedMissingService = true;
            }

            return;
        }

        _travelService =
            Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();

        if (_travelService == null)
        {
            if (logDebug && !_loggedMissingService)
            {
                Debug.LogWarning("[RouteSectorDebug] ISystemTravelService not found.");
                _loggedMissingService = true;
            }

            return;
        }

        _loggedMissingService = false;

        if (logDebug)
            Debug.Log("[RouteSectorDebug] ISystemTravelService resolved.");
    }

    private void Rebuild()
    {
        if (_travelService == null ||
            _travelService.State == null)
        {
            return;
        }

        SystemRouteSectorDebugInfo2A centerInfo =
            _travelService.GetRouteSectorDebugInfo2A(
                _travelService.State.GetCurrentPosition());

        Vector3 shipPosition =
            centerInfo.ShipPosition;

        int safeRings =
            Mathf.Max(1, rings);

        int safeSamplesPerRing =
            Mathf.Max(12, samplesPerRing);

        int pointCount =
            safeRings * safeSamplesPerRing;

        EnsurePointPool(pointCount);

        int pointIndex = 0;

        for (int ring = 1; ring <= safeRings; ring++)
        {
            float radius =
                debugRadius * ring / safeRings;

            for (int sample = 0; sample < safeSamplesPerRing; sample++)
            {
                float angle =
                    360f * sample / safeSamplesPerRing;

                Vector2 direction =
                    DirectionFromAngle(angle);

                Vector3 targetPosition =
                    shipPosition +
                    new Vector3(
                        direction.x * radius,
                        direction.y * radius,
                        0f);

                SystemRouteSectorDebugInfo2A info =
                    _travelService.GetRouteSectorDebugInfo2A(
                        targetPosition);

                SpriteRenderer point =
                    _points[pointIndex];

                point.gameObject.SetActive(true);

                point.transform.position =
                    new Vector3(
                        targetPosition.x,
                        targetPosition.y,
                        shipPosition.z + zOffset);

                point.transform.localScale =
                    Vector3.one * pointWorldSize;

                point.color =
                    GetColor(info.DestinationCase);

                pointIndex++;
            }
        }

        for (int i = pointIndex; i < _points.Count; i++)
            _points[i].gameObject.SetActive(false);

        if (showBoundaryLines)
        {
            DrawBoundaries(
                centerInfo,
                shipPosition);
        }
        else
        {
            HideLines();
        }

        if (logDebug)
        {
            Debug.Log(
                "[RouteSectorDebug] Drawn. Points = " +
                pointIndex +
                " | ShipPosition = " +
                shipPosition +
                " | CaseAtShip = " +
                centerInfo.DestinationCase);
        }
    }

    private void DrawBoundaries(
        SystemRouteSectorDebugInfo2A info,
        Vector3 shipPosition)
    {
        EnsureLinePool(16);

        int index = 0;

        Vector2 facingDirection =
            NormalizeOrUp(info.ShipFacingDirection);

        DrawCircle(
            _lines[index++],
            shipPosition,
            info.NearDistanceThreshold,
            new Color(1f, 1f, 1f, 0.55f));

        DrawRay(
            _lines[index++],
            shipPosition,
            facingDirection,
            debugRadius,
            new Color(0.15f, 0.85f, 1f, 0.95f));

        DrawRay(
            _lines[index++],
            shipPosition,
            Rotate(facingDirection, info.ForwardSectorAngleDegrees),
            debugRadius,
            new Color(0.1f, 1f, 0.25f, 0.85f));

        DrawRay(
            _lines[index++],
            shipPosition,
            Rotate(facingDirection, -info.ForwardSectorAngleDegrees),
            debugRadius,
            new Color(0.1f, 1f, 0.25f, 0.85f));

        float behindHalfAngle =
            Mathf.Max(
                0f,
                180f - info.BehindSectorAngleDegrees);

        DrawRay(
            _lines[index++],
            shipPosition,
            Rotate(-facingDirection, behindHalfAngle),
            debugRadius,
            new Color(1f, 0.2f, 0.15f, 0.9f));

        DrawRay(
            _lines[index++],
            shipPosition,
            Rotate(-facingDirection, -behindHalfAngle),
            debugRadius,
            new Color(1f, 0.2f, 0.15f, 0.9f));

        if (info.HasSun)
        {
            DrawCircle(
                _lines[index++],
                info.SunCenter,
                info.SunForbiddenRadius,
                new Color(1f, 0f, 0f, 0.95f));

            DrawCircle(
                _lines[index++],
                info.SunCenter,
                info.SunSafetyRadius,
                new Color(1f, 0.55f, 0f, 0.75f));

            Vector2 radialAway =
                new Vector2(
                    shipPosition.x - info.SunCenter.x,
                    shipPosition.y - info.SunCenter.y);

            radialAway =
                NormalizeOrRight(radialAway);

            Vector2 tangentA =
                new Vector2(
                    -radialAway.y,
                    radialAway.x);

            Vector2 tangentB =
                new Vector2(
                    radialAway.y,
                    -radialAway.x);

            DrawRay(
                _lines[index++],
                shipPosition,
                radialAway,
                debugRadius * 0.45f,
                new Color(0.2f, 0.75f, 1f, 0.95f));

            DrawRay(
                _lines[index++],
                shipPosition,
                -radialAway,
                debugRadius * 0.45f,
                new Color(1f, 0.2f, 0.85f, 0.95f));

            DrawRay(
                _lines[index++],
                shipPosition,
                tangentA,
                debugRadius * 0.45f,
                new Color(0.7f, 0.35f, 1f, 0.95f));

            DrawRay(
                _lines[index++],
                shipPosition,
                tangentB,
                debugRadius * 0.45f,
                new Color(0.7f, 0.35f, 1f, 0.95f));

            DrawRay(
                _lines[index++],
                shipPosition,
                Rotate(tangentA, info.SunTangentToleranceAngleDegrees),
                debugRadius * 0.35f,
                new Color(0.7f, 0.35f, 1f, 0.45f));

            DrawRay(
                _lines[index++],
                shipPosition,
                Rotate(tangentA, -info.SunTangentToleranceAngleDegrees),
                debugRadius * 0.35f,
                new Color(0.7f, 0.35f, 1f, 0.45f));

            DrawRay(
                _lines[index++],
                shipPosition,
                Rotate(tangentB, info.SunTangentToleranceAngleDegrees),
                debugRadius * 0.35f,
                new Color(0.7f, 0.35f, 1f, 0.45f));

            DrawRay(
                _lines[index++],
                shipPosition,
                Rotate(tangentB, -info.SunTangentToleranceAngleDegrees),
                debugRadius * 0.35f,
                new Color(0.7f, 0.35f, 1f, 0.45f));
        }

        for (int i = index; i < _lines.Count; i++)
            _lines[i].gameObject.SetActive(false);
    }

    private void EnsurePointPool(int count)
    {
        while (_points.Count < count)
        {
            GameObject pointObject =
                new GameObject("RouteSectorDebugPoint");

            pointObject.transform.SetParent(transform, false);

            SpriteRenderer renderer =
                pointObject.AddComponent<SpriteRenderer>();

            renderer.sprite = _pointSprite;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;

            _points.Add(renderer);
        }
    }

    private void EnsureLinePool(int count)
    {
        while (_lines.Count < count)
        {
            GameObject lineObject =
                new GameObject("RouteSectorDebugLine");

            lineObject.transform.SetParent(transform, false);

            LineRenderer line =
                lineObject.AddComponent<LineRenderer>();

            line.useWorldSpace = true;
            line.material = _lineMaterial;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.sortingLayerName = sortingLayerName;
            line.sortingOrder = sortingOrder + 1;

            _lines.Add(line);
        }
    }

    private void DrawCircle(
        LineRenderer line,
        Vector3 center,
        float radius,
        Color color)
    {
        if (line == null ||
            radius <= 0f)
        {
            return;
        }

        line.gameObject.SetActive(true);
        line.loop = true;
        line.positionCount = Mathf.Max(16, circleSegments);
        line.startColor = color;
        line.endColor = color;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;

        for (int i = 0; i < line.positionCount; i++)
        {
            float angle =
                360f * i / line.positionCount;

            Vector2 direction =
                DirectionFromAngle(angle);

            line.SetPosition(
                i,
                new Vector3(
                    center.x + direction.x * radius,
                    center.y + direction.y * radius,
                    center.z + zOffset));
        }
    }

    private void DrawRay(
        LineRenderer line,
        Vector3 origin,
        Vector2 direction,
        float length,
        Color color)
    {
        if (line == null)
            return;

        direction =
            NormalizeOrUp(direction);

        line.gameObject.SetActive(true);
        line.loop = false;
        line.positionCount = 2;
        line.startColor = color;
        line.endColor = color;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;

        line.SetPosition(
            0,
            new Vector3(
                origin.x,
                origin.y,
                origin.z + zOffset));

        line.SetPosition(
            1,
            new Vector3(
                origin.x + direction.x * length,
                origin.y + direction.y * length,
                origin.z + zOffset));
    }

    private void HideAll()
    {
        for (int i = 0; i < _points.Count; i++)
            _points[i].gameObject.SetActive(false);

        HideLines();
    }

    private void HideLines()
    {
        for (int i = 0; i < _lines.Count; i++)
            _lines[i].gameObject.SetActive(false);
    }

    private static Color GetColor(
        string destinationCase)
    {
        switch (destinationCase)
        {
            case "ForbiddenDestination":
                return new Color(1f, 0f, 0f, 0.95f);

            case "MovingTarget":
                return new Color(0.15f, 0.75f, 1f, 0.9f);

            case "SunBlockedNearForward":
            case "SunBlockedFarForward":
                return new Color(0.1f, 1f, 0.25f, 0.78f);

            case "SunBlockedNearSide":
            case "SunBlockedFarSide":
                return new Color(1f, 0.9f, 0.1f, 0.78f);

            case "SunBlockedNearBehind":
            case "SunBlockedFarBehind":
                return new Color(1f, 0.2f, 0.15f, 0.82f);

            case "NearSunStartAwayNearForward":
            case "NearSunStartAwayFarForward":
            case "NearSunStartAwayNearSide":
            case "NearSunStartAwayFarSide":
            case "NearSunStartAwayNearBehind":
            case "NearSunStartAwayFarBehind":
                return new Color(0.1f, 0.75f, 1f, 0.88f);

            case "NearSunStartTangentNearForward":
            case "NearSunStartTangentFarForward":
            case "NearSunStartTangentNearSideAway":
            case "NearSunStartTangentFarSideAway":
            case "NearSunStartTangentNearBehind":
            case "NearSunStartTangentFarBehind":
                return new Color(0.65f, 0.35f, 1f, 0.88f);

            case "NearSunStartTowardNearForward":
            case "NearSunStartTowardFarForward":
            case "NearSunStartTowardNearSide":
            case "NearSunStartTowardFarSide":
            case "NearSunStartTowardNearBehind":
            case "NearSunStartTowardFarBehind":
                return new Color(1f, 0.25f, 0.85f, 0.88f);

            case "NearSunStartExit":
                return new Color(1f, 0.55f, 0f, 0.9f);

            default:
                return new Color(1f, 1f, 1f, 0.35f);
        }
    }

    private static Sprite CreatePointSprite()
    {
        const int size = 16;

        Texture2D texture =
            new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false);

        texture.name = "RouteSectorDebugPointTexture";

        Color clear =
            new Color(1f, 1f, 1f, 0f);

        Color white =
            Color.white;

        float center =
            (size - 1) * 0.5f;

        float radius =
            size * 0.45f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                texture.SetPixel(
                    x,
                    y,
                    distance <= radius
                        ? white
                        : clear);
            }
        }

        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
    }

    private static Vector2 DirectionFromAngle(float angleDegrees)
    {
        float radians =
            angleDegrees * Mathf.Deg2Rad;

        return new Vector2(
            Mathf.Cos(radians),
            Mathf.Sin(radians));
    }

    private static Vector2 Rotate(
        Vector2 direction,
        float angleDegrees)
    {
        float radians =
            angleDegrees * Mathf.Deg2Rad;

        float sin =
            Mathf.Sin(radians);

        float cos =
            Mathf.Cos(radians);

        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos);
    }

    private static Vector2 NormalizeOrUp(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return Vector2.up;

        return direction.normalized;
    }

    private static Vector2 NormalizeOrRight(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return Vector2.right;

        return direction.normalized;
    }
}