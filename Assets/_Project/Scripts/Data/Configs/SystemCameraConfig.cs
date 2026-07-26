using UnityEngine;

public enum SystemCameraProjection2A
{
    Orthographic,
    Perspective
}

[CreateAssetMenu(
    fileName = "SystemCameraConfig",
    menuName = "StarFrontier/Configs/System Camera"
)]
public sealed class SystemCameraConfig :
    ScriptableObject
{
    [Header("Projection")]

    [SerializeField]
    private SystemCameraProjection2A projectionMode =
        SystemCameraProjection2A.Perspective;

    [Tooltip(
        "Z-плоскость, на которой находится системная карта.")]
    [SerializeField]
    private float gameplayPlaneZ = 0f;

    [Header("Perspective")]

    [Range(20f, 60f)]
    [SerializeField]
    private float perspectiveFieldOfView = 42f;

    [Tooltip(
        "Наклон от строго верхнего вида. " +
        "0 — строго сверху.")]
    [Range(0f, 55f)]
    [SerializeField]
    private float perspectiveTiltFromTop = 28f;

    [SerializeField]
    private float perspectiveYaw = 0f;

    [Min(1f)]
    [SerializeField]
    private float defaultPerspectiveDistance = 1250f;

    [Min(1f)]
    [SerializeField]
    private float minPerspectiveDistance = 650f;

    [Min(1f)]
    [SerializeField]
    private float maxPerspectiveDistance = 2200f;

    [Min(0.01f)]
    [SerializeField]
    private float perspectiveZoomUnitsPerInput = 140f;

    [Min(0.01f)]
    [SerializeField]
    private float nearClipPlane = 1f;

    [Min(1f)]
    [SerializeField]
    private float farClipPlane = 10000f;

    [Header("Orthographic Compatibility")]

    [SerializeField]
    private float orthographicCameraZ = -10f;

    [Min(1f)]
    [SerializeField]
    private float defaultOrthographicSize = 1200f;

    [Min(1f)]
    [SerializeField]
    private float minOrthographicSize = 650f;

    [Min(1f)]
    [SerializeField]
    private float maxOrthographicSize = 1900f;

    [Min(0.01f)]
    [SerializeField]
    private float orthographicZoomUnitsPerInput = 100f;

    [Header("Follow")]

    [Min(0.01f)]
    [SerializeField]
    private float followSmoothTime = 0.18f;

    [Min(0.01f)]
    [SerializeField]
    private float returnSmoothTime = 0.12f;

    [Min(0f)]
    [SerializeField]
    private float followDeadZone = 8f;

    [Min(0.01f)]
    [SerializeField]
    private float returnPositionTolerance = 1f;

    [Min(0.01f)]
    [SerializeField]
    private float returnZoomTolerance = 1f;

    [Header("System Bounds")]

    [SerializeField]
    private Vector2 worldBoundsCenter =
        Vector2.zero;

    [SerializeField]
    private Vector2 worldBoundsSize =
        new Vector2(2400f, 4000f);

    [Tooltip(
        "Внутренний отступ от края системной карты.")]
    [Min(0f)]
    [SerializeField]
    private float viewportBoundsInset = 0f;

    /*
     * Старые поля сохранены ради совместимости
     * существующего asset и другого кода.
     */
    [SerializeField]
    private float boundsPadding = 320f;

    [SerializeField]
    private float sunExtraPadding = 220f;

    [SerializeField]
    private float planetExtraPadding = 180f;

    [SerializeField]
    private float stationExtraPadding = 220f;

    [SerializeField]
    private float exitExtraPadding = 220f;

    [Header("Free Look")]

    [Min(0.01f)]
    [SerializeField]
    private float dragSensitivity = 1f;

    [Min(0.01f)]
    [SerializeField]
    private float freeLookSmoothTime = 0.04f;

    [Min(0f)]
    [SerializeField]
    private float freeLookInertia = 0f;

    [Header("Current System Framing")]

    [Tooltip(
        "Минимальная дистанция камеры относительно " +
        "радиуса крупнейшего объекта системы.")]
    [Min(1f)]
    [SerializeField]
    private float largestObjectZoomMultiplier = 4.5f;

    [Header("Zoom")]

    [Min(0.001f)]
    [SerializeField]
    private float zoomSmoothTime = 0.08f;

    public SystemCameraProjection2A ProjectionMode =>
        projectionMode;

    public float GameplayPlaneZ =>
        gameplayPlaneZ;

    public float PerspectiveFieldOfView =>
        Mathf.Clamp(
            perspectiveFieldOfView,
            20f,
            60f);

    public float PerspectiveTiltFromTop =>
        Mathf.Clamp(
            perspectiveTiltFromTop,
            0f,
            55f);

    public float PerspectiveYaw =>
        perspectiveYaw;

    public float DefaultPerspectiveDistance =>
        Mathf.Clamp(
            defaultPerspectiveDistance,
            MinPerspectiveDistance,
            MaxPerspectiveDistance);

    public float MinPerspectiveDistance =>
        Mathf.Max(
            1f,
            minPerspectiveDistance);

    public float MaxPerspectiveDistance =>
        Mathf.Max(
            MinPerspectiveDistance,
            maxPerspectiveDistance);

    public float NearClipPlane =>
        Mathf.Max(
            0.01f,
            nearClipPlane);

    public float FarClipPlane =>
        Mathf.Max(
            NearClipPlane + 1f,
            farClipPlane);

    public float OrthographicCameraZ =>
        orthographicCameraZ;

    public float DefaultOrthographicSize =>
        Mathf.Clamp(
            defaultOrthographicSize,
            MinOrthographicSize,
            MaxOrthographicSize);

    public float MinOrthographicSize =>
        Mathf.Max(
            1f,
            minOrthographicSize);

    public float MaxOrthographicSize =>
        Mathf.Max(
            MinOrthographicSize,
            maxOrthographicSize);

    public float FollowSmoothTime =>
        Mathf.Max(
            0.01f,
            followSmoothTime);

    public float ReturnSmoothTime =>
        Mathf.Max(
            0.01f,
            returnSmoothTime);

    public float FollowDeadZone =>
        Mathf.Max(
            0f,
            followDeadZone);

    public float ReturnPositionTolerance =>
        Mathf.Max(
            0.01f,
            returnPositionTolerance);

    public float ReturnZoomTolerance =>
        Mathf.Max(
            0.01f,
            returnZoomTolerance);

    public float DragSensitivity =>
        Mathf.Max(
            0.01f,
            dragSensitivity);

    public float FreeLookSmoothTime =>
        Mathf.Max(
            0.01f,
            freeLookSmoothTime);

    public float FreeLookInertia =>
        Mathf.Max(
            0f,
            freeLookInertia);

    public float ZoomSmoothTime =>
        Mathf.Max(
            0.001f,
            zoomSmoothTime);

    public float LargestObjectZoomMultiplier =>
        Mathf.Max(
            1f,
            largestObjectZoomMultiplier);

    public float BoundsPadding =>
        Mathf.Max(0f, boundsPadding);

    public float SunExtraPadding =>
        Mathf.Max(0f, sunExtraPadding);

    public float PlanetExtraPadding =>
        Mathf.Max(0f, planetExtraPadding);

    public float StationExtraPadding =>
        Mathf.Max(0f, stationExtraPadding);

    public float ExitExtraPadding =>
        Mathf.Max(0f, exitExtraPadding);

    public float DefaultZoom =>
        projectionMode ==
        SystemCameraProjection2A.Perspective
            ? DefaultPerspectiveDistance
            : DefaultOrthographicSize;

    public float MinZoom =>
        projectionMode ==
        SystemCameraProjection2A.Perspective
            ? MinPerspectiveDistance
            : MinOrthographicSize;

    public float MaxZoom =>
        projectionMode ==
        SystemCameraProjection2A.Perspective
            ? MaxPerspectiveDistance
            : MaxOrthographicSize;

    public float ZoomUnitsPerInput =>
        projectionMode ==
        SystemCameraProjection2A.Perspective
            ? Mathf.Max(
                0.01f,
                perspectiveZoomUnitsPerInput)
            : Mathf.Max(
                0.01f,
                orthographicZoomUnitsPerInput);

    public Rect WorldBoundsRect
    {
        get
        {
            float safeWidth =
                Mathf.Max(
                    1f,
                    worldBoundsSize.x);

            float safeHeight =
                Mathf.Max(
                    1f,
                    worldBoundsSize.y);

            float maximumInset =
                Mathf.Min(
                    safeWidth,
                    safeHeight) *
                0.49f;

            float safeInset =
                Mathf.Clamp(
                    viewportBoundsInset,
                    0f,
                    maximumInset);

            Vector2 finalSize =
                new Vector2(
                    safeWidth -
                    safeInset * 2f,
                    safeHeight -
                    safeInset * 2f);

            return new Rect(
                worldBoundsCenter -
                finalSize * 0.5f,
                finalSize);
        }
    }

    private void OnValidate()
    {
        minPerspectiveDistance =
            Mathf.Max(
                1f,
                minPerspectiveDistance);

        maxPerspectiveDistance =
            Mathf.Max(
                minPerspectiveDistance,
                maxPerspectiveDistance);

        defaultPerspectiveDistance =
            Mathf.Clamp(
                defaultPerspectiveDistance,
                minPerspectiveDistance,
                maxPerspectiveDistance);

        minOrthographicSize =
            Mathf.Max(
                1f,
                minOrthographicSize);

        maxOrthographicSize =
            Mathf.Max(
                minOrthographicSize,
                maxOrthographicSize);

        defaultOrthographicSize =
            Mathf.Clamp(
                defaultOrthographicSize,
                minOrthographicSize,
                maxOrthographicSize);

        worldBoundsSize.x =
            Mathf.Max(
                1f,
                worldBoundsSize.x);

        worldBoundsSize.y =
            Mathf.Max(
                1f,
                worldBoundsSize.y);

        farClipPlane =
            Mathf.Max(
                nearClipPlane + 1f,
                farClipPlane);
    }
}