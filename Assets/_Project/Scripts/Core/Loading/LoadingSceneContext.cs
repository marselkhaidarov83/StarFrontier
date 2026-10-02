using System.Collections;
using UnityEngine;

public enum LoadingSceneMode
{
    GameStart = 0,
    SystemTravel = 1
}

public static class LoadingSceneContext
{
    private const float DefaultOfflineRelocationNpcPercent = 25f;
    private const int DefaultOfflineRelocationMaxNpcsPerSlice = 250;
    private const float DefaultOfflineRelocationMaxSliceMs = 8f;
    private const float RuntimeProfileBlend = 0.35f;

    private static LoadingProgressConfig _progressConfig;
    private static LoadingProgressRuntimeProfile _runtimeProgressProfile;

    private static float _offlineRelocationNpcPercent =
        DefaultOfflineRelocationNpcPercent;

    private static int _offlineRelocationMaxNpcsPerSlice =
        DefaultOfflineRelocationMaxNpcsPerSlice;

    private static float _offlineRelocationMaxSliceMs =
        DefaultOfflineRelocationMaxSliceMs;

    private static float _sceneStartedAt;
    private static float _sceneShowSeconds;
    private static int _timerVersion;
    private static bool _sceneRegistered;

    private static float _workProgress01;
    private static string _workProgressLabel = string.Empty;
    private static bool _workCompleted;
    private static int _progressVersion;

    private static float _saveLoadMs;
    private static float _sessionLoadMs;
    private static float _offlineRelocationMs;
    private static float _populationMs;
    private static float _warmupMs;

    public static LoadingSceneMode Mode { get; private set; } =
        LoadingSceneMode.GameStart;

    public static float SceneShowSeconds =>
        _sceneShowSeconds;

    public static int TimerVersion =>
        _timerVersion;

    public static int ProgressVersion =>
        _progressVersion;

    public static bool IsSceneRegistered =>
        _sceneRegistered;

    public static float WorkProgress01 =>
        _workProgress01;

    public static string WorkProgressLabel =>
        _workProgressLabel;

    public static bool WorkCompleted =>
        _workCompleted;

    public static float OfflineRelocationNpcPercent =>
        Mathf.Clamp(
            _offlineRelocationNpcPercent,
            0f,
            100f);

    public static int OfflineRelocationMaxNpcsPerSlice =>
        Mathf.Max(
            1,
            _offlineRelocationMaxNpcsPerSlice);

    public static float OfflineRelocationMaxSliceMs =>
        Mathf.Max(
            0.1f,
            _offlineRelocationMaxSliceMs);

    public static float ElapsedSinceRegistered =>
        _sceneRegistered
            ? Time.unscaledTime - _sceneStartedAt
            : 0f;

    public static bool IsMinimumShowTimeComplete =>
        _sceneShowSeconds <= 0f ||
        (_sceneRegistered &&
         ElapsedSinceRegistered >= _sceneShowSeconds);

    public static float StartProgress =>
        GetConfigProgressValue(
            _progressConfig?.StartProgress,
            0.05f);

    public static float CanContinueProgress =>
        GetConfigProgressValue(
            _progressConfig?.CanContinueProgress,
            0.10f);

    public static float SaveLoadedProgress =>
        GetRuntimeOrConfigProgressValue(
            _runtimeProgressProfile?.SaveLoadedProgress,
            _progressConfig?.SaveLoadedProgress,
            0.22f);

    public static float SessionLoadedProgress =>
        GetRuntimeOrConfigProgressValue(
            _runtimeProgressProfile?.SessionLoadedProgress,
            _progressConfig?.SessionLoadedProgress,
            0.25f);

    public static float OfflineRelocationFromProgress =>
        GetRuntimeOrConfigProgressValue(
            _runtimeProgressProfile?.OfflineRelocationFromProgress,
            _progressConfig?.OfflineRelocationFromProgress,
            0.25f);

    public static float OfflineRelocationToProgress =>
        GetRuntimeOrConfigProgressValue(
            _runtimeProgressProfile?.OfflineRelocationToProgress,
            _progressConfig?.OfflineRelocationToProgress,
            0.78f);

    public static float PopulationProgress =>
        GetRuntimeOrConfigProgressValue(
            _runtimeProgressProfile?.PopulationProgress,
            _progressConfig?.PopulationProgress,
            0.80f);

    public static float EnterSystemProgress =>
        GetRuntimeOrConfigProgressValue(
            _runtimeProgressProfile?.EnterSystemProgress,
            _progressConfig?.EnterSystemProgress,
            0.82f);

