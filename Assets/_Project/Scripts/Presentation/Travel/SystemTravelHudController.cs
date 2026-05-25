using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SystemTravelHudController : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text modeText;
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text progressText;

    [Header("Progress")]
    [SerializeField] private RectTransform progressBarFill;
    [SerializeField] private float progressBarMaxWidth = 420f;

    [Header("Buttons")]
    [SerializeField] private Button flyButton;
    [SerializeField] private Button playPauseButton;
    [SerializeField] private Button stepDayButton;

    [Header("Button Labels")]
    [SerializeField] private TMP_Text flyButtonText;
    [SerializeField] private TMP_Text playPauseButtonText;
    [SerializeField] private TMP_Text stepDayButtonText;

    private IGameTimeService _gameTimeService;
    private ISystemTravelService _travelService;
    private SimpleEventBus _eventBus;

    private string _currentTargetLabel = "None";

    private void Start()
    {
        _gameTimeService = Bootstrapper.Instance.ServiceRegistry.Get<IGameTimeService>();
        _travelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        if (_gameTimeService == null)
            Debug.LogError("[SystemTravelHudController] IGameTimeService not found.");

        if (_travelService == null)
            Debug.LogError("[SystemTravelHudController] ISystemTravelService not found.");

        if (_eventBus == null)
            Debug.LogError("[SystemTravelHudController] SimpleEventBus not found.");

        SubscribeButtons();
        SubscribeEvents();

        SetFlyButtonActive(false);
        RefreshAll();
    }

    private void Update()
    {
        RefreshTime();
        RefreshTravelState();
    }

    private void OnDestroy()
    {
        UnsubscribeButtons();
        UnsubscribeEvents();
    }

    private void SubscribeButtons()
    {
        if (flyButton != null)
            flyButton.onClick.AddListener(OnFlyClicked);

        if (playPauseButton != null)
            playPauseButton.onClick.AddListener(OnPlayPauseClicked);

        if (stepDayButton != null)
            stepDayButton.onClick.AddListener(OnStepDayClicked);
    }

    private void UnsubscribeButtons()
    {
        if (flyButton != null)
            flyButton.onClick.RemoveListener(OnFlyClicked);

        if (playPauseButton != null)
            playPauseButton.onClick.RemoveListener(OnPlayPauseClicked);

        if (stepDayButton != null)
            stepDayButton.onClick.RemoveListener(OnStepDayClicked);
    }

    private void SubscribeEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Subscribe<DestinationSelectedEvent>(OnDestinationSelected);
        _eventBus.Subscribe<SystemTravelStartedEvent>(OnTravelStarted);
        _eventBus.Subscribe<SystemTravelProgressChangedEvent>(OnTravelProgressChanged);
        _eventBus.Subscribe<SystemTravelCompletedEvent>(OnTravelCompleted);
        _eventBus.Subscribe<SystemTravelCancelledEvent>(OnTravelCancelled);
    }

    private void UnsubscribeEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Unsubscribe<DestinationSelectedEvent>(OnDestinationSelected);
        _eventBus.Unsubscribe<SystemTravelStartedEvent>(OnTravelStarted);
        _eventBus.Unsubscribe<SystemTravelProgressChangedEvent>(OnTravelProgressChanged);
        _eventBus.Unsubscribe<SystemTravelCompletedEvent>(OnTravelCompleted);
        _eventBus.Unsubscribe<SystemTravelCancelledEvent>(OnTravelCancelled);
    }

    private void OnFlyClicked()
    {
        if (_travelService == null)
            return;

        if (_travelService.State.Status == SystemTravelStatus.Flying)
            return;

        if (!_travelService.State.HasDestination)
            return;

        _travelService.StartTravel();
        SetFlyButtonActive(false);
        RefreshTravelState();
    }

    private void OnPlayPauseClicked()
    {
        if (_gameTimeService == null)
            return;

        _gameTimeService.TogglePause();
        RefreshTime();
    }

    // private void OnStepDayClicked()
    // {
    //     if (_gameTimeService == null)
    //         return;

    //     _gameTimeService.StepOneDay();
    //     RefreshTime();
    // }

    private void OnStepDayClicked()
    {
        if (_gameTimeService == null)
            return;

        // _gameTimeService.StepOneDay();
        _gameTimeService.TogglePause();
        StartCoroutine(Delay(_gameTimeService.DelayTime));

        RefreshTime();
    }

    private IEnumerator Delay(float delay)
    {
        yield return new WaitForSeconds(delay);
        _gameTimeService.TogglePause();
    }    
    private void OnDestinationSelected(DestinationSelectedEvent evt)
    {
        _currentTargetLabel = BuildTargetLabel(evt);

        SetTargetText(_currentTargetLabel);
        SetStatusText("Selected");
        SetProgress(0f);
        SetFlyButtonActive(true);
    }

    private void OnTravelStarted(SystemTravelStartedEvent evt)
    {
        SetStatusText("Flying");
        SetProgress(0f);
        SetFlyButtonActive(false);
    }

    private void OnTravelProgressChanged(SystemTravelProgressChangedEvent evt)
    {
        // Debug.Log("[HUD] Progress event: " + evt.Progress01);
        SetStatusText("Flying");
        SetProgress(evt.Progress01);
    }

    private void OnTravelCompleted(SystemTravelCompletedEvent evt)
    {
        SetStatusText("Arrived");
        SetProgress(1f);
        SetFlyButtonActive(false);

        if (evt.DestinationType == TravelDestinationType.SystemExit)
            _currentTargetLabel = "System Jump";

        if (evt.DestinationType == TravelDestinationType.MapPoint)
            _currentTargetLabel = "Map Point";

        if (evt.DestinationType == TravelDestinationType.Planet && !string.IsNullOrEmpty(evt.PlanetId))
            _currentTargetLabel = evt.PlanetId;

        SetTargetText(_currentTargetLabel);
    }

    private void OnTravelCancelled(SystemTravelCancelledEvent evt)
    {
        _currentTargetLabel = "None";

        SetTargetText(_currentTargetLabel);
        SetStatusText("Cancelled");
        SetProgress(0f);
        SetFlyButtonActive(false);
    }

    private void RefreshAll()
    {
        RefreshTime();
        RefreshTravelState();
        SetTargetText(_currentTargetLabel);
    }

    private void RefreshTime()
    {
        if (_gameTimeService == null)
            return;

        SetTextSafe(dayText, $"QuantTick {_gameTimeService.CurrentQuantTick}");

        if (_gameTimeService.IsPaused)
        {
            SetTextSafe(modeText, "Pause");
            SetTextSafe(playPauseButtonText, "Play");
        }
        else
        {
            SetTextSafe(modeText, "Play");
            SetTextSafe(playPauseButtonText, "Pause");
        }

        SetTextSafe(stepDayButtonText, "Step");
    }

    private void RefreshTravelState()
    {
        if (_travelService == null)
            return;

        SystemTravelState state = _travelService.State;

        switch (state.Status)
        {
            case SystemTravelStatus.Idle:
                if (!state.HasDestination)
                {
                    SetStatusText("Idle");
                    SetFlyButtonActive(false);
                }
                break;

            case SystemTravelStatus.DestinationSelected:
                SetStatusText("Selected");
                SetFlyButtonActive(true);
                break;

            case SystemTravelStatus.Flying:
                SetStatusText("Flying");
                SetFlyButtonActive(false);
                SetProgress(state.TravelProgress01);
                break;

            case SystemTravelStatus.Arrived:
                SetStatusText("Arrived");
                SetFlyButtonActive(false);
                break;

            case SystemTravelStatus.Cancelled:
                SetStatusText("Cancelled");
                SetFlyButtonActive(false);
                break;
        }
    }

    private string BuildTargetLabel(DestinationSelectedEvent evt)
    {
        switch (evt.DestinationType)
        {
            case TravelDestinationType.Planet:
                return string.IsNullOrEmpty(evt.PlanetId)
                    ? "Planet"
                    : evt.PlanetId;

            case TravelDestinationType.MapPoint:
                return $"Point {Mathf.RoundToInt(evt.DestinationPosition.x)}, {Mathf.RoundToInt(evt.DestinationPosition.y)}";

            case TravelDestinationType.SystemExit:
                return string.IsNullOrEmpty(evt.TargetSystemId)
                    ? "System Exit"
                    : $"Jump to {evt.TargetSystemId}";

            default:
                return "None";
        }
    }

    private void SetTargetText(string value)
    {
        SetTextSafe(targetText, $"Target: {value}");
    }

    private void SetStatusText(string value)
    {
        SetTextSafe(statusText, $"Status: {value}");
    }

    // private void SetProgress(float progress01)
    // {
    //     float clamped = Mathf.Clamp01(progress01);

    //     SetTextSafe(progressText, $"{Mathf.RoundToInt(clamped * 100f)}%");

    //     if (progressBarFill != null)
    //     {
    //         Vector2 size = progressBarFill.sizeDelta;
    //         size.x = progressBarMaxWidth * clamped;
    //         progressBarFill.sizeDelta = size;
    //     }
    // }

    private void SetProgress(float progress01)
    {
        float clamped = Mathf.Clamp01(progress01);

        SetTextSafe(progressText, $"{Mathf.RoundToInt(clamped * 100f)}%");

        if (progressBarFill != null)
        {
            progressBarFill.anchorMin = new Vector2(0f, 0f);
            progressBarFill.anchorMax = new Vector2(0f, 1f);
            progressBarFill.pivot = new Vector2(0f, 0.5f);

            Vector2 size = progressBarFill.sizeDelta;
            size.x = progressBarMaxWidth * clamped;
            progressBarFill.sizeDelta = size;
        }
    }
    private void SetFlyButtonActive(bool active)
    {
        if (flyButton != null)
            flyButton.interactable = active;

        SetTextSafe(flyButtonText, "Fly");
    }

    private void SetTextSafe(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value;
    }
}