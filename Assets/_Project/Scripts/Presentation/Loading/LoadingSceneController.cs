using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class LoadingSceneController : CustomMonoBehaviour
{
    [SerializeField] private LoadingProgressConfig progressConfig;
    public static LoadingSceneController Active { get; private set; }

    private int _observedTimerVersion = -1;
    private int _observedProgressVersion = -1;
    private int _lastLoggedProgressSecond = -1;
    private bool _registeredSceneTimer;


    [Header("Background")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite[] gameStartImages;
    [SerializeField] private Sprite[] systemTravelImages;

    [Header("Progress")]
    [SerializeField] private Slider progressSlider;

    [FormerlySerializedAs("sceneShowSeconds")]
    [SerializeField, Min(0f)] private float gameStartShowSeconds = 20f;

    [SerializeField, Min(0f)] private float systemTravelShowSeconds = 3f;

    [Header("Loading Work")]
    [SerializeField, Min(1)] private int offlineRelocationMaxNpcsPerSlice = 250;
    [SerializeField, Min(0.1f)] private float offlineRelocationMaxSliceMs = 8f;

    [Header("Texts")]
    [SerializeField] private TMP_Text bottomText;
    [SerializeField] private string[] gameStartTexts;
    [SerializeField] private string[] systemTravelTexts;
    [SerializeField, Min(0.1f)] private float textChangeSeconds = 1f;

    private string[] _activeTexts;
    private int _lastTextIndex = -1;
    private float _displayedProgress;

    public bool IsMinimumShowTimeComplete =>
        LoadingSceneContext.IsMinimumShowTimeComplete;

    private void Awake()
    {
        Active = this;

        SelectActiveTexts();
        ApplyRandomBackground();
        ConfigureProgressImage();
        UpdateText(force: true);

        LogState("Awake");
    }

    private void OnEnable()
    {
        ConfigureProgressImage();
        LogState("OnEnable");
    }

    private void OnDestroy()
    {
        LogState("OnDestroy");

        if (Active == this)
            Active = null;
    }

    private void Update()
    {
        EnsureSceneTimerRegistered();
        SyncTimerVersion(force: false);
        SyncProgressVersion(force: false);
        UpdateProgress();
        UpdateText(force: false);
    }

    private void EnsureSceneTimerRegistered()
    {
        if (_registeredSceneTimer)
            return;

        _registeredSceneTimer = true;

        LoadingSceneContext.RegisterLoadedScene(
            progressConfig,
            GetSceneShowSecondsForCurrentMode(),
            offlineRelocationMaxNpcsPerSlice,
            offlineRelocationMaxSliceMs);

        SyncTimerVersion(force: true);
        SyncProgressVersion(force: true);

        LogState("EnsureSceneTimerRegistered");
    }

    private float GetSceneShowSecondsForCurrentMode()
    {
        return LoadingSceneContext.Mode == LoadingSceneMode.SystemTravel
            ? systemTravelShowSeconds
            : gameStartShowSeconds;
    }

    private void SyncTimerVersion(bool force)
    {
        if (!force &&
            _observedTimerVersion == LoadingSceneContext.TimerVersion)
        {
            return;
        }

        _observedTimerVersion =
            LoadingSceneContext.TimerVersion;

        _lastTextIndex = -1;
        _lastLoggedProgressSecond = -1;

        ConfigureProgressImage();
        UpdateProgress();
        UpdateText(force: true);

        LogState("SyncTimerVersion");
    }

    private void SyncProgressVersion(bool force)
    {
        if (!force &&
            _observedProgressVersion == LoadingSceneContext.ProgressVersion)
        {
            return;
        }

        _observedProgressVersion =
            LoadingSceneContext.ProgressVersion;

        UpdateProgress();
        UpdateText(force: true);

        LogState("SyncProgressVersion");
    }

    public static IEnumerator WaitForCurrentSceneMinimumTime()
    {
        LoadingSceneController controller = Active;

        if (controller == null)
        {
            yield return LoadingSceneContext
                .WaitForCurrentSceneMinimumTime();

            yield break;
        }

        while (!controller.IsMinimumShowTimeComplete)
            yield return null;
    }

    private void ApplyRandomBackground()
    {
        if (backgroundImage == null)
            return;

        Sprite[] images = LoadingSceneContext.Mode ==
                          LoadingSceneMode.SystemTravel
            ? systemTravelImages
            : gameStartImages;

        Sprite sprite = GetRandomSprite(images);

        if (sprite == null)
            return;

        backgroundImage.sprite = sprite;
        backgroundImage.preserveAspect = false;
        backgroundImage.color = Color.white;
    }

    private void SelectActiveTexts()
    {
        _activeTexts = LoadingSceneContext.Mode ==
                       LoadingSceneMode.SystemTravel
            ? systemTravelTexts
            : gameStartTexts;
    }

    private void UpdateProgress()
    {
        if (progressSlider == null)
            return;

        float targetProgress =
            LoadingSceneContext.WorkCompleted
                ? 1f
                : Mathf.Clamp01(
                    LoadingSceneContext.WorkProgress01 *
                    LoadingSceneContext.MaxProgressBeforeWorkComplete);

        if (!LoadingSceneContext.WorkCompleted)
        {
            targetProgress =
                Mathf.Min(
                    targetProgress,
                    LoadingSceneContext.MaxProgressBeforeWorkComplete);
        }

        _displayedProgress =
            Mathf.MoveTowards(
                _displayedProgress,
                targetProgress,
                LoadingSceneContext.ProgressSmoothSpeed * Time.unscaledDeltaTime);

        if (LoadingSceneContext.WorkCompleted)
            _displayedProgress = 1f;

        progressSlider.SetValueWithoutNotify(
            _displayedProgress);

        int visibleSecond =
            Mathf.FloorToInt(
                LoadingSceneContext.ElapsedSinceRegistered);

        if (LoadingSceneContext.IsSceneRegistered &&
            visibleSecond != _lastLoggedProgressSecond)
        {
            _lastLoggedProgressSecond = visibleSecond;
            LogState("UpdateProgress.SecondChanged");
        }
    }

    private void UpdateText(bool force)
    {
        if (bottomText == null)
            return;

        string progressLabel =
            LoadingSceneContext.WorkProgressLabel;

        if (!string.IsNullOrWhiteSpace(progressLabel))
        {
            if (!force && bottomText.text == progressLabel)
                return;

            bottomText.text = progressLabel;
            LogState("UpdateText.ProgressLabel");
            return;
        }

        if (_activeTexts == null ||
            _activeTexts.Length == 0)
        {
            return;
        }

        float sceneShowSeconds =
            Mathf.Max(0f, LoadingSceneContext.SceneShowSeconds);

        int index = sceneShowSeconds <= 0f
            ? 0
            : Mathf.FloorToInt(
                LoadingSceneContext.ElapsedSinceRegistered /
                textChangeSeconds) % _activeTexts.Length;

        if (!force && index == _lastTextIndex)
            return;

        _lastTextIndex = index;
        bottomText.text = _activeTexts[index];

        LogState("UpdateText | Index=" + index);
    }

    private static Sprite GetRandomSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        return sprites[Random.Range(0, sprites.Length)];
    }

    private void ConfigureProgressImage()
    {
        _displayedProgress = 0f;

        if (progressSlider == null)
            return;

        progressSlider.wholeNumbers = false;
        progressSlider.minValue = 0f;
        progressSlider.maxValue = 1f;
        progressSlider.interactable = false;
        progressSlider.SetValueWithoutNotify(0f);
        progressSlider.normalizedValue = 0f;

        Canvas.ForceUpdateCanvases();
    }

    private void LogState(string phase)
    {
        if (!IsLoadingSceneDiagnosticsLogEnabled())
            return;

        Debug.Log(
            "[LOADING_DIAG][Controller] " +
            phase +
            " | Frame=" + Time.frameCount +
            " | Time=" + Time.unscaledTime.ToString("F3") +
            " | Mode=" + LoadingSceneContext.Mode +
            " | GameStartShowSeconds=" + gameStartShowSeconds.ToString("F2") +
            " | SystemTravelShowSeconds=" + systemTravelShowSeconds.ToString("F2") +
            " | ContextShowSeconds=" + LoadingSceneContext.SceneShowSeconds.ToString("F2") +
            " | Registered=" + LoadingSceneContext.IsSceneRegistered +
            " | Elapsed=" + LoadingSceneContext.ElapsedSinceRegistered.ToString("F3") +
            " | Complete=" + LoadingSceneContext.IsMinimumShowTimeComplete +
            " | TimerVersion=" + LoadingSceneContext.TimerVersion +
            " | ObservedVersion=" + _observedTimerVersion +
            " | ProgressVersion=" + LoadingSceneContext.ProgressVersion +
            " | ObservedProgressVersion=" + _observedProgressVersion +
            " | WorkProgress01=" + LoadingSceneContext.WorkProgress01.ToString("F3") +
            " | WorkCompleted=" + LoadingSceneContext.WorkCompleted +
            " | WorkLabel=" + LoadingSceneContext.WorkProgressLabel +
            " | DisplayedProgress=" + _displayedProgress.ToString("F3") +
            " | SliderValue=" + (progressSlider != null ? progressSlider.value.ToString("F3") : "null") +
            " | SliderNormalized=" + (progressSlider != null ? progressSlider.normalizedValue.ToString("F3") : "null") +
            " | ContextOfflineRelocationNpcPercent=" + LoadingSceneContext.OfflineRelocationNpcPercent.ToString("F1") +
            " | TextIndex=" + _lastTextIndex);
    }

    private static bool IsLoadingSceneDiagnosticsLogEnabled()
    {
        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.DebugLogConfig == null)
        {
            return false;
        }

        return Bootstrapper.Instance
            .DebugLogConfig
            .LoadingSceneDiagnosticsLogs;
    }
}