    public static float WarmupFromProgress =>
        GetRuntimeOrConfigProgressValue(
            _runtimeProgressProfile?.WarmupFromProgress,
            _progressConfig?.WarmupFromProgress,
            0.82f);

    public static float WarmupToProgress =>
        GetRuntimeOrConfigProgressValue(
            _runtimeProgressProfile?.WarmupToProgress,
            _progressConfig?.WarmupToProgress,
            0.98f);

    public static float BeforeLoadSystemProgress =>
        GetConfigProgressValue(
            _progressConfig?.BeforeLoadSystemProgress,
            0.99f);

    public static float MaxProgressBeforeWorkComplete =>
        GetConfigProgressValue(
            _progressConfig?.MaxProgressBeforeWorkComplete,
            0.98f);

    public static float ProgressSmoothSpeed =>
        Mathf.Max(
            0.01f,
            _progressConfig != null
                ? _progressConfig.ProgressSmoothSpeed
                : 1.5f);

    public static void SetGameStart()
    {
        Mode = LoadingSceneMode.GameStart;
        ResetProgress();
        Log("SetGameStart");
    }

    public static void SetSystemTravel()
    {
        Mode = LoadingSceneMode.SystemTravel;
        ResetProgress();
        Log("SetSystemTravel");
    }

    public static void SetSystemTravel(GameRuntimeState state)
    {
        Mode = LoadingSceneMode.SystemTravel;
        ResetProgress();

        ApplyRuntimeProgressProfile(
            state != null && state.Meta != null
                ? state.Meta.LoadingProgressProfile
                : null);

        Log("SetSystemTravel.StateProfile");
    }

    public static void RegisterLoadedScene(
        LoadingProgressConfig progressConfig,
        float fallbackSceneShowSeconds,
        int fallbackOfflineRelocationMaxNpcsPerSlice,
        float fallbackOfflineRelocationMaxSliceMs)
    {
        _progressConfig = progressConfig;

        _sceneStartedAt = Time.unscaledTime;

        _sceneShowSeconds =
            progressConfig != null
                ? Mathf.Max(
                    0f,
                    Mode == LoadingSceneMode.SystemTravel
                        ? progressConfig.SystemTravelShowSeconds
                        : progressConfig.GameStartShowSeconds)
                : Mathf.Max(0f, fallbackSceneShowSeconds);

        _offlineRelocationMaxNpcsPerSlice =
            progressConfig != null
                ? Mathf.Max(1, progressConfig.OfflineRelocationMaxNpcsPerSlice)
                : Mathf.Max(1, fallbackOfflineRelocationMaxNpcsPerSlice);

        _offlineRelocationMaxSliceMs =
            progressConfig != null
                ? Mathf.Max(0.1f, progressConfig.OfflineRelocationMaxSliceMs)
                : Mathf.Max(0.1f, fallbackOfflineRelocationMaxSliceMs);

        _offlineRelocationNpcPercent =
            progressConfig != null
                ? Mathf.Clamp(progressConfig.OfflineRelocationNpcPercent, 0f, 100f)
                : DefaultOfflineRelocationNpcPercent;

        _sceneRegistered = true;
        _timerVersion++;

        Log("RegisterLoadedScene");
    }

    public static void ApplyRuntimeProgressProfile(
        LoadingProgressRuntimeProfile profile)
    {
        if (profile == null || !profile.HasSamples)
        {
            _runtimeProgressProfile = null;
            Log("ApplyRuntimeProgressProfile.Empty");
            return;
        }

        _runtimeProgressProfile = profile;
        Log("ApplyRuntimeProgressProfile");
    }

    public static void RecordSaveLoadMs(double ms)
    {
        _saveLoadMs = Mathf.Max(0f, (float)ms);
    }

    public static void RecordSessionLoadMs(double ms)
    {
        _sessionLoadMs = Mathf.Max(0f, (float)ms);
    }

    public static void RecordOfflineRelocationMs(double ms)
    {
        _offlineRelocationMs = Mathf.Max(0f, (float)ms);
    }

    public static void RecordPopulationMs(double ms)
    {
        _populationMs = Mathf.Max(0f, (float)ms);
    }

    public static void RecordWarmupMs(double ms)
    {
        _warmupMs = Mathf.Max(0f, (float)ms);
    }

    public static void WriteRuntimeProgressProfileToSave(
        GameRuntimeState state)
    {
        if (state == null)
            return;

        state.Meta ??= new GameRuntimeMetaState();

        LoadingProgressRuntimeProfile previous =
            state.Meta.LoadingProgressProfile;

        LoadingProgressRuntimeProfile next =
            BuildRuntimeProgressProfile(previous);

        if (next == null)
            return;

        state.Meta.LoadingProgressProfile = next;
        _runtimeProgressProfile = next;

        Log("WriteRuntimeProgressProfileToSave");
    }

