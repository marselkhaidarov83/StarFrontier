using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Отображает CTA, hold progress
/// и причину отказа.
///
/// Не изменяет gameplay State.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class InteractionStatusView2A :
    MonoBehaviour
{
    [Header("Root")]

    [SerializeField]
    private CanvasGroup canvasGroup;

    [Header("Button")]

    [SerializeField]
    private Button interactionButton;

    [SerializeField]
    private Image actionIcon;

    [SerializeField]
    private Image holdProgressImage;

    [Header("Texts")]

    [SerializeField]
    private TMP_Text actionText;

    [SerializeField]
    private TMP_Text reasonText;

    [Header("Feedback")]

    [SerializeField]
    [Min(0.1f)]
    private float feedbackDurationSeconds =
        1.5f;

    private IInteractionService2A
        _interactionService;

    private SimpleEventBus
        _eventBus;

    private bool _subscribed;

    private InteractionFailReason2A
        _currentReason =
            InteractionFailReason2A.NoTarget;

    private string _currentActionName =
        "Взаимодействовать";

    private string _feedbackText =
        string.Empty;

    private float _feedbackUntilTime;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup =
                GetComponent<CanvasGroup>();
        }

        SetVisible(false);
    }

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();

        if (_interactionService != null)
        {
            _interactionService
                .RefreshAvailability();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double resolveMs = 0.0;
        double stateMs = 0.0;
        double uiMs = 0.0;

        bool hasService = false;
        bool feedbackActive = false;
        bool hasTarget = false;

        try
        {
            if (_interactionService == null)
            {
                double phaseStartedAt =
                    Time.realtimeSinceStartupAsDouble;

                ResolveDependencies();

                resolveMs =
                    (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

                return;
            }

            hasService = true;

            double stateStartedAt =
                Time.realtimeSinceStartupAsDouble;

            InteractionRuntimeState state =
                _interactionService.State;

            stateMs =
                (Time.realtimeSinceStartupAsDouble - stateStartedAt) * 1000.0;

            double uiStartedAt =
                Time.realtimeSinceStartupAsDouble;

            if (holdProgressImage != null)
            {
                holdProgressImage.fillAmount =
                    state.HoldProgressNormalized;

                holdProgressImage.enabled =
                    state.IsInteractionInProgress;
            }

            feedbackActive =
                Time.unscaledTime <
                _feedbackUntilTime;

            hasTarget =
                !string.IsNullOrWhiteSpace(
                    state.CurrentInteractionTargetId);

            SetVisible(
                feedbackActive ||
                hasTarget);

            if (interactionButton != null)
            {
                interactionButton.interactable =
                    state.CanInteract &&
                    state.CooldownRemainingSeconds <= 0f;
            }

            if (actionText != null)
            {
                actionText.text =
                    _currentActionName;
            }

            if (reasonText != null)
            {
                reasonText.text =
                    feedbackActive
                        ? _feedbackText
                        : state.CanInteract
                            ? string.Empty
                            : GetReasonText(_currentReason);
            }

            if (actionIcon != null)
            {
                Color color =
                    actionIcon.color;

                color.a =
                    state.CanInteract
                        ? 1f
                        : 0.35f;

                actionIcon.color =
                    color;
            }

            uiMs =
                (Time.realtimeSinceStartupAsDouble - uiStartedAt) * 1000.0;
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            string details =
                "Name=" + name +
                " | HasService=" + hasService +
                " | FeedbackActive=" + feedbackActive +
                " | HasTarget=" + hasTarget +
                " | ResolveMs=" + resolveMs.ToString("F3") +
                " | StateMs=" + stateMs.ToString("F3") +
                " | UiMs=" + uiMs.ToString("F3");

            VisualUpdateAggregateLog.Record(
                "InteractionStatusView2A.Update",
                elapsedMs,
                details);

            VisualUpdatePerfLog.LogIfSlow(
                "InteractionStatusView2A.Update",
                startedAt,
                details);
        }
    }

    private void OnAvailabilityChanged(
        InteractionAvailabilityChangedEvent2A
            evt)
    {
        if (evt == null)
            return;

        _currentReason =
            evt.FailReason;

        _currentActionName =
            evt.Descriptor != null &&
            !string.IsNullOrWhiteSpace(
                evt.Descriptor
                    .ActionDisplayName)
                ? evt.Descriptor
                    .ActionDisplayName
                : "Взаимодействовать";
    }

    private void OnExecution(
        InteractionExecutionEvent2A evt)
    {
        if (evt == null ||
            evt.Result == null)
        {
            return;
        }

        _feedbackText =
            !string.IsNullOrWhiteSpace(
                evt.Result.Message)
                ? evt.Result.Message
                : evt.Result.Success
                    ? "Действие выполнено"
                    : "Действие не выполнено";

        _feedbackUntilTime =
            Time.unscaledTime +
            feedbackDurationSeconds;
    }

    private void ResolveDependencies()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance
                .ServiceRegistry == null)
        {
            return;
        }

        IServiceRegistry registry =
            Bootstrapper.Instance
                .ServiceRegistry;

        _interactionService =
            registry.Get<
                IInteractionService2A>();

        _eventBus =
            registry.Get<SimpleEventBus>();
    }

    private void Subscribe()
    {
        if (_subscribed ||
            _eventBus == null)
        {
            return;
        }

        _eventBus.Subscribe<
            InteractionAvailabilityChangedEvent2A>(
                OnAvailabilityChanged);

        _eventBus.Subscribe<
            InteractionExecutionEvent2A>(
                OnExecution);

        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed ||
            _eventBus == null)
        {
            return;
        }

        _eventBus.Unsubscribe<
            InteractionAvailabilityChangedEvent2A>(
                OnAvailabilityChanged);

        _eventBus.Unsubscribe<
            InteractionExecutionEvent2A>(
                OnExecution);

        _subscribed = false;
    }

    private void SetVisible(
        bool visible)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha =
            visible
                ? 1f
                : 0f;

        canvasGroup.interactable =
            visible;

        canvasGroup.blocksRaycasts =
            visible;
    }

    private static string GetReasonText(
        InteractionFailReason2A reason)
    {
        switch (reason)
        {
            case InteractionFailReason2A
                .NoTarget:
                return string.Empty;

            case InteractionFailReason2A
                .TargetUnavailable:
                return "Цель недоступна";

            case InteractionFailReason2A
                .OutOfRange:
                return "Подлетите ближе";

            case InteractionFailReason2A
                .UnsupportedTargetType:
                return
                    "Действие не поддерживается";

            case InteractionFailReason2A
                .RequirementsNotMet:
                return
                    "Переход недоступен";

            case InteractionFailReason2A
                .InsufficientFuel:
                return
                    "Недостаточно топлива";

            case InteractionFailReason2A
                .CooldownActive:
                return
                    "Подождите";

            case InteractionFailReason2A
                .ServiceNotReady:
                return
                    "Сервис недоступен";

            default:
                return
                    "Действие недоступно";
        }
    }
}