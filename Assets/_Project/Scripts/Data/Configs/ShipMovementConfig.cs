using UnityEngine;

// Конфиг ShipMovementConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(
    fileName = "ShipMovementConfig",
    menuName = "StarFrontier/Configs/Sprint 3/Ship Movement")]
public sealed class ShipMovementConfig : BaseConfig
{
    [Header("Linear Movement")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("Максимальное значение параметра maxSpeed. Используется как верхняя граница диапазона.")]
    private float maxSpeed = 7f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Параметр acceleration. Используется связанными игровыми системами этого конфига.")]
    private float acceleration = 18f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Параметр deceleration. Используется связанными игровыми системами этого конфига.")]
    private float deceleration = 22f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Параметр brakingDeceleration. Используется связанными игровыми системами этого конфига.")]
    private float brakingDeceleration = 30f;

    [Header("Runtime Tuning")]
    [SerializeField]
    [Range(0.01f, 10f)]
    [Tooltip("Скорость для параметра speedMultiplier. Используется при движении или анимации.")]
    private float speedMultiplier = 1f;

    [SerializeField, Min(0.01f)]
    [Tooltip("Параметр offscreenNpcTravelSlowdown. Используется связанными игровыми системами этого конфига.")]
    private float offscreenNpcTravelSlowdown = 1.3f;

    [Header("Rotation")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("Скорость для параметра turnSpeedDegrees. Используется при движении или анимации.")]
    private float turnSpeedDegrees = 240f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Параметр rotationSmoothing. Используется связанными игровыми системами этого конфига.")]
    private float rotationSmoothing = 0.18f;

    [Header("Travel Maneuver Assist")]
    [SerializeField]
    [Range(0.1f, 50f)]
    [Tooltip("Радиус для параметра routeTurnRadiusAdjustmentStepPercent. Используется при расчёте расстояний и зон действия.")]
    private float routeTurnRadiusAdjustmentStepPercent = 5f;

    [SerializeField]
    [Range(0.1f, 50f)]
    [Tooltip("Скорость для параметра routeSpeedAdjustmentStepPercent. Используется при движении или анимации.")]
    private float routeSpeedAdjustmentStepPercent = 2.5f;

    [SerializeField]
    [Range(0.01f, 1f)]
    [Tooltip("Минимальное значение параметра minRouteTurnRadiusAdjustmentFactor. Используется как нижняя граница диапазона.")]
    private float minRouteTurnRadiusAdjustmentFactor = 0.05f;

    [SerializeField]
    [Range(1, 30)]
    [Tooltip("Параметр routeSubstepsPerTick. Используется связанными игровыми системами этого конфига.")]
    private int routeSubstepsPerTick = 10;

    public float SpeedMultiplier => Mathf.Max(0.01f, speedMultiplier);

    [SerializeField]
    [Range(0f, 15f)]
    [Tooltip("Параметр routeStraightExitAngleDegrees. Используется связанными игровыми системами этого конфига.")]
    private float routeStraightExitAngleDegrees = 3f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Минимальное значение параметра minRouteTurnRadiusAbsolute. Используется как нижняя граница диапазона.")]
    private float minRouteTurnRadiusAbsolute = 30f;

    [SerializeField]
    [Range(0, 30)]
    [Tooltip("Параметр movingDestinationRouteRefreshBlockedInitialSlots. Используется связанными игровыми системами этого конфига.")]
    private int movingDestinationRouteRefreshBlockedInitialSlots = 2;

    [SerializeField]
    [Range(1, 60)]
    [Tooltip("Параметр movingDestinationRouteRefreshesPerTick. Используется связанными игровыми системами этого конфига.")]
    private int movingDestinationRouteRefreshesPerTick = 10;
    [SerializeField]
    [Range(0, 10)]
    [Tooltip("Параметр movingDestinationRouteRefreshBlockedInitialTicks. Используется связанными игровыми системами этого конфига.")]
    private int movingDestinationRouteRefreshBlockedInitialTicks = 1;

