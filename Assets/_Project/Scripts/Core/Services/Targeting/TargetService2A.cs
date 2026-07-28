using System;
using UnityEngine;

/// <summary>
/// Единственный сервис, который изменяет
/// TargetingRuntimeState.
///
/// Не является MonoBehaviour.
/// Не размещается на сцене.
/// Не записывает цель в Save.
/// </summary>
public sealed class TargetService2A :
    ITargetService2A
{
    private readonly ISystemGameplayStateService
        _gameplayStateService;

    private readonly TargetingConfig
        _targetingConfig;

    private readonly SimpleEventBus
        _eventBus;

    public TargetingRuntimeState State =>
        _gameplayStateService.Targeting;

    public bool HasTarget =>
        State.HasTarget;

    /// <summary>
    /// Production-конструктор.
    /// Используется Bootstrapper.
    /// </summary>
    public TargetService2A()
        : this(
            GetGameplayStateService(),
            GetTargetingConfig(),
            GetEventBus())
    {
    }

    /// <summary>
    /// Конструктор с явными зависимостями.
    /// Используется автоматическими тестами.
    /// </summary>
    public TargetService2A(
        ISystemGameplayStateService gameplayStateService,
        TargetingConfig targetingConfig,
        SimpleEventBus eventBus)
    {
        _gameplayStateService =
            gameplayStateService ??
            throw new ArgumentNullException(
                nameof(gameplayStateService));

        _targetingConfig =
            targetingConfig ??
            throw new ArgumentNullException(
                nameof(targetingConfig));

        _eventBus =
            eventBus ??
            throw new ArgumentNullException(
                nameof(eventBus));
    }

    public bool TrySelectTarget(
        string targetId,
        SystemGameplayTargetType targetType,
        Vector2 worldPosition,
        bool isInteractable,
        bool isAvailable,
        bool wasSelectedByPlayer,
        out TargetSelectionFailReason2A failReason)
    {
        failReason =
            ValidateTarget(
                targetId,
                targetType,
                worldPosition,
                isAvailable);

        if (failReason !=
            TargetSelectionFailReason2A.None)
        {
            return false;
        }

        float distance =
            Vector2.Distance(
                _gameplayStateService
                    .Movement
                    .Position,
                worldPosition);

        if (!IsFinite(distance))
        {
            failReason =
                TargetSelectionFailReason2A
                    .InvalidWorldPosition;

            return false;
        }

        bool isInRange =
            distance <=
            _targetingConfig
                .MaxTargetDistance;

        _gameplayStateService
            .Targeting
            .SetTarget(
                targetId.Trim(),
                targetType,
                worldPosition,
                distance,
                isInteractable,
                isInRange,
                wasSelectedByPlayer);

        _eventBus.Publish(
            new TargetChangedEvent2A(
                targetId.Trim(),
                targetType,
                true));

        failReason =
            TargetSelectionFailReason2A.None;

        return true;
    }

    public bool TryRefreshCurrentTarget(
        string targetId,
        Vector2 worldPosition,
        bool isInteractable,
        bool isAvailable,
        out TargetSelectionFailReason2A failReason)
    {
        if (!IsCurrentTarget(
                targetId))
        {
            failReason =
                TargetSelectionFailReason2A
                    .TargetIsNotCurrent;

            return false;
        }

        if (!isAvailable)
        {
            ClearTarget();

            failReason =
                TargetSelectionFailReason2A
                    .TargetUnavailable;

            return false;
        }

        if (!IsFinite(
                worldPosition))
        {
            ClearTarget();

            failReason =
                TargetSelectionFailReason2A
                    .InvalidWorldPosition;

            return false;
        }

        float distance =
            Vector2.Distance(
                _gameplayStateService
                    .Movement
                    .Position,
                worldPosition);

        if (!IsFinite(distance))
        {
            ClearTarget();

            failReason =
                TargetSelectionFailReason2A
                    .InvalidWorldPosition;

            return false;
        }

        bool isInRange =
            distance <=
            _targetingConfig
                .MaxTargetDistance;

        /*
         * Повторно записывается snapshot,
         * потому что планета или другой объект
         * может двигаться.
         *
         * Событие здесь не публикуется,
         * чтобы не создавать событие каждый кадр.
         */
        _gameplayStateService
            .Targeting
            .SetTarget(
                State.CurrentTargetId,
                State.CurrentTargetType,
                worldPosition,
                distance,
                isInteractable,
                isInRange,
                State.WasSelectedByPlayer);

        failReason =
            TargetSelectionFailReason2A.None;

        return true;
    }

    public bool IsCurrentTarget(
        string targetId)
    {
        if (!State.HasTarget)
            return false;

        if (string.IsNullOrWhiteSpace(
                targetId))
        {
            return false;
        }

        return string.Equals(
            State.CurrentTargetId,
            targetId.Trim(),
            StringComparison.Ordinal);
    }

    public void ClearTarget()
    {
        if (!State.HasTarget)
            return;

        State.ClearTarget();

        _eventBus.Publish(
            new TargetChangedEvent2A(
                string.Empty,
                SystemGameplayTargetType.None,
                false));
    }

    private TargetSelectionFailReason2A
        ValidateTarget(
            string targetId,
            SystemGameplayTargetType targetType,
            Vector2 worldPosition,
            bool isAvailable)
    {
        if (string.IsNullOrWhiteSpace(
                targetId))
        {
            return
                TargetSelectionFailReason2A
                    .EmptyTargetId;
        }

        if (targetType ==
            SystemGameplayTargetType.None)
        {
            return
                TargetSelectionFailReason2A
                    .InvalidTargetType;
        }

        if (!IsFinite(
                worldPosition))
        {
            return
                TargetSelectionFailReason2A
                    .InvalidWorldPosition;
        }

        if (!isAvailable)
        {
            return
                TargetSelectionFailReason2A
                    .TargetUnavailable;
        }

        if (!IsTargetTypeEnabled(
                targetType))
        {
            return
                TargetSelectionFailReason2A
                    .TargetTypeDisabled;
        }

        return
            TargetSelectionFailReason2A.None;
    }

    /// <summary>
    /// Не использует конкретные имена всех enum-значений,
    /// поэтому остаётся совместимым с текущим enum проекта.
    /// </summary>
    private bool IsTargetTypeEnabled(
        SystemGameplayTargetType targetType)
    {
        string typeName =
            targetType
                .ToString()
                .ToLowerInvariant();

        if (typeName.Contains(
                "planet"))
        {
            return
                _targetingConfig
                    .AllowPlanets;
        }

        if (typeName.Contains(
                "station"))
        {
            return
                _targetingConfig
                    .AllowStations;
        }

        if (typeName.Contains("travel") ||
            typeName.Contains("exit") ||
            typeName.Contains("route"))
        {
            return
                _targetingConfig
                    .AllowTravelPoints;
        }

        if (typeName.Contains(
                "enemy"))
        {
            return
                _targetingConfig
                    .AllowEnemies;
        }

        /*
         * Неизвестный, но валидный тип
         * не блокируется автоматически.
         */
        return true;
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

    private static IServiceRegistry
        GetRegistry()
    {
        if (Bootstrapper.Instance == null)
        {
            throw new InvalidOperationException(
                "Bootstrapper.Instance is null.");
        }

        if (Bootstrapper.Instance
                .ServiceRegistry == null)
        {
            throw new InvalidOperationException(
                "ServiceRegistry is null.");
        }

        return
            Bootstrapper.Instance
                .ServiceRegistry;
    }

    private static
        ISystemGameplayStateService
        GetGameplayStateService()
    {
        return
            GetRegistry()
                .Get<
                    ISystemGameplayStateService>();
    }

    private static TargetingConfig
        GetTargetingConfig()
    {
        IConfigService configService =
            GetRegistry()
                .Get<IConfigService>();

        return
            configService
                .TargetingConfig;
    }

    private static SimpleEventBus
        GetEventBus()
    {
        return
            GetRegistry()
                .Get<SimpleEventBus>();
    }
}