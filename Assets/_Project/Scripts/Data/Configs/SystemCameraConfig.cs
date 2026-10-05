using UnityEngine;

[CreateAssetMenu(
    fileName = "SystemCameraConfig",
    menuName = "StarFrontier/Configs/Game/System Camera"
)]
public sealed class SystemCameraConfig : ScriptableObject
{
    [Header("Follow")]
    [SerializeField] private float followSmoothTime = 0.18f;
    [SerializeField] private float returnSmoothTime = 0.12f;
    [SerializeField] private float followDeadZone = 8f;

    [Header("View Size")]
    [SerializeField] private float defaultOrthographicSize = 1200f;
    [SerializeField] private float minOrthographicSize = 650f;
    [SerializeField] private float maxOrthographicSize = 1900f;

    [Header("New Game Start Frame")]
    [SerializeField] private bool useNewGameStartFrame = true;
    [SerializeField] [Range(0.01f, 0.49f)] private float startFrameShipBottomViewportPercent = 0.2f;
    [SerializeField] [Range(0.51f, 0.99f)] private float startFrameSunBottomViewportPercent = 0.8f;
    [SerializeField] [Min(1f)] private float startFrameMinOrthographicSize = 1f;

    [Header("System Bounds")]
    [SerializeField] private float boundsPadding = 320f;
    [SerializeField] private float sunExtraPadding = 220f;
    [SerializeField] private float planetExtraPadding = 180f;
    [SerializeField] private float stationExtraPadding = 220f;
    [SerializeField] private float exitExtraPadding = 220f;

    [Header("Free Look")]
    [SerializeField] private float dragSensitivity = 1f;
    [SerializeField] private float freeLookInertia = 0f;

    public float FollowSmoothTime => Mathf.Max(0.01f, followSmoothTime);
    public float ReturnSmoothTime => Mathf.Max(0.01f, returnSmoothTime);
    public float FollowDeadZone => Mathf.Max(0f, followDeadZone);

    public float DefaultOrthographicSize => Mathf.Max(1f, defaultOrthographicSize);
    public float MinOrthographicSize => Mathf.Max(1f, minOrthographicSize);
    public float MaxOrthographicSize => Mathf.Max(MinOrthographicSize, maxOrthographicSize);
    public bool UseNewGameStartFrame => useNewGameStartFrame;
    public float StartFrameShipBottomViewportPercent =>
        Mathf.Clamp(startFrameShipBottomViewportPercent, 0.01f, 0.49f);
    public float StartFrameSunBottomViewportPercent =>
        Mathf.Clamp(startFrameSunBottomViewportPercent, 0.51f, 0.99f);
    public float StartFrameMinOrthographicSize =>
        Mathf.Max(1f, startFrameMinOrthographicSize);

    public float BoundsPadding => Mathf.Max(0f, boundsPadding);
    public float SunExtraPadding => Mathf.Max(0f, sunExtraPadding);
    public float PlanetExtraPadding => Mathf.Max(0f, planetExtraPadding);
    public float StationExtraPadding => Mathf.Max(0f, stationExtraPadding);
    public float ExitExtraPadding => Mathf.Max(0f, exitExtraPadding);

    public float DragSensitivity => Mathf.Max(0.01f, dragSensitivity);
    public float FreeLookInertia => Mathf.Max(0f, freeLookInertia);
}
