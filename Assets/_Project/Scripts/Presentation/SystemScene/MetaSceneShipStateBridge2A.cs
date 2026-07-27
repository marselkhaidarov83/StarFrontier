using UnityEngine;

/// <summary>
/// Связывает существующий визуальный корабль MetaScene
/// с новым ShipMovementRuntimeState.
///
/// В S3-16 основной режим:
/// legacy visual is source.
/// То есть корабль продолжает двигаться старой системой,
/// а новый runtime-state повторяет его позицию и направление.
/// </summary>
[DisallowMultipleComponent]
public sealed class MetaSceneShipStateBridge2A : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private ShipMarkerView2 shipMarkerView;
    [SerializeField] private SpriteRenderer shipMarkerImage;

    [Header("Bridge Mode")]
    [Tooltip(
        "true = читать позицию из legacy-визуала и писать в ShipMovementRuntimeState. " +
        "false = читать ShipMovementRuntimeState и двигать legacy-визуал. " +
        "Для S3-16 должно быть true.")]
    [SerializeField] private bool legacyVisualIsSource = true;

    [Header("Runtime State")]
    [SerializeField] private bool writeToShipMovementState = true;
    [SerializeField] private bool writeToPlayerStateEveryFrame = true;

    [Header("Legacy Travel")]
    [SerializeField] private bool writeRuntimeStateBackToSystemTravel = false;

    [Header("Debug")]
    [SerializeField] private bool logMissingServices = false;

    private IGameSessionService _gameSessionService;
    private ISystemTravelService _systemTravelService;
    private IShipMovementService _shipMovementService;

    private void Reset()
    {
        ResolveSceneReferences();
    }

    private void Awake()
    {
        ResolveSceneReferences();
        ResolveServices();
    }

    private void LateUpdate()
    {
        ResolveServicesIfNeeded();

        if (legacyVisualIsSource)
        {
            SyncRuntimeStateFromLegacyVisual();
        }
        else
        {
            SyncLegacyVisualFromRuntimeState();
        }
    }

    public bool HasRequiredSceneReferences()
    {
        return shipMarkerView != null
            && shipMarkerImage != null;
    }

    private void SyncRuntimeStateFromLegacyVisual()
    {
        if (!writeToShipMovementState)
            return;

        if (_shipMovementService == null)
        {
            if (logMissingServices)
            {
                Debug.LogWarning(
                    "[MetaSceneShipStateBridge2A] IShipMovementService not found.",
                    this);
            }

            return;
        }

        if (shipMarkerView == null)
            return;

        Vector3 visualPosition =
            shipMarkerView.transform.position;

        if (!MetaSceneShipTransformUtility2A
                .IsFinite(visualPosition))
        {
            return;
        }

        Vector2 visualDirection =
            ResolveVisualDirection();

        _shipMovementService.SetPosition(
            MetaSceneShipTransformUtility2A
                .ToVector2(visualPosition));

        _shipMovementService.SetFacingDirection(
            visualDirection);

        if (writeToPlayerStateEveryFrame
            && _gameSessionService != null
            && _gameSessionService.HasActiveSession
            && _gameSessionService.State != null
            && _gameSessionService.State.Player != null)
        {
            _shipMovementService.WriteToPlayerState(
                _gameSessionService.State.Player);
        }
    }

    private void SyncLegacyVisualFromRuntimeState()
    {
        if (_shipMovementService == null)
            return;

        ShipMovementRuntimeState movementState =
            _shipMovementService.State;

        if (movementState == null)
            return;

        Vector3 currentVisualPosition =
            shipMarkerView != null
                ? shipMarkerView.transform.position
                : Vector3.zero;

        Vector3 runtimePosition =
            MetaSceneShipTransformUtility2A.ToVector3(
                movementState.Position,
                currentVisualPosition.z);

        Vector2 runtimeDirection =
            MetaSceneShipTransformUtility2A
                .NormalizeDirectionOrUp(
                    movementState.FacingDirection);

        if (shipMarkerView != null)
        {
            shipMarkerView.SetPosition(
                runtimePosition);
        }

        if (shipMarkerImage != null)
        {
            shipMarkerImage.transform.localRotation =
                MetaSceneShipTransformUtility2A
                    .RotationFromDirection(runtimeDirection);
        }

        if (writeRuntimeStateBackToSystemTravel
            && _systemTravelService != null)
        {
            _systemTravelService.SetCurrentPosition(
                runtimePosition);
        }
    }

    private Vector2 ResolveVisualDirection()
    {
        if (shipMarkerImage != null)
        {
            return MetaSceneShipTransformUtility2A
                .DirectionFromRotation(
                    shipMarkerImage.transform.localRotation);
        }

        return Vector2.up;
    }

    private void ResolveSceneReferences()
    {
        if (shipMarkerView == null)
        {
            shipMarkerView =
                GetComponentInChildren<ShipMarkerView2>(
                    true);
        }

        if (shipMarkerImage == null
            && shipMarkerView != null)
        {
            shipMarkerImage =
                shipMarkerView.GetComponentInChildren<SpriteRenderer>(
                    true);
        }
    }

    private void ResolveServicesIfNeeded()
    {
        if (_gameSessionService != null
            && _systemTravelService != null
            && _shipMovementService != null)
        {
            return;
        }

        ResolveServices();
    }

    private void ResolveServices()
    {
        TryResolveService(out _gameSessionService);
        TryResolveService(out _systemTravelService);
        TryResolveService(out _shipMovementService);
    }

    private static bool TryResolveService<TService>(
        out TService service)
        where TService : class
    {
        service = null;

        if (Bootstrapper.Instance == null
            || Bootstrapper.Instance.ServiceRegistry == null)
        {
            return false;
        }

        try
        {
            service =
                Bootstrapper.Instance
                    .ServiceRegistry
                    .Get<TService>();

            return service != null;
        }
        catch
        {
            service = null;
            return false;
        }
    }
}
