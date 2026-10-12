using System.Collections;
using UnityEngine;

public class SystemState : IGameState
{
    private const string LOADING_SCENE = "LoadingScene";

    private readonly ISceneService _sceneService;
    private readonly IGalaxyNpcWarmupService _galaxyNpcWarmupService;
    private readonly IGalaxyNpcSimulationScheduleService _galaxyNpcSimulationScheduleService;
    private readonly IGameSessionService _gameSessionService;
    private readonly ITravelService _travelService;
    private readonly string _pendingTravelTargetSystemId;

    private Coroutine _warmupRoutine;
    private bool _waitingForLoading;
    private bool _debugEnabled;

    public SystemState()
        : this(null)
    {
    }

    public SystemState(string pendingTravelTargetSystemId)
    {
        IServiceRegistry registry =
            Bootstrapper.Instance.ServiceRegistry;

        _sceneService =
            registry.Get<ISceneService>();

        registry.TryGet(out _gameSessionService);
        registry.TryGet(out _galaxyNpcWarmupService);
        registry.TryGet(out _travelService);
        registry.TryGet(out _galaxyNpcSimulationScheduleService);

        _pendingTravelTargetSystemId =
            NormalizeSystemId(pendingTravelTargetSystemId);
    }

    public SystemState(
        ISceneService sceneService,
        IGalaxyNpcWarmupService galaxyNpcWarmupService)
        : this(sceneService, galaxyNpcWarmupService, null)
    {
    }

    public SystemState(
        ISceneService sceneService,
        IGalaxyNpcWarmupService galaxyNpcWarmupService,
        string pendingTravelTargetSystemId)
    {
        _sceneService = sceneService;
        _galaxyNpcWarmupService = galaxyNpcWarmupService;
        _pendingTravelTargetSystemId =
            NormalizeSystemId(pendingTravelTargetSystemId);

        if (Bootstrapper.Instance?.ServiceRegistry != null)
        {
            Bootstrapper.Instance.ServiceRegistry.TryGet(
                out _gameSessionService);

            Bootstrapper.Instance.ServiceRegistry.TryGet(
                out _travelService);
        }
    }