    [Tooltip("Переключатель offscreenMovingPlanetRouteRefreshEnabled. Включает или выключает соответствующее правило или отображение.")]
    [SerializeField] private bool offscreenMovingPlanetRouteRefreshEnabled = true;
    [Tooltip("Параметр offscreenMovingPlanetRouteRefreshCooldownTicks. Используется связанными игровыми системами этого конфига.")]
    [SerializeField][Min(0)] private int offscreenMovingPlanetRouteRefreshCooldownTicks = 10;
    [Tooltip("Минимальное значение параметра offscreenMovingPlanetTerminalRefreshDistance. Используется как нижняя граница диапазона.")]
    [SerializeField][Min(0f)] private float offscreenMovingPlanetTerminalRefreshDistance = 80f;
    [Tooltip("Минимальное значение параметра offscreenMovingPlanetTerminalRefreshTimeTicks. Используется как нижняя граница диапазона.")]
    [SerializeField][Min(0)] private int offscreenMovingPlanetTerminalRefreshTimeTicks = 1;
    [Tooltip("Максимальное значение параметра offscreenMovingPlanetMaxDestinationDriftBeforeRefresh. Используется как верхняя граница диапазона.")]
    [SerializeField][Min(0f)] private float offscreenMovingPlanetMaxDestinationDriftBeforeRefresh = 300f;

    [Header("Travel Route Classification")]
    [SerializeField]
    [Range(0.1f, 5f)]
    [Tooltip("Радиус для параметра routeNearDistanceTurnRadiusMultiplier. Используется при расчёте расстояний и зон действия.")]
    private float routeNearDistanceTurnRadiusMultiplier = 0.75f;

    [SerializeField]
    [Range(1f, 179f)]
    [Tooltip("Параметр routeForwardSectorAngleDegrees. Используется связанными игровыми системами этого конфига.")]
    private float routeForwardSectorAngleDegrees = 60f;

    [SerializeField]
    [Range(1f, 179f)]
    [Tooltip("Параметр routeBehindSectorAngleDegrees. Используется связанными игровыми системами этого конфига.")]
    private float routeBehindSectorAngleDegrees = 135f;

    [SerializeField]
    [Range(0f, 90f)]
    [Tooltip("Параметр routeBehindSmallTurnAngleToleranceDegrees. Используется связанными игровыми системами этого конфига.")]
    private float routeBehindSmallTurnAngleToleranceDegrees = 15f;

    [Header("Travel Sun Safety")]
    [SerializeField]
    [Range(1f, 3f)]
    [Tooltip("Радиус для параметра sunDestinationForbiddenRadiusMultiplier. Используется при расчёте расстояний и зон действия.")]
    private float sunDestinationForbiddenRadiusMultiplier = 1.2f;

    [SerializeField]
    [Range(0f, 25f)]
    [Tooltip("Параметр sunTangentTolerancePercent. Используется связанными игровыми системами этого конфига.")]
    private float sunTangentTolerancePercent = 5f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Параметр sunAvoidanceRoutePaddingStep. Используется связанными игровыми системами этого конфига.")]
    private float sunAvoidanceRoutePaddingStep = 5f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Максимальное значение параметра sunAvoidanceRoutePaddingMax. Используется как верхняя граница диапазона.")]
    private float sunAvoidanceRoutePaddingMax = 40f;

    [Header("Pseudo 3D")]
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Количество для параметра visualTiltAmount. Используется соответствующей системой при генерации или расчёте.")]
    private float visualTiltAmount = 0.18f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Количество для параметра visualBankAmount. Используется соответствующей системой при генерации или расчёте.")]
    private float visualBankAmount = 0.25f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Скорость для параметра visualTiltReturnSpeed. Используется при движении или анимации.")]
    private float visualTiltReturnSpeed = 8f;

    [Header("Bounds")]
    [SerializeField]
    [Tooltip("Переключатель clampToSystemBounds. Включает или выключает соответствующее правило или отображение.")]
    private bool clampToSystemBounds = true;

    [SerializeField]
    private Vector2 systemBoundsHalfSize = new Vector2(12f, 20f);

    [SerializeField]
    [Range(0f, 0.25f)]
    [Tooltip("Параметр boundaryNavigationInsetPercent. Используется связанными игровыми системами этого конфига.")]
    private float boundaryNavigationInsetPercent = 0.03f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Радиус для параметра boundaryProtectionRadiusWorld. Используется при расчёте расстояний и зон действия.")]
    private float boundaryProtectionRadiusWorld = 1500f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Параметр boundaryProtectionPullStepWorld. Используется связанными игровыми системами этого конфига.")]
    private float boundaryProtectionPullStepWorld = 80f;

