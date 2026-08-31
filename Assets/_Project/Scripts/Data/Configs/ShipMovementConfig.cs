using UnityEngine;

[CreateAssetMenu(
    fileName = "ShipMovementConfig",
    menuName = "StarFrontier/Configs/Sprint 3/Ship Movement")]
public sealed class ShipMovementConfig : ScriptableObject
{
    [Header("Linear Movement")]
    [SerializeField]
    [Min(0f)]
    private float maxSpeed = 7f;

    [SerializeField]
    [Min(0f)]
    private float acceleration = 18f;

    [SerializeField]
    [Min(0f)]
    private float deceleration = 22f;

    [SerializeField]
    [Min(0f)]
    private float brakingDeceleration = 30f;

    [Header("Rotation")]
    [SerializeField]
    [Min(0f)]
    private float turnSpeedDegrees = 240f;

    [SerializeField]
    [Range(0f, 1f)]
    private float rotationSmoothing = 0.18f;

    [Header("Travel Maneuver Assist")]
    [SerializeField]
    [Range(0.1f, 50f)]
    private float routeTurnRadiusAdjustmentStepPercent = 5f;

    [SerializeField]
    [Range(0.1f, 50f)]
    private float routeSpeedAdjustmentStepPercent = 2.5f;

    [SerializeField]
    [Range(0.01f, 1f)]
    private float minRouteTurnRadiusAdjustmentFactor = 0.05f;

    [Header("Travel Route Classification")]
    [SerializeField]
    [Range(0.1f, 5f)]
    private float routeNearDistanceTurnRadiusMultiplier = 0.75f;

    [SerializeField]
    [Range(1f, 179f)]
    private float routeForwardSectorAngleDegrees = 60f;

    [SerializeField]
    [Range(1f, 179f)]
    private float routeBehindSectorAngleDegrees = 135f;

    [Header("Pseudo 3D")]
    [SerializeField]
    [Range(0f, 1f)]
    private float visualTiltAmount = 0.18f;

    [SerializeField]
    [Range(0f, 1f)]
    private float visualBankAmount = 0.25f;

    [SerializeField]
    [Min(0f)]
    private float visualTiltReturnSpeed = 8f;

    [Header("Bounds")]
    [SerializeField]
    private bool clampToSystemBounds = true;

    [SerializeField]
    private Vector2 systemBoundsHalfSize = new Vector2(12f, 20f);

    public float MaxSpeed => maxSpeed;
    public float Acceleration => acceleration;
    public float Deceleration => deceleration;
    public float BrakingDeceleration => brakingDeceleration;

    public float TurnSpeedDegrees => turnSpeedDegrees;
    public float RotationSmoothing => rotationSmoothing;
    public float RouteTurnRadiusAdjustmentStepPercent =>
        Mathf.Clamp(routeTurnRadiusAdjustmentStepPercent, 0.1f, 50f);

    public float RouteSpeedAdjustmentStepPercent =>
        Mathf.Clamp(routeSpeedAdjustmentStepPercent, 0.1f, 50f);

    public float MinRouteTurnRadiusAdjustmentFactor =>
        Mathf.Clamp(minRouteTurnRadiusAdjustmentFactor, 0.01f, 1f);

    public float RouteNearDistanceTurnRadiusMultiplier =>
        Mathf.Clamp(routeNearDistanceTurnRadiusMultiplier, 0.1f, 5f);

    public float RouteForwardSectorAngleDegrees =>
        Mathf.Clamp(routeForwardSectorAngleDegrees, 1f, 179f);

    public float RouteBehindSectorAngleDegrees =>
        Mathf.Clamp(routeBehindSectorAngleDegrees, 1f, 179f);

    public float VisualTiltAmount => visualTiltAmount;
    public float VisualBankAmount => visualBankAmount;
    public float VisualTiltReturnSpeed => visualTiltReturnSpeed;

    public bool ClampToSystemBounds => clampToSystemBounds;
    public Vector2 SystemBoundsHalfSize => systemBoundsHalfSize;
}
