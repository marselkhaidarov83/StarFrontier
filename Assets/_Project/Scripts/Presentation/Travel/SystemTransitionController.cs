using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SystemTransitionController : CustomMonoBehaviour
{
    [Header("FX")]
    [SerializeField] private ArrivalFxView systemJumpFlash;

    // [Header("Transition")]
    // [SerializeField] private float transitionDelay = 0.35f;

    private SimpleEventBus _eventBus;
    private ISystemTravelService _systemTravelService;
    private ITravelService _travelService;
    private IGameSessionService _gameSessionService;

    private Coroutine currentRoutine;
    [SerializeField] private Image errorImage;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private float showDuration = 2f;

    private void Start()
    {
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _travelService = Bootstrapper.Instance.ServiceRegistry.Get<ITravelService>();
        _systemTravelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();

        if (_eventBus == null)
        {
            Debug.LogError("[SystemTransitionService] SimpleEventBus not found.");
            return;
        }

        if (_systemTravelService == null)
        {
            Debug.LogError("[SystemTransitionService] ISystemTravelService not found.");
            return;
        }

        if (_gameSessionService == null)
        {
            Debug.LogError("[SystemTransitionService] IGameSessionService not found.");
            return;
        }

        _eventBus.Subscribe<SystemTravelCompletedEvent>(OnTravelCompleted);
        Debug.Log("[SystemTransitionService] SystemTravelCompletedEvent subscribed");
    }

    private void OnDestroy()
    {
        if (_eventBus != null)
            _eventBus.Unsubscribe<SystemTravelCompletedEvent>(OnTravelCompleted);
    }

    private void OnTravelCompleted(SystemTravelCompletedEvent evt)
    {
        if (IsDebug())
            Debug.Log($"[SystemTransitionService] Clicked system: {evt.TargetSystemId}");

        var currentSystemId = _gameSessionService.CurrentSave.PlayerProfile.CurrentSystemId;

        if (string.Equals(currentSystemId, evt.TargetSystemId, StringComparison.Ordinal))
        {
            if (IsDebug())
                Debug.Log($"SystemTransitionService: '{evt.TargetSystemId}' is the current system.");
            _eventBus.Publish(new SystemEnteredEvent(evt.TargetSystemId));
            return;
        }

        var result = _travelService.TryTravel(evt.TargetSystemId);
        if (IsDebug())
            Debug.Log($"SystemTransitionService: travel result = {result}");                

        switch (result.FailReason)
        {
            case TravelFailReason.NotEnoughFuel:
                ShowMessage("Для перелёта в выбранную систему недостаточно топлива");
                break;
            case TravelFailReason.SystemsAreNotNeighbors:
                ShowMessage("Перелёт в выбранную систему из текущей невозможен");
                break;
            default:
                break;
        }
    
        
        // Debug.LogError("[SystemTransitionService] evt = " + evt);

        // if (evt.DestinationType != TravelDestinationType.SystemExit)
        //     return;

        // if (evt.SystemLink == null)
        // {
        //     Debug.LogError("[SystemTransitionService] Cannot transition: SystemLink is null.");
        //     return;
        // }

        // Debug.LogError("[SystemTransitionService] Transit to system = " + evt.SystemLink.LinkedSystem.Id);
        // _eventBus.Publish(new SystemEnteredEvent(evt.SystemLink.LinkedSystem.Id));
        // // StartCoroutine(TransitionRoutine(evt.SystemLink, evt.FinalPosition));
    }

    public void ShowMessage(string text)
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(ShowMessageRoutine(text));
    }

    private IEnumerator ShowMessageRoutine(string text)
    {
        if (errorImage != null)
            errorImage.gameObject.SetActive(true);
        
        if (errorText != null)
            errorText.text = text;

        yield return new WaitForSeconds(showDuration);

        if (errorImage != null)
            errorImage.gameObject.SetActive(false);

        currentRoutine = null;
    }    
}