using UnityEngine;

/// <summary>
/// Отображает состояние ShipMovementService2A
/// на визуальном Transform корабля.
///
/// Компонент:
/// - не рассчитывает движение;
/// - не изменяет Movement State;
/// - не изменяет PlayerState;
/// - не сохраняет игру;
/// - не вызывает Tick;
/// - не обрабатывает input.
///
/// Единственная ответственность:
///
/// IShipMovementService.State
///     ↓
/// Transform корабля.
/// </summary>
[DefaultExecutionOrder(900)]
[DisallowMultipleComponent]
public sealed class ShipMovementViewBinder2A :
    MonoBehaviour
{
    [Header("View")]

    [Tooltip(
        "Корневой Transform изображения корабля. " +
        "Назначайте объект ShipMarkerView2, " +
        "а не дочерний компонент Image.")]
    [SerializeField]
    private Transform shipView;

    [Header("Rendering")]

    [SerializeField]
    private bool applyPosition = true;

    [SerializeField]
    private bool applyRotation = true;

    [Tooltip(
        "Сохраняет исходную координату Z визуального объекта. " +
        "Movement State содержит только X и Y.")]
    [SerializeField]
    private bool preserveInitialZ = true;

    [Header("Diagnostics")]

    [SerializeField]
    private bool logSuccessfulBinding = true;

    [SerializeField]
    private bool logMissingService = true;

    private IShipMovementService _movementService;

    private float _initialWorldZ;
    private Quaternion _initialLocalRotation;

    private bool _viewInitialized;
    private bool _successfulBindingLogged;
    private bool _missingServiceLogged;

    public Transform ShipView =>
        shipView;

    public bool IsBound =>
        _movementService != null;

    private void Reset()
    {
        /*
         * Автоматическое назначение допустимо,
         * когда компонент добавлен непосредственно
         * на корень визуального объекта корабля.
         */
        shipView = transform;
    }

    private void Awake()
    {
        InitializeViewReference();
    }

    private void OnEnable()
    {
        InitializeViewReference();
        TryResolveMovementService();
    }

    private void LateUpdate()
    {
        if (!TryResolveMovementService())
            return;

        RenderMovementState();
    }

    /// <summary>
    /// Позволяет назначить View из другого
    /// presentation-компонента без изменения State.
    /// </summary>
    public void SetShipView(
        Transform targetView)
    {
        shipView = targetView;
        _viewInitialized = false;

        InitializeViewReference();
    }

    /// <summary>
    /// Немедленно синхронизирует картинку со State.
    /// State при этом не изменяется.
    /// </summary>
    public void RefreshViewImmediately()
    {
        if (!TryResolveMovementService())
            return;

        RenderMovementState();
    }

    private void InitializeViewReference()
    {
        if (_viewInitialized)
            return;

        if (shipView == null)
        {
            /*
             * Не ищем произвольный объект по имени:
             * неправильная автоматическая ссылка опаснее
             * явного сообщения об ошибке.
             */
            return;
        }

        _initialWorldZ =
            shipView.position.z;

        _initialLocalRotation =
            shipView.localRotation;

        _viewInitialized =
            true;
    }

    private bool TryResolveMovementService()
    {
        if (_movementService != null)
            return true;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return false;
        }

        bool resolved =
            Bootstrapper.Instance
                .ServiceRegistry
                .TryGet<IShipMovementService>(
                    out _movementService);

        if (!resolved ||
            _movementService == null)
        {
            if (logMissingService &&
                !_missingServiceLogged)
            {
                _missingServiceLogged = true;

                Debug.LogError(
                    "[ShipMovementViewBinder2A] " +
                    "IShipMovementService was not found " +
                    "in ServiceRegistry.",
                    this);
            }

            return false;
        }

        _missingServiceLogged =
            false;

        if (logSuccessfulBinding &&
            !_successfulBindingLogged)
        {
            _successfulBindingLogged = true;

            Debug.Log(
                "[ShipMovementViewBinder2A] " +
                "View is bound to IShipMovementService.State.",
                this);
        }

        return true;
    }

    private void RenderMovementState()
    {
        if (shipView == null)
        {
            Debug.LogError(
                "[ShipMovementViewBinder2A] " +
                "Ship View is not assigned.",
                this);

            enabled = false;
            return;
        }

        InitializeViewReference();

        ShipMovementRuntimeState state =
            _movementService.State;

        if (state == null)
            return;

        if (applyPosition)
        {
            ApplyStatePosition(
                state.Position);
        }

        if (applyRotation)
        {
            ApplyStateRotation(
                state.FacingDirection,
                state.RotationDegrees);
        }
    }

    private void ApplyStatePosition(
        Vector2 statePosition)
    {
        if (!IsFinite(statePosition))
        {
            Debug.LogError(
                "[ShipMovementViewBinder2A] " +
                "Movement State contains invalid position.",
                this);

            return;
        }

        Vector3 visualPosition =
            shipView.position;

        visualPosition.x =
            statePosition.x;

        visualPosition.y =
            statePosition.y;

        if (preserveInitialZ)
        {
            visualPosition.z =
                _initialWorldZ;
        }

        shipView.position =
            visualPosition;
    }

    private void ApplyStateRotation(
        Vector2 facingDirection,
        float rotationDegrees)
    {
        if (!IsFinite(rotationDegrees))
            return;

        /*
         * В Movement State угол считается:
         *
         * 0°   = вверх;
         * 90°  = вправо;
         * 180° = вниз;
         * 270° = влево.
         *
         * Это угол по часовой стрелке.
         *
         * Положительный Z-поворот Unity идёт
         * против часовой стрелки, поэтому знак
         * необходимо инвертировать.
         */
        float visualRotationZ =
            -rotationDegrees;

        shipView.localRotation =
            _initialLocalRotation *
            Quaternion.Euler(
                0f,
                0f,
                visualRotationZ);
    }

    private static bool IsFinite(
        Vector2 value)
    {
        return
            IsFinite(value.x) &&
            IsFinite(value.y);
    }

    private static bool IsFinite(
        float value)
    {
        return
            !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }
}