    [SerializeField]
    [Tooltip("Переключатель useBoundaryNavigationInsetWorldUnits. Включает или выключает соответствующее правило или отображение.")]
    private bool useBoundaryNavigationInsetWorldUnits;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Параметр boundaryNavigationInsetWorldUnits. Используется связанными игровыми системами этого конфига.")]
    private float boundaryNavigationInsetWorldUnits = 0f;

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

    public float SunDestinationForbiddenRadiusMultiplier =>
        Mathf.Clamp(sunDestinationForbiddenRadiusMultiplier, 1f, 3f);

    public float SunTangentTolerancePercent =>
        Mathf.Clamp(sunTangentTolerancePercent, 0f, 25f);

    public float VisualTiltAmount => visualTiltAmount;
    public float VisualBankAmount => visualBankAmount;
    public float VisualTiltReturnSpeed => visualTiltReturnSpeed;

    public bool ClampToSystemBounds => clampToSystemBounds;
    public Vector2 SystemBoundsHalfSize => systemBoundsHalfSize;
    public float BoundaryNavigationInsetPercent =>
    Mathf.Clamp(boundaryNavigationInsetPercent, 0f, 0.25f);

    public bool UseBoundaryNavigationInsetWorldUnits =>
        useBoundaryNavigationInsetWorldUnits;

    public float BoundaryProtectionRadiusWorld =>
Mathf.Max(0f, boundaryProtectionRadiusWorld);

    public float BoundaryProtectionPullStepWorld =>
        Mathf.Max(0f, boundaryProtectionPullStepWorld);

    public float BoundaryNavigationInsetWorldUnits =>
        Mathf.Max(0f, boundaryNavigationInsetWorldUnits);

    public float OffscreenNpcTravelSlowdown =>
        Mathf.Max(0.01f, offscreenNpcTravelSlowdown);

    public int RouteSubstepsPerTick =>
    Mathf.Clamp(routeSubstepsPerTick, 1, 30);

    public float RouteStraightExitAngleDegrees =>
    Mathf.Clamp(routeStraightExitAngleDegrees, 0f, 15f);

    public float RouteBehindSmallTurnAngleToleranceDegrees =>
    Mathf.Clamp(routeBehindSmallTurnAngleToleranceDegrees, 0f, 90f);

    public bool OffscreenMovingPlanetRouteRefreshEnabled => offscreenMovingPlanetRouteRefreshEnabled;

    public int OffscreenMovingPlanetRouteRefreshCooldownTicks =>
        Mathf.Clamp(offscreenMovingPlanetRouteRefreshCooldownTicks, 0, 600);

    public float OffscreenMovingPlanetTerminalRefreshDistance =>
        Mathf.Max(0f, offscreenMovingPlanetTerminalRefreshDistance);

    public int OffscreenMovingPlanetTerminalRefreshTimeTicks =>
        Mathf.Clamp(offscreenMovingPlanetTerminalRefreshTimeTicks, 0, 60);

    public float OffscreenMovingPlanetMaxDestinationDriftBeforeRefresh =>
        Mathf.Max(0f, offscreenMovingPlanetMaxDestinationDriftBeforeRefresh);

    public float MinRouteTurnRadiusAbsolute =>
    Mathf.Max(0f, minRouteTurnRadiusAbsolute);

    public float SunAvoidanceRoutePaddingStep =>
    Mathf.Max(0f, sunAvoidanceRoutePaddingStep);

    public float SunAvoidanceRoutePaddingMax =>
        Mathf.Max(0f, sunAvoidanceRoutePaddingMax);

    public int MovingDestinationRouteRefreshBlockedInitialSlots =>
        Mathf.Clamp(movingDestinationRouteRefreshBlockedInitialSlots, 0, 30);

    public int MovingDestinationRouteRefreshesPerTick =>
        Mathf.Clamp(movingDestinationRouteRefreshesPerTick, 1, 10);

    public int MovingDestinationRouteRefreshBlockedInitialTicks =>
        Mathf.Clamp(movingDestinationRouteRefreshBlockedInitialTicks, 0, 10);
}
