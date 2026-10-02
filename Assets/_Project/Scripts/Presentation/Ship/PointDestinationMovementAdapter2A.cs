using UnityEngine;

/// <summary>
/// Преобразует point/click destination
/// в movement intent.
///
/// Компонент не изменяет Transform корабля.
/// Фактическое движение выполняет
/// IShipMovementService.
/// </summary>
[DisallowMultipleComponent]
public sealed class PointDestinationMovementAdapter2A :
    MonoBehaviour
{
    [Header("Arrival")]

    [SerializeField]
    [Min(0.01f)]
    private float arrivalRadius = 3f;

    [SerializeField]
    [Min(0.02f)]
    private float slowDownRadius = 120f;

    [Tooltip(
        "При включении корабль полностью " +
        "останавливается после достижения точки.")]
    [SerializeField]
    private bool stopImmediatelyOnArrival = true;

    [Header("Diagnostics")]

    [SerializeField]
    private bool logCommands = false;

    private IPlayerControlService _controlService;
    private IShipMovementService _movementService;

    private Vector2 _destination;
    private bool _hasDestination;
    private bool _errorReported;

    public bool HasDestination =>
        _hasDestination;

    public Vector2 Destination =>
        _destination;

    private void Update()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double resolveServicesMs = 0.0;
        double movementStateMs = 0.0;
        double setInputMs = 0.0;

        bool completed = false;
        bool cancelledInvalid = false;
        float distance = 0f;

        try
        {
            if (!_hasDestination)
                return;

            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            bool servicesResolved =
                TryResolveServices();

            resolveServicesMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (!servicesResolved)
                return;

            if (_movementService.State == null)
                return;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            Vector2 currentPosition =
                _movementService.State.Position;

            movementStateMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (!IsFinite(currentPosition))
            {
                cancelledInvalid = true;

                Debug.LogError(
                    "[PointDestinationMovementAdapter2A] " +
                    "Movement State contains invalid position.",
                    this);

                CancelDestination(
                    immediateStop: true);

                return;
            }

            Vector2 delta =
                _destination -
                currentPosition;

            distance =
                delta.magnitude;

            if (distance <= arrivalRadius)
            {
                completed = true;
                CompleteDestination();
                return;
            }

            Vector2 direction =
                delta / distance;

            float effectiveSlowDownRadius =
                Mathf.Max(
                    slowDownRadius,
                    arrivalRadius + 0.01f);

            float inputStrength =
                Mathf.Clamp01(
                    distance /
                    effectiveSlowDownRadius);

            inputStrength =
                Mathf.Max(
                    inputStrength,
                    0.1f);

            Vector2 movementIntent =
                direction *
                inputStrength;

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            _controlService.SetRawMoveInput(
                movementIntent);

            setInputMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            string details =
                "Name=" + name +
                " | HasDestination=" + _hasDestination +
                " | Destination=" + _destination +
                " | Distance=" + distance.ToString("F2") +
                " | Completed=" + completed +
                " | CancelledInvalid=" + cancelledInvalid +
                " | ResolveServicesMs=" + resolveServicesMs.ToString("F3") +
                " | MovementStateMs=" + movementStateMs.ToString("F3") +
                " | SetInputMs=" + setInputMs.ToString("F3");

            VisualUpdateAggregateLog.Record(
                "PointDestinationMovementAdapter2A.Update",
                elapsedMs,
                details);

            VisualUpdatePerfLog.LogIfSlow(
                "PointDestinationMovementAdapter2A.Update",
                startedAt,
                details);
        }
    }

    private void OnDisable()
    {
        if (!_hasDestination)
            return;

        CancelDestination(
            immediateStop: false);
    }

    /// <summary>
    /// Используйте этот метод, когда destination
    /// приходит в формате мировой Vector3-позиции.
    /// Координата Z намеренно игнорируется.
    /// </summary>
    public void SetDestination(
        Vector3 worldPosition)
    {
        SetDestination(
            new Vector2(
                worldPosition.x,
                worldPosition.y));
    }

    /// <summary>
    /// Устанавливает новую point/click destination.
    /// </summary>
    public void SetDestination(
        Vector2 worldPosition)
    {
        if (!IsFinite(worldPosition))
        {
            Debug.LogError(
                "[PointDestinationMovementAdapter2A] " +
                "Destination contains NaN or Infinity.",
                this);

            return;
        }

        _destination =
            worldPosition;

        _hasDestination =
            true;

        if (logCommands)
        {
            Debug.Log(
                "[PointDestinationMovementAdapter2A] " +
                $"Destination set: {_destination}.",
                this);
        }
    }

    /// <summary>
    /// Метод для UnityEvent, который не умеет
    /// передавать bool-параметр.
    /// </summary>
    public void CancelDestination()
    {
        CancelDestination(
            immediateStop: false);
    }

    /// <summary>
    /// Отменяет destination.
    ///
    /// immediateStop = false:
    /// очищается intent, затем корабль тормозит
    /// по обычным правилам MovementService.
    ///
    /// immediateStop = true:
    /// дополнительно вызывается StopImmediately().
    /// </summary>
    public void CancelDestination(
        bool immediateStop)
    {
        bool hadDestination =
            _hasDestination;

        _hasDestination =
            false;

        if (TryResolveServices())
        {
            _controlService.SetRawMoveInput(
                Vector2.zero);

            if (immediateStop)
            {
                _movementService
                    .StopImmediately();
            }
        }

        if (hadDestination &&
            logCommands)
        {
            Debug.Log(
                "[PointDestinationMovementAdapter2A] " +
                "Destination cancelled. " +
                $"Immediate stop: {immediateStop}.",
                this);
        }
    }

    /// <summary>
    /// Немедленно отменяет destination
    /// и полностью останавливает корабль.
    /// Удобно подключать к клику по кораблю.
    /// </summary>
    public void CancelAndStopImmediately()
    {
        CancelDestination(
            immediateStop: true);
    }

    private void CompleteDestination()
    {
        _hasDestination =
            false;

        _controlService.SetRawMoveInput(
            Vector2.zero);

        if (stopImmediatelyOnArrival)
        {
            _movementService
                .StopImmediately();
        }

        if (logCommands)
        {
            Debug.Log(
                "[PointDestinationMovementAdapter2A] " +
                "Destination reached.",
                this);
        }
    }

    private bool TryResolveServices()
    {
        if (_controlService != null &&
            _movementService != null)
        {
            return true;
        }

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return false;
        }

        Bootstrapper.Instance
            .ServiceRegistry
            .TryGet(
                out _controlService);

        Bootstrapper.Instance
            .ServiceRegistry
            .TryGet(
                out _movementService);

        bool isReady =
            _controlService != null &&
            _movementService != null;

        if (!isReady &&
            !_errorReported)
        {
            _errorReported = true;

            Debug.LogError(
                "[PointDestinationMovementAdapter2A] " +
                "IPlayerControlService or " +
                "IShipMovementService was not found.",
                this);
        }

        return isReady;
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