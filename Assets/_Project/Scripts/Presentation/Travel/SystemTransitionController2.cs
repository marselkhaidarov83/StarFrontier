using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SystemTransitionController2 : CustomMonoBehaviour
{
    [Header("FX")]
    [SerializeField]
    private ArrivalFxView2 systemJumpFlash;

    [Header("Error Message")]
    [SerializeField]
    private Image errorImage;

    [SerializeField]
    private TMP_Text errorText;

    [SerializeField]
    private float showDuration = 2f;

    private SimpleEventBus _eventBus;
    private ISystemTravelService _systemTravelService;
    private ITravelService _travelService;
    private IGameSessionService _gameSessionService;
    private IGameStateMachine _gameStateMachine;

    private Coroutine _currentRoutine;

    private void Start()
    {
        if (Bootstrapper.Instance == null)
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemTransitionController2),
                "Bootstrapper.Instance is null.");

            return;
        }

        if (Bootstrapper.Instance.ServiceRegistry == null)
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemTransitionController2),
                "ServiceRegistry is null.");

            return;
        }

        IServiceRegistry registry =
            Bootstrapper.Instance.ServiceRegistry;

        _eventBus =
            registry.Get<SimpleEventBus>();

        _travelService =
            registry.Get<ITravelService>();

        _systemTravelService =
            registry.Get<ISystemTravelService>();

        _gameSessionService =
            registry.Get<IGameSessionService>();

        _gameStateMachine =
            registry.Get<IGameStateMachine>();

        if (_eventBus == null)
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemTransitionController2),
                "SimpleEventBus not found.");

            return;
        }

        if (_travelService == null)
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemTransitionController2),
                "ITravelService not found.");

            return;
        }

        if (_systemTravelService == null)
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemTransitionController2),
                "ISystemTravelService not found.");

            return;
        }

        if (_gameSessionService == null)
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemTransitionController2),
                "IGameSessionService not found.");

            return;
        }

        if (_gameStateMachine == null)
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemTransitionController2),
                "IGameStateMachine not found.");

            return;
        }

        _eventBus.Subscribe<SystemTravelCompletedEvent>(
            OnTravelCompleted);

        LogCustom(
            "SystemTravelCompletedEvent subscribed");
    }

    private void OnDestroy()
    {
        if (_eventBus != null)
        {
            _eventBus.Unsubscribe<SystemTravelCompletedEvent>(
                OnTravelCompleted);
        }

        if (_currentRoutine != null)
        {
            StopCoroutine(
                _currentRoutine);

            _currentRoutine =
                null;
        }
    }

    private void OnTravelCompleted(
        SystemTravelCompletedEvent evt)
    {
        if (evt.DestinationType != TravelDestinationType.SystemExit)
            return;

        if (_gameSessionService == null ||
            _gameSessionService.State == null ||
            _gameSessionService.State.Player == null)
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemTransitionController2),
                "Player state is not available.");

            return;
        }

        string targetSystemId =
            string.IsNullOrWhiteSpace(evt.TargetSystemId)
                ? string.Empty
                : evt.TargetSystemId.Trim();

        if (IsDebug())
        {
            AppLog.Info(DebugLogChannel.Travel,
                typeof(SystemTransitionController2),
                "System exit reached. Target system: " + targetSystemId);
        }

        string currentSystemId =
            _gameSessionService
                .State
                .Player
                .CurrentSystemId;

        if (string.Equals(
                currentSystemId,
                targetSystemId,
                StringComparison.Ordinal))
        {
            _eventBus.Publish(
                new StarSystemEnteredEvent(
                    targetSystemId));

            return;
        }

        if (_travelService == null)
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemTransitionController2),
                "Cannot perform travel: ITravelService is null.");

            return;
        }

        TravelFailReason failReason =
            _travelService.GetTravelFailReason(
                currentSystemId,
                targetSystemId);

        if (failReason != TravelFailReason.None)
        {
            ShowTravelFailReason(failReason);
            return;
        }

        if (_gameStateMachine == null)
        {
            AppLog.Error(DebugLogChannel.Travel,
                typeof(SystemTransitionController2),
                "Cannot enter target system: IGameStateMachine is null.");

            return;
        }

        LoadingSceneContext.SetSystemTravel(
            _gameSessionService.State);

        _gameStateMachine.Enter(
            new SystemState(targetSystemId));
    }

    // Проверка выбора выхода общая для карты и списка объектов.
    // Топливо здесь не списывается; фактический прыжок проверяет его повторно.
    public bool ValidateSystemExitSelection(string fromSystemId, string toSystemId)
    {
        IServiceRegistry registry = Bootstrapper.Instance != null
            ? Bootstrapper.Instance.ServiceRegistry
            : null;
        if (_travelService == null && registry != null)
            registry.TryGet(out _travelService);

        if (_travelService == null)
        {
            AppLog.Error(DebugLogChannel.Travel,
                "[SystemTransitionController2] Cannot validate exit: ITravelService is missing.");
            return false;
        }

        TravelFailReason failReason = _travelService.GetTravelFailReason(fromSystemId, toSystemId);
        if (failReason != TravelFailReason.NotEnoughFuel)
            return true;

        ShowTravelFailReason(failReason);
        return true;
    }

    /// <summary>
    /// Показывает сообщение для причины отказа перелёта.
    ///
    /// Используется:
    /// - при выборе точки выхода;
    /// - при повторной проверке перед перелётом.
    /// </summary>
    public void ShowTravelFailReason(
        TravelFailReason failReason)
    {
        switch (failReason)
        {
            case TravelFailReason.NotEnoughFuel:
                ShowMessage(
                    "Для прыжка недостаточно топлива");
                break;

            case TravelFailReason.SystemsAreNotNeighbors:
                ShowMessage(
                    "Перелёт в выбранную систему " +
                    "из текущей невозможен");
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Показывает текст через существующий
    /// объект ошибки на SystemScene.
    /// </summary>
    public void ShowMessage(
        string text)
    {
        if (string.IsNullOrWhiteSpace(
                text))
        {
            return;
        }

        if (_currentRoutine != null)
        {
            StopCoroutine(
                _currentRoutine);

            _currentRoutine =
                null;
        }

        _currentRoutine =
            StartCoroutine(
                ShowMessageRoutine(
                    text));
    }

    private IEnumerator ShowMessageRoutine(
        string text)
    {
        if (errorText != null)
        {
            errorText.text =
                text;
        }

        if (errorImage != null)
        {
            errorImage
                .gameObject
                .SetActive(true);
        }
        else if (errorText != null)
        {
            /*
             * Резервный вариант:
             * если фон не назначен,
             * показываем хотя бы объект текста.
             */
            errorText
                .gameObject
                .SetActive(true);
        }

        yield return new WaitForSeconds(
            Mathf.Max(
                0.01f,
                showDuration));

        if (errorImage != null)
        {
            errorImage
                .gameObject
                .SetActive(false);
        }
        else if (errorText != null)
        {
            errorText
                .gameObject
                .SetActive(false);
        }

        _currentRoutine =
            null;
    }
}