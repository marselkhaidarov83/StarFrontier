using UnityEngine;

/// <summary>
/// Совместимый мост этапа 2А.
///
/// Берёт направление из IPlayerControlService,
/// но двигает существующий ShipMarkerView2 и
/// ISystemTravelService.
///
/// Это позволяет сохранить point/click/touch
/// и добавить клавиатуру без второго владельца
/// движения.
///
/// Разгон, торможение и поворот сюда
/// намеренно не добавляются.
/// </summary>
[DisallowMultipleComponent]
public sealed class MetaSceneKeyboardTravelBridge2A :
    CustomMonoBehaviour
{
    private const float InputThresholdSqrMagnitude = 0.0001f;

    [Header("Scene References")]
    [SerializeField]
    private ShipMarkerView2 shipMarkerView;

    [Header("Constant Speed Movement")]
    [SerializeField]
    [Min(0f)]
    private float speedUnitsPerSecond = 100f;

    [SerializeField]
    private bool cancelPointTravelWhenKeyboardStarts = true;

    [Header("Bounds")]
    [SerializeField]
    private bool clampToRuntimeBounds = true;

    [SerializeField]
    private bool useFallbackBounds = true;

    [SerializeField]
    private Vector2 fallbackBoundsCenter = Vector2.zero;

    [SerializeField]
    private Vector2 fallbackBoundsHalfSize =
        new Vector2(1200f, 2000f);

    [Header("Debug")]
    [SerializeField]
    private bool logInitialization = true;

    private IPlayerControlService _playerControlService;
    private ISystemTravelService _systemTravelService;
    private ISystemGameplayStateService _stateService;
    private IShipMovementService _shipMovementService;

    private bool _keyboardWasActive;
    private bool _blockUntilInputReleased;
    private bool _initializationLogged;
    private bool _errorReported;

    private void Awake()
    {
        ResolveSceneReferences();
        TryResolveServices();
    }

    private void OnEnable()
    {
        ResolveSceneReferences();
        TryResolveServices();
    }

    private void LateUpdate()
    {
        ResolveSceneReferences();

        if (!TryResolveServices())
            return;

        if (shipMarkerView == null)
        {
            ReportErrorOnce(
                "[MetaSceneKeyboardTravelBridge2A] " +
                "ShipMarkerView2 is not assigned.");
            return;
        }

        Vector2 moveInput =
            _playerControlService.RawMoveInput;

        if (!IsFinite(moveInput))
        {
            moveInput = Vector2.zero;
        }

        moveInput =
            Vector2.ClampMagnitude(moveInput, 1f);

        bool hasMoveInput =
            moveInput.sqrMagnitude >=
            InputThresholdSqrMagnitude;

        /*
         * После клика по кораблю движение
         * не возобновится, пока игрок не
         * отпустит клавишу.
         */
        if (_blockUntilInputReleased)
        {
            if (!hasMoveInput)
            {
                _blockUntilInputReleased = false;
            }

            _keyboardWasActive = false;
            return;
        }

        if (!hasMoveInput)
        {
            _keyboardWasActive = false;
            return;
        }

        if (!_keyboardWasActive &&
            cancelPointTravelWhenKeyboardStarts)
        {
            _systemTravelService.CancelTravel();
        }

        _keyboardWasActive = true;

        Vector3 currentPosition3D =
            shipMarkerView.transform.position;

        Vector2 currentPosition =
            new Vector2(
                currentPosition3D.x,
                currentPosition3D.y);

        Vector2 nextPosition =
            ConstantSpeedMovement2A.Step(
                currentPosition,
                moveInput,
                speedUnitsPerSecond,
                Time.deltaTime);

        nextPosition =
            ClampPosition(nextPosition);

        Vector3 nextPosition3D =
            new Vector3(
                nextPosition.x,
                nextPosition.y,
                currentPosition3D.z);

        /*
         * Двигаем существующий визуальный корабль.
         */
        shipMarkerView.SetPosition(nextPosition3D);

        /*
         * Обновляем legacy travel state.
         */
        _systemTravelService.SetCurrentPosition(
            nextPosition3D);

        /*
         * Синхронизируем новый runtime state,
         * но не включаем его Tick.
         */
        if (_shipMovementService != null)
        {
            _shipMovementService.SetPosition(
                nextPosition);
        }
    }

    /// <summary>
    /// Используется кликом по кораблю.
    /// После отмены клавиша должна быть
    /// отпущена перед новым движением.
    /// </summary>
    public void CancelMovementUntilInputReleased()
    {
        _blockUntilInputReleased = true;
        _keyboardWasActive = false;

        if (_playerControlService != null)
        {
            _playerControlService.SetRawMoveInput(
                Vector2.zero);
        }

        if (_systemTravelService != null)
        {
            _systemTravelService.CancelTravel();
        }

        if (_shipMovementService != null)
        {
            _shipMovementService.StopImmediately();
        }
    }

    private Vector2 ClampPosition(
        Vector2 position)
    {
        if (clampToRuntimeBounds &&
            _stateService != null &&
            _stateService.Bounds != null &&
            _stateService.Bounds.IsInitialized &&
            _stateService.Bounds.IsEnabled)
        {
            return _stateService.Bounds.ClampPosition(
                position);
        }

        if (!useFallbackBounds)
            return position;

        Vector2 absoluteHalfSize =
            new Vector2(
                Mathf.Abs(fallbackBoundsHalfSize.x),
                Mathf.Abs(fallbackBoundsHalfSize.y));

        Vector2 min =
            fallbackBoundsCenter -
            absoluteHalfSize;

        Vector2 max =
            fallbackBoundsCenter +
            absoluteHalfSize;

        return new Vector2(
            Mathf.Clamp(position.x, min.x, max.x),
            Mathf.Clamp(position.y, min.y, max.y));
    }

    private void ResolveSceneReferences()
    {
        if (shipMarkerView == null)
        {
            shipMarkerView =
                GetComponentInChildren<ShipMarkerView2>(
                    true);
        }
    }

    private bool TryResolveServices()
    {
        if (_playerControlService != null &&
            _systemTravelService != null)
        {
            LogInitializationOnce();
            return true;
        }

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return false;
        }

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _playerControlService);

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _systemTravelService);

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _stateService);

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _shipMovementService);

        bool isReady =
            _playerControlService != null &&
            _systemTravelService != null;

        if (isReady)
        {
            LogInitializationOnce();
        }

        return isReady;
    }

    private void LogInitializationOnce()
    {
        if (!logInitialization ||
            _initializationLogged)
        {
            return;
        }

        _initializationLogged = true;

        LogCustom(
            "[MetaSceneKeyboardTravelBridge2A] " +
            "Initialized. Constant speed = " +
            speedUnitsPerSecond);
    }

    private void ReportErrorOnce(
        string message)
    {
        if (_errorReported)
            return;

        _errorReported = true;
        Debug.LogError(message, this);
    }

    private void OnDisable()
    {
        _keyboardWasActive = false;
        _blockUntilInputReleased = false;

        if (_playerControlService != null)
        {
            _playerControlService.SetRawMoveInput(
                Vector2.zero);
        }
    }

    private static bool IsFinite(
        Vector2 value)
    {
        return
            !float.IsNaN(value.x) &&
            !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) &&
            !float.IsInfinity(value.y);
    }
}