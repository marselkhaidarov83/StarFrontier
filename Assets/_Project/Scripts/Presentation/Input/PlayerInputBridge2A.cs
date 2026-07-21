using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Мост между Unity Input System / touch UI
/// и существующим IPlayerControlService.
///
/// Поддерживает:
/// - keyboard;
/// - mouse как UI pointer;
/// - touch UI.
///
/// Не поддерживает hardware Gamepad или Joystick.
/// Не рассчитывает движение и не вызывает Tick().
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerInputBridge2A : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField]
    private InputActionAsset inputActions;

    [SerializeField]
    private string actionMapName = "Player";

    [SerializeField]
    private string moveActionName = "Move";

    [SerializeField]
    private string interactActionName = "Interact";

    [SerializeField]
    private string recenterActionName = "RecenterCamera";

    [Header("Input Blocking")]
    [Tooltip(
        "Панели, при открытии которых корабль не должен " +
        "получать команды движения.")]
    [SerializeField]
    private GameObject[] blockingUiRoots;

    [SerializeField]
    private bool enableInputOnStart = true;

    [SerializeField]
    private bool logInitialization = true;

    private IPlayerControlService _playerControlService;

    private InputActionMap _playerActionMap;
    private InputAction _moveAction;
    private InputAction _interactAction;
    private InputAction _recenterAction;

    private Vector2 _keyboardMoveInput;
    private Vector2 _touchMoveInput;

    private bool _keyboardInteractionHeld;
    private bool _touchInteractionHeld;

    private bool _manualInputEnabled;
    private bool _initialized;
    private bool _subscribed;
    private bool _mapEnabledByThisBridge;
    private bool _wasBlocked;
    private bool _errorReported;

    public bool IsInitialized => _initialized;

    private void Awake()
    {
        _manualInputEnabled = enableInputOnStart;
    }

    private void OnEnable()
    {
        TryInitialize();
    }

    private void Update()
    {
        if (!_initialized)
        {
            TryInitialize();
            return;
        }

        bool isBlocked = IsInputBlocked();

        if (isBlocked == _wasBlocked)
            return;

        _wasBlocked = isBlocked;

        if (isBlocked)
        {
            _touchMoveInput = Vector2.zero;

            _playerControlService.SetRawMoveInput(
                Vector2.zero);

            CancelAllInteraction();
            return;
        }

        _keyboardMoveInput =
            _moveAction.ReadValue<Vector2>();

        PushMoveInput();
    }

    private void OnDisable()
    {
        ShutdownBridge();
    }

    /// <summary>
    /// Вызывается экранной сенсорной зоной движения.
    /// </summary>
    public void SetTouchMoveInput(Vector2 value)
    {
        _touchMoveInput =
            IsFinite(value)
                ? Vector2.ClampMagnitude(value, 1f)
                : Vector2.zero;

        PushMoveInput();
    }

    public void ClearTouchMoveInput()
    {
        _touchMoveInput = Vector2.zero;
        PushMoveInput();
    }

    public void PressInteractFromTouch()
    {
        if (!TryInitialize() || IsInputBlocked())
            return;

        SetTouchInteractionHeld(true);
    }

    public void ReleaseInteractFromTouch()
    {
        if (!_initialized)
            return;

        SetTouchInteractionHeld(false);
    }

    public void PressRecenterCameraFromUi()
    {
        if (!TryInitialize() || IsInputBlocked())
            return;

        _playerControlService.PressRecenterCamera();
    }

    public void SetGameplayInputEnabled(bool isEnabled)
    {
        _manualInputEnabled = isEnabled;

        if (!_initialized)
            return;

        if (IsInputBlocked())
        {
            _touchMoveInput = Vector2.zero;

            _playerControlService.SetRawMoveInput(
                Vector2.zero);

            CancelAllInteraction();
            return;
        }

        _keyboardMoveInput =
            _moveAction.ReadValue<Vector2>();

        PushMoveInput();
    }

    private bool TryInitialize()
    {
        if (_initialized)
            return true;

        if (inputActions == null)
        {
            ReportErrorOnce(
                "[PlayerInputBridge2A] InputActions is not assigned.");

            return false;
        }

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return false;
        }

        if (!Bootstrapper.Instance.ServiceRegistry.TryGet(
                out _playerControlService))
        {
            return false;
        }

        _playerActionMap =
            inputActions.FindActionMap(
                actionMapName,
                false);

        if (_playerActionMap == null)
        {
            ReportErrorOnce(
                $"[PlayerInputBridge2A] Action Map " +
                $"'{actionMapName}' was not found.");

            return false;
        }

        _moveAction =
            _playerActionMap.FindAction(
                moveActionName,
                false);

        _interactAction =
            _playerActionMap.FindAction(
                interactActionName,
                false);

        _recenterAction =
            _playerActionMap.FindAction(
                recenterActionName,
                false);

        if (_moveAction == null ||
            _interactAction == null ||
            _recenterAction == null)
        {
            ReportErrorOnce(
                "[PlayerInputBridge2A] Required actions " +
                "Move, Interact or RecenterCamera were not found.");

            return false;
        }

        Subscribe();

        if (!_playerActionMap.enabled)
        {
            _playerActionMap.Enable();
            _mapEnabledByThisBridge = true;
        }

        _initialized = true;
        _wasBlocked = IsInputBlocked();

        _keyboardMoveInput =
            _moveAction.ReadValue<Vector2>();

        PushMoveInput();

        if (logInitialization)
        {
            Debug.Log(
                "[PlayerInputBridge2A] Initialized: " +
                "keyboard, mouse UI and touch UI.",
                this);
        }

        return true;
    }

    private void Subscribe()
    {
        if (_subscribed)
            return;

        _moveAction.performed += OnMoveChanged;
        _moveAction.canceled += OnMoveCancelled;

        _interactAction.started += OnInteractStarted;
        _interactAction.canceled += OnInteractCancelled;

        _recenterAction.performed += OnRecenterPerformed;

        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;

        if (_moveAction != null)
        {
            _moveAction.performed -= OnMoveChanged;
            _moveAction.canceled -= OnMoveCancelled;
        }

        if (_interactAction != null)
        {
            _interactAction.started -= OnInteractStarted;
            _interactAction.canceled -= OnInteractCancelled;
        }

        if (_recenterAction != null)
        {
            _recenterAction.performed -= OnRecenterPerformed;
        }

        _subscribed = false;
    }

    private void OnMoveChanged(
        InputAction.CallbackContext context)
    {
        _keyboardMoveInput =
            context.ReadValue<Vector2>();

        PushMoveInput();
    }

    private void OnMoveCancelled(
        InputAction.CallbackContext context)
    {
        _keyboardMoveInput = Vector2.zero;
        PushMoveInput();
    }

    private void OnInteractStarted(
        InputAction.CallbackContext context)
    {
        if (IsInputBlocked())
            return;

        SetKeyboardInteractionHeld(true);
    }

    private void OnInteractCancelled(
        InputAction.CallbackContext context)
    {
        SetKeyboardInteractionHeld(false);
    }

    private void OnRecenterPerformed(
        InputAction.CallbackContext context)
    {
        if (IsInputBlocked())
            return;

        _playerControlService.PressRecenterCamera();
    }

    private void PushMoveInput()
    {
        if (!_initialized ||
            _playerControlService == null)
        {
            return;
        }

        if (IsInputBlocked())
        {
            _playerControlService.SetRawMoveInput(
                Vector2.zero);

            return;
        }

        // Touch имеет приоритет, пока игрок держит
        // экранную сенсорную зону.
        Vector2 selectedInput =
            _touchMoveInput.sqrMagnitude > 0f
                ? _touchMoveInput
                : _keyboardMoveInput;

        _playerControlService.SetRawMoveInput(
            Vector2.ClampMagnitude(selectedInput, 1f));
    }

    private void SetKeyboardInteractionHeld(bool isHeld)
    {
        SetInteractionSource(
            ref _keyboardInteractionHeld,
            isHeld);
    }

    private void SetTouchInteractionHeld(bool isHeld)
    {
        SetInteractionSource(
            ref _touchInteractionHeld,
            isHeld);
    }

    private void SetInteractionSource(
        ref bool source,
        bool newValue)
    {
        if (source == newValue)
            return;

        bool wasHeld =
            _keyboardInteractionHeld ||
            _touchInteractionHeld;

        source = newValue;

        bool isHeldNow =
            _keyboardInteractionHeld ||
            _touchInteractionHeld;

        if (!wasHeld && isHeldNow)
        {
            _playerControlService.PressInteract();
        }
        else if (wasHeld && !isHeldNow)
        {
            _playerControlService.ReleaseInteract();
        }
    }

    private void CancelAllInteraction()
    {
        bool wasHeld =
            _keyboardInteractionHeld ||
            _touchInteractionHeld;

        _keyboardInteractionHeld = false;
        _touchInteractionHeld = false;

        if (wasHeld &&
            _playerControlService != null)
        {
            _playerControlService.ReleaseInteract();
        }
    }

    private bool IsInputBlocked()
    {
        if (!_manualInputEnabled)
            return true;

        if (blockingUiRoots == null)
            return false;

        foreach (GameObject root in blockingUiRoots)
        {
            if (root != null &&
                root.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private void ShutdownBridge()
    {
        if (_playerControlService != null)
        {
            _playerControlService.SetRawMoveInput(
                Vector2.zero);

            CancelAllInteraction();
        }

        _keyboardMoveInput = Vector2.zero;
        _touchMoveInput = Vector2.zero;

        Unsubscribe();

        if (_mapEnabledByThisBridge &&
            _playerActionMap != null &&
            _playerActionMap.enabled)
        {
            _playerActionMap.Disable();
        }

        _mapEnabledByThisBridge = false;
        _initialized = false;
    }

    private void ReportErrorOnce(string message)
    {
        if (_errorReported)
            return;

        _errorReported = true;
        Debug.LogError(message, this);
    }

    private static bool IsFinite(Vector2 value)
    {
        return
            !float.IsNaN(value.x) &&
            !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) &&
            !float.IsInfinity(value.y);
    }
}