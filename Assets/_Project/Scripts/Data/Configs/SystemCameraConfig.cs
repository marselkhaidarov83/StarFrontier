using UnityEngine;

[CreateAssetMenu(
    fileName = "SystemCameraConfig",
    menuName = "StarFrontier/Configs/Sprint 3/System Camera")]
public sealed class SystemCameraConfig : ScriptableObject
{
    [Header("Camera")]
    [SerializeField]
    [Min(1f)]
    private float orthographicSize = 6.5f;

    [SerializeField]
    [Min(0f)]
    private float followSmoothTime = 0.18f;

    [SerializeField]
    private Vector2 followOffset = new Vector2(0f, 1.2f);

    [Header("Look Ahead")]
    [SerializeField]
    private bool useLookAhead = true;

    [SerializeField]
    [Min(0f)]
    private float lookAheadDistance = 1.4f;

    [SerializeField]
    [Min(0f)]
    private float lookAheadSmoothTime = 0.25f;

    [Header("Bounds")]
    [SerializeField]
    private bool clampCameraToSystemBounds = true;

    [SerializeField]
    private Vector2 cameraBoundsHalfSize = new Vector2(14f, 22f);

    [Header("Shake")]
    [SerializeField]
    [Min(0f)]
    private float defaultShakeDuration = 0.18f;

    [SerializeField]
    [Min(0f)]
    private float defaultShakeAmplitude = 0.12f;

    public float OrthographicSize => orthographicSize;
    public float FollowSmoothTime => followSmoothTime;
    public Vector2 FollowOffset => followOffset;

    public bool UseLookAhead => useLookAhead;
    public float LookAheadDistance => lookAheadDistance;
    public float LookAheadSmoothTime => lookAheadSmoothTime;

    public bool ClampCameraToSystemBounds => clampCameraToSystemBounds;
    public Vector2 CameraBoundsHalfSize => cameraBoundsHalfSize;

    public float DefaultShakeDuration => defaultShakeDuration;
    public float DefaultShakeAmplitude => defaultShakeAmplitude;
}
