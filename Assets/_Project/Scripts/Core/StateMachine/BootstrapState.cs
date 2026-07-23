public class BootstrapState : IGameState
{
    private const string LOADING_SCENE = "LoadingScene";
    private const string MAIN_MENU_SCENE = "MainMenuScene";

    private readonly IGameStateMachine _stateMachine;
    private readonly ISceneService _sceneService;
    private readonly INewGameService _newGameService;
    private bool _waitingForLoading;
    private bool _debugEnabled;

    public BootstrapState(
        IGameStateMachine stateMachine,
        ISceneService sceneService)
    {
        _stateMachine = stateMachine;
        _sceneService = sceneService;

        if (Bootstrapper.Instance?.ServiceRegistry != null)
        {
            Bootstrapper.Instance.ServiceRegistry.TryGet(
                out _newGameService);
        }
    }

    public BootstrapState()
    {
        _stateMachine =
            Bootstrapper.Instance.ServiceRegistry.Get<IGameStateMachine>();
        _sceneService =
            Bootstrapper.Instance.ServiceRegistry.Get<ISceneService>();
        _newGameService =
            Bootstrapper.Instance.ServiceRegistry.Get<INewGameService>();

        if (_debugEnabled)
        {
            AppLog.Info(
                "BootstrapState | NewGameService = " +
                _newGameService);
        }
    }

    public void Enter()
    {
        if (_debugEnabled)
            AppLog.Info("BootstrapState | Entered BootstrapState");

        if (_sceneService == null || _newGameService == null)
        {
            AppLog.Error(
                "BootstrapState cannot start: " +
                "SceneService or NewGameService is missing.");
            return;
        }

        SubscribeToSceneEvents();
        _waitingForLoading = true;

        if (_sceneService.LoadLoadingAsync() == null &&
            _waitingForLoading)
        {
            HandleLoadingFailure(
                LOADING_SCENE,
                "Loading operation was not created.");
        }
    }

    public void Exit()
    {
        if (_waitingForLoading)
            _sceneService?.CancelActiveLoad();

        _waitingForLoading = false;
        UnsubscribeFromSceneEvents();

        if (_debugEnabled)
            AppLog.Info("BootstrapState | Exited BootstrapState");
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
        if (!_waitingForLoading ||
            sceneName != LOADING_SCENE)
        {
            return;
        }

        _waitingForLoading = false;
        UnsubscribeFromSceneEvents();
        _newGameService.StartNewGame();
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

        AppLog.Error($"Bootstrap Loading failed. {error}");

        if (!_sceneService.TryLoadFallback(MAIN_MENU_SCENE))
        {
            AppLog.Error(
                "Bootstrap fallback to MainMenuScene " +
                "could not be started.");
        }
    }

    private void HandleLoadingCancelled(string sceneName)
    {
        if (sceneName != LOADING_SCENE)
            return;

        _waitingForLoading = false;
        UnsubscribeFromSceneEvents();
        AppLog.Warning("Bootstrap Loading was cancelled.");
    }
}