    public static void SetProgress(string label, float progress01)
    {
        float clampedProgress =
            Mathf.Clamp01(progress01);

        if (clampedProgress < _workProgress01)
            clampedProgress = _workProgress01;

        _workProgress01 = clampedProgress;
        _workProgressLabel = label ?? string.Empty;
        _workCompleted = _workProgress01 >= 0.999f;
        _progressVersion++;

        Log("SetProgress | Label=" + _workProgressLabel);
    }

    public static void MarkWorkComplete(string label)
    {
        _workProgress01 = 1f;
        _workProgressLabel = label ?? string.Empty;
        _workCompleted = true;
        _progressVersion++;

        Log("MarkWorkComplete | Label=" + _workProgressLabel);
    }

    public static IEnumerator WaitForCurrentSceneMinimumTime()
    {
        Log("WaitForCurrentSceneMinimumTime.Start");

        if (_sceneShowSeconds <= 0f)
            yield break;

        while (!IsMinimumShowTimeComplete)
            yield return null;

        Log("WaitForCurrentSceneMinimumTime.Complete");
    }

    private static LoadingProgressRuntimeProfile BuildRuntimeProgressProfile(
        LoadingProgressRuntimeProfile previous)
    {
        float saveLoadMs =
            BlendMs(previous?.SaveLoadMs ?? 0f, _saveLoadMs, previous);

        float sessionLoadMs =
            BlendMs(previous?.SessionLoadMs ?? 0f, _sessionLoadMs, previous);

        float offlineRelocationMs =
            BlendMs(previous?.OfflineRelocationMs ?? 0f, _offlineRelocationMs, previous);

        float populationMs =
            BlendMs(previous?.PopulationMs ?? 0f, _populationMs, previous);

        float warmupMs =
            BlendMs(previous?.WarmupMs ?? 0f, _warmupMs, previous);

        float totalMs =
            saveLoadMs +
            sessionLoadMs +
            offlineRelocationMs +
            populationMs +
            warmupMs;

        if (totalMs <= 0.001f)
            return previous;

        float start =
            Mathf.Clamp01(CanContinueProgress);

        float end =
            Mathf.Clamp01(WarmupToProgress);

        if (end <= start + 0.05f)
            end = Mathf.Clamp01(start + 0.05f);

        float range =
            end - start;

        float cursor = start;

        float saveLoadedProgress =
            AdvanceProgress(ref cursor, range, saveLoadMs, totalMs);

        float sessionLoadedProgress =
            AdvanceProgress(ref cursor, range, sessionLoadMs, totalMs);

        float offlineFromProgress =
            sessionLoadedProgress;

        float offlineToProgress =
            AdvanceProgress(ref cursor, range, offlineRelocationMs, totalMs);

        float populationProgress =
            AdvanceProgress(ref cursor, range, populationMs, totalMs);

        float enterSystemProgress =
            populationProgress;

        float warmupFromProgress =
            enterSystemProgress;

        float warmupToProgress =
            AdvanceProgress(ref cursor, range, warmupMs, totalMs);

        LoadingProgressRuntimeProfile next =
            new LoadingProgressRuntimeProfile
            {
                HasSamples = true,
                SampleCount = previous != null && previous.HasSamples
                    ? previous.SampleCount + 1
                    : 1,

                SaveLoadMs = saveLoadMs,
                SessionLoadMs = sessionLoadMs,
                OfflineRelocationMs = offlineRelocationMs,
                PopulationMs = populationMs,
                WarmupMs = warmupMs,

                SaveLoadedProgress = saveLoadedProgress,
                SessionLoadedProgress = sessionLoadedProgress,
                OfflineRelocationFromProgress = offlineFromProgress,
                OfflineRelocationToProgress = offlineToProgress,
                PopulationProgress = populationProgress,
                EnterSystemProgress = enterSystemProgress,
                WarmupFromProgress = warmupFromProgress,
                WarmupToProgress = warmupToProgress
            };

        NormalizeRuntimeProfile(next);

        return next;
    }

    private static float BlendMs(
        float previousMs,
        float currentMs,
        LoadingProgressRuntimeProfile previous)
    {
        if (previous == null || !previous.HasSamples)
            return currentMs;

        if (currentMs <= 0.001f)
            return previousMs;

        return Mathf.Lerp(
            previousMs,
            currentMs,
            RuntimeProfileBlend);
    }

