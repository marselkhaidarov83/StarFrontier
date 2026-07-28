using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Центральный runtime-сервис взаимодействий.
///
/// Читает существующие:
/// - PlayerControlRuntimeState;
/// - TargetingRuntimeState;
/// - InteractionRuntimeState.
///
/// Не является MonoBehaviour.
/// Не размещается на сцене.
/// Не сохраняет игру самостоятельно.
/// </summary>
public sealed class InteractionService2A :
    IInteractionService2A
{
    private readonly
        ISystemGameplayStateService
            _gameplayStateService;

    private readonly ITargetService2A
        _targetService;

    private readonly InteractionConfig
        _config;

    private readonly SimpleEventBus
        _eventBus;

    private readonly IShipMovementService
        _shipMovementService;

    private readonly List<IInteractionHandler2A>
        _handlers;

    private float _holdElapsedSeconds;

    private bool _executedForCurrentHold;

    private string _lastTargetId =
        string.Empty;

    private string _lastActionId =
        string.Empty;

    private bool _lastCanInteract;

    private InteractionFailReason2A
        _lastFailReason =
            InteractionFailReason2A
                .ServiceNotReady;

    public InteractionRuntimeState State =>
        _gameplayStateService.Interaction;

    /// <summary>
    /// Production-конструктор.
    /// </summary>
    public InteractionService2A()
        : this(
            GetRegistry()
                .Get<
                    ISystemGameplayStateService>(),

            GetRegistry()
                .Get<ITargetService2A>(),

            GetRegistry()
                .Get<IConfigService>()
                .InteractionConfig,

            GetRegistry()
                .Get<SimpleEventBus>(),

            GetRegistry()
                .Get<IShipMovementService>(),

            new IInteractionHandler2A[]
            {
                new TravelPointInteractionHandler2A(
                    GetRegistry()
                        .Get<ITravelService>(),

                    GetRegistry()
                        .Get<
                            IGameSessionService>())
            })
    {
    }

    /// <summary>
    /// Конструктор для автоматических тестов.
    /// </summary>
    public InteractionService2A(
        ISystemGameplayStateService
            gameplayStateService,
        ITargetService2A targetService,
        InteractionConfig config,
        SimpleEventBus eventBus,
        IShipMovementService shipMovementService,
        IEnumerable<IInteractionHandler2A>
            handlers)
    {
        _gameplayStateService =
            gameplayStateService ??
            throw new ArgumentNullException(
                nameof(gameplayStateService));

        _targetService =
            targetService ??
            throw new ArgumentNullException(
                nameof(targetService));

        _config =
            config ??
            throw new ArgumentNullException(
                nameof(config));

        _eventBus =
            eventBus ??
            throw new ArgumentNullException(
                nameof(eventBus));

        _shipMovementService =
            shipMovementService;

        _handlers =
            handlers != null
                ? handlers
                    .Where(
                        handler =>
                            handler != null)
                    .ToList()
                : new List<
                    IInteractionHandler2A>();
    }

    public bool CanInteract()
    {
        InteractionFailReason2A reason =
            Evaluate(
                out _,
                out _);

        return
            reason ==
            InteractionFailReason2A.None;
    }

    public InteractionFailReason2A
        GetFailReason()
    {
        return Evaluate(
            out _,
            out _);
    }

    public void RefreshAvailability()
    {
        InteractionFailReason2A reason =
            Evaluate(
                out InteractionDescriptor2A
                    descriptor,
                out _);

        bool canInteract =
            reason ==
            InteractionFailReason2A.None;

        string targetId =
            descriptor != null
                ? descriptor.TargetId
                : string.Empty;

        SystemGameplayTargetType targetType =
            descriptor != null
                ? descriptor.TargetType
                : SystemGameplayTargetType.None;

        State.SetAvailableInteraction(
            targetId,
            targetType,
            canInteract,
            reason == InteractionFailReason2A.None
                ? string.Empty
                : reason.ToString());

        string actionId =
            descriptor != null
                ? descriptor.ActionId
                : string.Empty;

        bool changed =
            targetId != _lastTargetId ||
            actionId != _lastActionId ||
            canInteract != _lastCanInteract ||
            reason != _lastFailReason;

        if (!changed)
            return;

        _lastTargetId = targetId;
        _lastActionId = actionId;
        _lastCanInteract = canInteract;
        _lastFailReason = reason;

        _eventBus.Publish(
            new
                InteractionAvailabilityChangedEvent2A(
                    descriptor,
                    canInteract,
                    reason));
    }

    public void Tick(float deltaTime)
    {
        if (!IsFiniteNonNegative(
                deltaTime))
        {
            throw new ArgumentOutOfRangeException(
                nameof(deltaTime),
                deltaTime,
                "deltaTime must be finite and non-negative.");
        }

        State.ResetFrameFlags();
        State.TickCooldown(deltaTime);

        RefreshAvailability();

        PlayerControlRuntimeState control =
            _gameplayStateService.Control;

        if (control.InteractPressedThisFrame)
        {
            BeginInteraction();
        }

        if (control.InteractHeld &&
            State.IsInteractionInProgress &&
            !_executedForCurrentHold)
        {
            ContinueInteraction(
                deltaTime);
        }

        if (control.InteractReleasedThisFrame)
        {
            EndInteraction();
        }
    }

    public InteractionExecutionResult2A
        Execute()
    {
        InteractionFailReason2A reason =
            Evaluate(
                out InteractionDescriptor2A
                    descriptor,
                out IInteractionHandler2A
                    handler);

        if (reason !=
            InteractionFailReason2A.None)
        {
            return PublishFailedExecution(
                reason,
                descriptor);
        }

        if (_config.PauseShipOnInteraction &&
            _shipMovementService != null)
        {
            _shipMovementService
                .StopImmediately();
        }

        InteractionExecutionResult2A result;

        try
        {
            result =
                handler.Execute(
                    descriptor);
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception);

            result =
                InteractionExecutionResult2A
                    .Failed(
                        InteractionFailReason2A
                            .ExecutionFailed,
                        descriptor,
                        "Ошибка выполнения взаимодействия");
        }

        if (result == null)
        {
            result =
                InteractionExecutionResult2A
                    .Failed(
                        InteractionFailReason2A
                            .ExecutionFailed,
                        descriptor,
                        "Взаимодействие не вернуло результат");
        }

        if (result.Success)
        {
            State.MarkCompleted();

            State.SetCooldown(
                _config
                    .InteractionCooldownSeconds);

            /*
             * TargetService остаётся единственным
             * владельцем изменения target state.
             */
            _targetService.ClearTarget();
        }
        else
        {
            State.MarkFailed(
                result.Message);
        }

        _eventBus.Publish(
            new InteractionExecutionEvent2A(
                result));

        Debug.Log(
            "[InteractionService2A] " +
            "Target = " +
            (descriptor != null
                ? descriptor.TargetId
                : "none") +
            " | Action = " +
            (descriptor != null
                ? descriptor.ActionId
                : "none") +
            " | Success = " +
            result.Success +
            " | Reason = " +
            result.FailReason);

        RefreshAvailability();

        return result;
    }

    private void BeginInteraction()
    {
        State.MarkPressedThisFrame();

        _holdElapsedSeconds = 0f;
        _executedForCurrentHold = false;

        InteractionFailReason2A reason =
            GetFailReason();

        if (State.CooldownRemainingSeconds >
            0f)
        {
            reason =
                InteractionFailReason2A
                    .CooldownActive;
        }

        if (reason !=
            InteractionFailReason2A.None)
        {
            PublishFailedExecution(
                reason,
                null);

            _executedForCurrentHold = true;
            return;
        }

        float holdSeconds =
            Mathf.Max(
                0f,
                _config
                    .HoldToInteractSeconds);

        State.SetInteractionInProgress(
            true,
            holdSeconds <= 0f
                ? 1f
                : 0f);

        if (holdSeconds <= 0f)
        {
            Execute();

            _executedForCurrentHold = true;
        }
    }

    private void ContinueInteraction(
        float deltaTime)
    {
        InteractionFailReason2A reason =
            GetFailReason();

        if (reason !=
            InteractionFailReason2A.None)
        {
            PublishFailedExecution(
                reason,
                null);

            _executedForCurrentHold = true;
            return;
        }

        float holdSeconds =
            Mathf.Max(
                0f,
                _config
                    .HoldToInteractSeconds);

        if (holdSeconds <= 0f)
        {
            Execute();

            _executedForCurrentHold = true;
            return;
        }

        _holdElapsedSeconds +=
            deltaTime;

        float progress =
            Mathf.Clamp01(
                _holdElapsedSeconds /
                holdSeconds);

        State.SetInteractionInProgress(
            true,
            progress);

        if (progress < 1f)
            return;

        Execute();

        _executedForCurrentHold = true;
    }

    private void EndInteraction()
    {
        if (!_executedForCurrentHold)
        {
            State.SetInteractionInProgress(
                false,
                0f);
        }

        _holdElapsedSeconds = 0f;
        _executedForCurrentHold = false;
    }

    private InteractionExecutionResult2A
        PublishFailedExecution(
            InteractionFailReason2A reason,
            InteractionDescriptor2A descriptor)
    {
        if (descriptor == null)
        {
            Evaluate(
                out descriptor,
                out _);
        }

        string message =
            GetPlayerMessage(
                reason);

        State.MarkFailed(
            message);

        InteractionExecutionResult2A result =
            InteractionExecutionResult2A
                .Failed(
                    reason,
                    descriptor,
                    message);

        _eventBus.Publish(
            new InteractionExecutionEvent2A(
                result));

        Debug.LogWarning(
            "[InteractionService2A] " +
            "Interaction rejected. " +
            "Reason = " +
            reason +
            " | Target = " +
            (descriptor != null
                ? descriptor.TargetId
                : "none"));

        return result;
    }

    private InteractionFailReason2A
        Evaluate(
            out InteractionDescriptor2A
                descriptor,
            out IInteractionHandler2A
                handler)
    {
        descriptor = null;
        handler = null;

        TargetingRuntimeState target =
            _gameplayStateService.Targeting;

        if (target == null ||
            !target.HasTarget)
        {
            return
                InteractionFailReason2A
                    .NoTarget;
        }

        if (!target.IsTargetInteractable)
        {
            return
                InteractionFailReason2A
                    .TargetUnavailable;
        }

        handler =
            _handlers.FirstOrDefault(
                candidate =>
                    candidate.CanHandle(
                        target
                            .CurrentTargetType));

        if (handler == null)
        {
            return
                InteractionFailReason2A
                    .UnsupportedTargetType;
        }

        float allowedDistance =
            GetAllowedDistance(
                target.CurrentTargetType);

        if (!IsFiniteNonNegative(
                target
                    .CurrentTargetDistance))
        {
            return
                InteractionFailReason2A
                    .TargetUnavailable;
        }

        if (target.CurrentTargetDistance >
            allowedDistance)
        {
            descriptor =
                handler.CreateDescriptor(
                    target.CurrentTargetId,
                    target.CurrentTargetType);

            return
                InteractionFailReason2A
                    .OutOfRange;
        }

        descriptor =
            handler.CreateDescriptor(
                target.CurrentTargetId,
                target.CurrentTargetType);

        if (descriptor == null)
        {
            return
                InteractionFailReason2A
                    .UnsupportedTargetType;
        }

        return
            handler.GetFailReason(
                descriptor);
    }

    private float GetAllowedDistance(
        SystemGameplayTargetType targetType)
    {
        string typeName =
            targetType
                .ToString();

        if (ContainsIgnoreCase(
                typeName,
                "planet"))
        {
            return
                _config
                    .PlanetInteractionDistance;
        }

        if (ContainsIgnoreCase(
                typeName,
                "station"))
        {
            return
                _config
                    .StationInteractionDistance;
        }

        if (ContainsIgnoreCase(
                typeName,
                "travel") ||
            ContainsIgnoreCase(
                typeName,
                "exit") ||
            ContainsIgnoreCase(
                typeName,
                "route"))
        {
            return
                _config
                    .TravelPointInteractionDistance;
        }

        return 0f;
    }

    private static string GetPlayerMessage(
        InteractionFailReason2A reason)
    {
        switch (reason)
        {
            case InteractionFailReason2A.NoTarget:
                return "Цель не выбрана";

            case InteractionFailReason2A
                .TargetUnavailable:
                return "Цель недоступна";

            case InteractionFailReason2A
                .OutOfRange:
                return "Подлетите ближе";

            case InteractionFailReason2A
                .UnsupportedTargetType:
                return
                    "Для этой цели действие не поддерживается";

            case InteractionFailReason2A
                .RequirementsNotMet:
                return
                    "Условия взаимодействия не выполнены";

            case InteractionFailReason2A
                .InsufficientFuel:
                return "Недостаточно топлива";

            case InteractionFailReason2A
                .CooldownActive:
                return
                    "Действие временно недоступно";

            case InteractionFailReason2A
                .ServiceNotReady:
                return
                    "Сервис взаимодействия недоступен";

            default:
                return
                    "Взаимодействие не выполнено";
        }
    }

    private static bool ContainsIgnoreCase(
        string source,
        string value)
    {
        if (string.IsNullOrEmpty(source) ||
            string.IsNullOrEmpty(value))
        {
            return false;
        }

        return
            source.IndexOf(
                value,
                StringComparison
                    .OrdinalIgnoreCase) >= 0;
    }

    private static bool IsFiniteNonNegative(
        float value)
    {
        return
            !float.IsNaN(value) &&
            !float.IsInfinity(value) &&
            value >= 0f;
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
}