    public void Enter()
    {
        LogSystemOpenTrace(
            "Enter.Start" +
            " | PendingTravelTargetSystemId=" + FormatTraceValue(_pendingTravelTargetSystemId) +
            " | LoadingSceneRegistered=" + LoadingSceneContext.IsSceneRegistered +
            " | ActiveScene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

        if (_debugEnabled)
            Debug.Log("Entered SystemState");

        if (_sceneService == null)
        {
            AppLog.Error(
                "SystemState cannot start: SceneService is missing.");

            return;
        }

        if (IsLoadingSceneReadyForSystemWarmup())
        {
            LogSystemOpenTrace(
                "Enter.LoadingSceneAlreadyReady" +
                " | PendingTravelTargetSystemId=" + FormatTraceValue(_pendingTravelTargetSystemId));

            StartWarmupAfterLoadingSceneReady();
            return;
        }

        SubscribeToSceneEvents();

        _waitingForLoading = true;

        LogSystemOpenTrace(
            "Enter.LoadLoadingAsync.Before" +
            " | PendingTravelTargetSystemId=" + FormatTraceValue(_pendingTravelTargetSystemId));

        if (_sceneService.LoadLoadingAsync() == null &&
            _waitingForLoading)
        {
            HandleLoadingFailure(
                LOADING_SCENE,
                "Loading operation was not created.");
        }
    }

    private static bool IsLoadingSceneReadyForSystemWarmup()
    {
        if (LoadingSceneContext.IsSceneRegistered)
            return true;

        UnityEngine.SceneManagement.Scene activeScene =
            UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        return activeScene.IsValid() &&
               activeScene.name == LOADING_SCENE;
    }

    private void StartWarmupAfterLoadingSceneReady()
    {
        LogSystemOpenTrace(
            "StartWarmupAfterLoadingSceneReady.Start" +
            " | PendingTravelTargetSystemId=" + FormatTraceValue(_pendingTravelTargetSystemId) +
            " | HasWarmupRoutine=" + (_warmupRoutine != null));

        if (_warmupRoutine != null)
            return;

        if (Bootstrapper.Instance == null)
        {
            AppLog.Error(
                "SystemState cannot run warmup: Bootstrapper.Instance is null.");

            _sceneService.LoadSystem();
            return;
        }

        _warmupRoutine =
            Bootstrapper.Instance.StartCoroutine(
                RunWarmupThenLoadSystem());

        LogSystemOpenTrace(
            "StartWarmupAfterLoadingSceneReady.CoroutineStarted" +
            " | PendingTravelTargetSystemId=" + FormatTraceValue(_pendingTravelTargetSystemId));
    }

    public void Exit()
    {
        if (_waitingForLoading)
            _sceneService?.CancelActiveLoad();

        _waitingForLoading = false;

        if (_warmupRoutine != null &&
            Bootstrapper.Instance != null)
        {
            Bootstrapper.Instance.StopCoroutine(_warmupRoutine);
            _warmupRoutine = null;
        }

        UnsubscribeFromSceneEvents();

        if (_debugEnabled)
            Debug.Log("Exited SystemState");
    }

    private void SubscribeToSceneEvents()
    {
        _sceneService.SceneLoadCompleted += HandleSceneLoaded;
        _sceneService.SceneLoadFailed += HandleLoadingFailure;
        _sceneService.SceneLoadCancelled += HandleLoadingCancelled;
    }

    private void UnsubscribeFromSceneEvents()
    {
        if (_sceneService == null)
            return;

        _sceneService.SceneLoadCompleted -= HandleSceneLoaded;
        _sceneService.SceneLoadFailed -= HandleLoadingFailure;
        _sceneService.SceneLoadCancelled -= HandleLoadingCancelled;
    }

    private void HandleSceneLoaded(string sceneName)
    {
        LogSystemOpenTrace(
            "HandleSceneLoaded" +
            " | SceneName=" + FormatTraceValue(sceneName) +
            " | WaitingForLoading=" + _waitingForLoading +
            " | PendingTravelTargetSystemId=" + FormatTraceValue(_pendingTravelTargetSystemId));

        if (!_waitingForLoading ||
            sceneName != LOADING_SCENE)
        {
            return;
        }

        _waitingForLoading = false;
        UnsubscribeFromSceneEvents();

        StartWarmupAfterLoadingSceneReady();
    }

    private IEnumerator RunWarmupThenLoadSystem()
    {
        while (!LoadingSceneContext.IsSceneRegistered)
            yield return null;

        LoadingSceneContext.Log(
            "SystemState.RunWarmupThenLoadSystem.Start");

        bool hasPendingTravel =
            HasPendingSystemTravel();

        bool initialWarmupComplete =
            _galaxyNpcSimulationScheduleService != null &&
            _galaxyNpcSimulationScheduleService.IsInitialWarmupComplete;

        LogSystemOpenTrace(
            "RunWarmupThenLoadSystem.Start" +
            " | PendingTravelTargetSystemId=" + FormatTraceValue(_pendingTravelTargetSystemId) +
            " | HasPendingTravel=" + hasPendingTravel +
            " | InitialWarmupComplete=" + initialWarmupComplete);

        if (!hasPendingTravel &&
            initialWarmupComplete)
        {
            _warmupRoutine = null;

            LoadingSceneContext.MarkWorkComplete(
                string.Empty);

            LogSystemOpenTrace(
                "RunWarmupThenLoadSystem.LoadSystemWithoutPendingTravel.Before" +
                " | Reason=InitialWarmupAlreadyComplete" +
                " | PendingTravelTargetSystemId=" + FormatTraceValue(_pendingTravelTargetSystemId));

            _sceneService.LoadSystem();

            LogSystemOpenTrace(
                "RunWarmupThenLoadSystem.LoadSystemWithoutPendingTravel.After" +
                " | Reason=InitialWarmupAlreadyComplete" +
                " | PendingTravelTargetSystemId=" + FormatTraceValue(_pendingTravelTargetSystemId));

            yield break;
        }

        LoadingSceneContext.SetProgress(
            string.Empty,
            LoadingSceneContext.WarmupFromProgress);

        yield return null;

        if (hasPendingTravel &&
            !RunPendingSystemTravel())
        {
            _warmupRoutine = null;

            LoadingSceneContext.MarkWorkComplete(
                string.Empty);

            LogSystemOpenTrace(
                "RunWarmupThenLoadSystem.LoadSystemAfterFailedPendingTravel.Before" +
                " | PendingTravelTargetSystemId=" + FormatTraceValue(_pendingTravelTargetSystemId));

            _sceneService.LoadSystem();

            LogSystemOpenTrace(
                "RunWarmupThenLoadSystem.LoadSystemAfterFailedPendingTravel.After" +
                " | PendingTravelTargetSystemId=" + FormatTraceValue(_pendingTravelTargetSystemId));

            yield break;
        }

        LoadingSceneContext.Log(
            "SystemState.BeforeWarmup");

        LogSystemOpenTrace(
            "RunWarmupThenLoadSystem.BeforeWarmup" +
            " | PendingTravelTargetSystemId=" + FormatTraceValue(_pendingTravelTargetSystemId) +
            " | HasPendingTravel=" + hasPendingTravel +
            " | InitialWarmupComplete=" + initialWarmupComplete);

        long warmupStartedAt =
            System.Diagnostics.Stopwatch.GetTimestamp();

        if (_galaxyNpcWarmupService != null)
        {
            yield return _galaxyNpcWarmupService.RunInitialWarmupRoutine(
                hasPendingTravel
                    ? "SystemState.PendingTravel"
                    : "SystemState.InitialSystemOpen",
                LoadingSceneContext.WarmupFromProgress,
                LoadingSceneContext.WarmupToProgress);
        }
        else
        {
            AppLog.Warning(
                "SystemState warmup skipped: " +
                "IGalaxyNpcWarmupService is missing.");
        }

        double warmupMs =
            (System.Diagnostics.Stopwatch.GetTimestamp() - warmupStartedAt) *
            1000.0 /
            System.Diagnostics.Stopwatch.Frequency;

        LogSystemOpenTrace(
            "RunWarmupThenLoadSystem.AfterWarmup" +
            " | PendingTravelTargetSystemId=" + FormatTraceValue(_pendingTravelTargetSystemId) +
            " | HasPendingTravel=" + hasPendingTravel +
            " | WarmupMs=" + warmupMs.ToString("0.00"));

        LoadingSceneContext.RecordWarmupMs(warmupMs);

        if (_gameSessionService != null)
        {
            LoadingSceneContext.WriteRuntimeProgressProfileToSave(
                _gameSessionService.State);
        }

        LoadingSceneContext.SetProgress(
            string.Empty,
            LoadingSceneContext.BeforeLoadSystemProgress);

        LoadingSceneContext.Log(
            "SystemState.LoadSystem.Before");

        _warmupRoutine = null;

        if (_sceneService == null)
        {
            AppLog.Error(
                "SystemState cannot load SystemScene: SceneService is missing.");

            yield break;
        }

        LogSystemOpenTrace(
            "RunWarmupThenLoadSystem.LoadSystemAfterWarmup.Before" +
            " | PendingTravelTargetSystemId=" + FormatTraceValue(_pendingTravelTargetSystemId));

        _sceneService.LoadSystem();

        LogSystemOpenTrace(
            "RunWarmupThenLoadSystem.LoadSystemAfterWarmup.After" +
            " | PendingTravelTargetSystemId=" + FormatTraceValue(_pendingTravelTargetSystemId));

        LoadingSceneContext.MarkWorkComplete(
            string.Empty);

        LoadingSceneContext.Log(
            "SystemState.LoadSystem.After");
    }

    private bool RunPendingSystemTravel()
    {
        LogSystemOpenTrace(
            "RunPendingSystemTravel.Start" +
            " | PendingTravelTargetSystemId=" + FormatTraceValue(_pendingTravelTargetSystemId));

        if (!HasPendingSystemTravel())
        {
            LogSystemOpenTrace(
                "RunPendingSystemTravel.NoPendingTravel.ReturnFalse");

            return false;
        }

        if (_travelService == null)
        {
            AppLog.Error(
                "SystemState cannot perform pending system travel: " +
                "ITravelService is missing.");

            return false;
        }

        LoadingSceneContext.SetProgress(
            string.Empty,
            LoadingSceneContext.EnterSystemProgress);

        LogSystemOpenTrace(
            "RunPendingSystemTravel.TryTravel.Before" +
            " | PendingTravelTargetSystemId=" + FormatTraceValue(_pendingTravelTargetSystemId));

        TravelResult result =
            _travelService.TryTravel(
                _pendingTravelTargetSystemId);

        LogSystemOpenTrace(
            "RunPendingSystemTravel.TryTravel.After" +
            " | HasResult=" + (result != null) +
            " | Success=" + (result != null && result.Success) +
            " | FailReason=" + (result != null ? result.FailReason.ToString() : "None"));

        if (result == null)
        {
            AppLog.Error(
                "SystemState pending system travel failed: result is null.");

            return false;
        }

        if (!result.Success)
        {
            AppLog.Warning(
                "SystemState pending system travel rejected. Reason=" +
                result.FailReason);

            return false;
        }

        LoadingSceneContext.Log(
            "SystemState.PendingTravel.Completed");

        return true;
    }

    private bool HasPendingSystemTravel()
    {
        return !string.IsNullOrWhiteSpace(
            _pendingTravelTargetSystemId);
    }

    private static void LogSystemOpenTrace(string message)
    {
        Debug.Log(
            "[SYSTEM_OPEN_TRACE] " +
            message +
            " | Realtime=" + Time.realtimeSinceStartup.ToString("0.000"));
    }

    private static string FormatTraceValue(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "None"
            : value;
    }

    private void HandleLoadingFailure(
        string sceneName,
        string error)
    {
        if (!_waitingForLoading ||
            sceneName != LOADING_SCENE)
        {
            return;
        }

        _waitingForLoading = false;
        UnsubscribeFromSceneEvents();

        AppLog.Error(
            "SystemState Loading failed. " + error);

        _sceneService.LoadSystem();
    }

    private void HandleLoadingCancelled(string sceneName)
    {
        if (sceneName != LOADING_SCENE)
            return;

        _waitingForLoading = false;
        UnsubscribeFromSceneEvents();

        AppLog.Warning(
            "SystemState Loading was cancelled.");
    }

    private static string NormalizeSystemId(string systemId)
    {
        return string.IsNullOrWhiteSpace(systemId)
            ? string.Empty
            : systemId.Trim();
    }
}