    private static float AdvanceProgress(
        ref float cursor,
        float range,
        float partMs,
        float totalMs)
    {
        if (totalMs <= 0f)
            return cursor;

        cursor += range * Mathf.Max(0f, partMs) / totalMs;

        return Mathf.Clamp01(cursor);
    }

    private static void NormalizeRuntimeProfile(
        LoadingProgressRuntimeProfile profile)
    {
        if (profile == null)
            return;

        profile.SaveLoadedProgress =
            ClampProgressAfter(
                profile.SaveLoadedProgress,
                CanContinueProgress,
                0.01f);

        profile.SessionLoadedProgress =
            ClampProgressAfter(
                profile.SessionLoadedProgress,
                profile.SaveLoadedProgress,
                0.01f);

        profile.OfflineRelocationFromProgress =
            profile.SessionLoadedProgress;

        profile.OfflineRelocationToProgress =
            ClampProgressAfter(
                profile.OfflineRelocationToProgress,
                profile.OfflineRelocationFromProgress,
                0.03f);

        profile.PopulationProgress =
            ClampProgressAfter(
                profile.PopulationProgress,
                profile.OfflineRelocationToProgress,
                0.01f);

        profile.EnterSystemProgress =
            ClampProgressAfter(
                profile.EnterSystemProgress,
                profile.PopulationProgress,
                0f);

        profile.WarmupFromProgress =
            profile.EnterSystemProgress;

        profile.WarmupToProgress =
            ClampProgressAfter(
                profile.WarmupToProgress,
                profile.WarmupFromProgress,
                0.03f);

        profile.WarmupToProgress =
            Mathf.Min(
                profile.WarmupToProgress,
                MaxProgressBeforeWorkComplete);
    }

    private static float ClampProgressAfter(
        float value,
        float previous,
        float minStep)
    {
        return Mathf.Clamp01(
            Mathf.Max(
                value,
                previous + minStep));
    }

    private static float GetConfigProgressValue(
        float? value,
        float fallback)
    {
        return Mathf.Clamp01(
            value ?? fallback);
    }

    private static float GetRuntimeOrConfigProgressValue(
        float? runtimeValue,
        float? configValue,
        float fallback)
    {
        if (_runtimeProgressProfile != null &&
            _runtimeProgressProfile.HasSamples &&
            runtimeValue.HasValue)
        {
            return Mathf.Clamp01(runtimeValue.Value);
        }

        return GetConfigProgressValue(
            configValue,
            fallback);
    }

    private static void ResetProgress()
    {
        _runtimeProgressProfile = null;
        _sceneRegistered = false;
        _sceneStartedAt = 0f;
        _sceneShowSeconds = 0f;
        _workProgress01 = 0f;
        _workProgressLabel = string.Empty;
        _workCompleted = false;
        _saveLoadMs = 0f;
        _sessionLoadMs = 0f;
        _offlineRelocationMs = 0f;
        _populationMs = 0f;
        _warmupMs = 0f;
        _progressVersion++;
    }

    public static void Log(string phase)
    {
        if (!IsLoadingSceneDiagnosticsLogEnabled())
            return;

        Debug.Log(
            "[LOADING_DIAG][Context] " +
            phase +
            " | Frame=" + Time.frameCount +
            " | Time=" + Time.unscaledTime.ToString("F3") +
            " | Mode=" + Mode +
            " | Registered=" + _sceneRegistered +
            " | ShowSeconds=" + _sceneShowSeconds.ToString("F2") +
            " | Elapsed=" + ElapsedSinceRegistered.ToString("F3") +
            " | Complete=" + IsMinimumShowTimeComplete +
            " | TimerVersion=" + _timerVersion +
            " | ProgressVersion=" + _progressVersion +
            " | WorkProgress01=" + _workProgress01.ToString("F3") +
            " | WorkCompleted=" + _workCompleted +
            " | RuntimeProfile=" + (_runtimeProgressProfile != null && _runtimeProgressProfile.HasSamples) +
            " | OfflineRelocationNpcPercent=" + _offlineRelocationNpcPercent.ToString("F1") +
            " | SaveLoadMs=" + _saveLoadMs.ToString("F1") +
            " | SessionLoadMs=" + _sessionLoadMs.ToString("F1") +
            " | OfflineRelocationMs=" + _offlineRelocationMs.ToString("F1") +
            " | PopulationMs=" + _populationMs.ToString("F1") +
            " | WarmupMs=" + _warmupMs.ToString("F1") +
            " | WorkLabel=" + _workProgressLabel);
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