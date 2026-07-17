public sealed class GameTimePauseScopeService : CustomService, IGameTimePauseScopeService
{
    private enum PauseScope
    {
        None,
        Galaxy,
        Planet
    }

    private readonly IGameTimeService _gameTimeService;
    private readonly SimpleEventBus _eventBus;

    private PauseScope _activeScope = PauseScope.None;

    private bool _savedPauseState;
    private bool _hasSavedPauseState;

    private bool _isInternalPauseChange;
    private bool _pauseWasManuallyChangedInsidePlanetScope;

    public GameTimePauseScopeService()
    {
        _gameTimeService = Bootstrapper.Instance.ServiceRegistry.Get<IGameTimeService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        if (_eventBus == null)
            return;

        _eventBus.Subscribe<GalaxyEnteredEvent>(OnGalaxyEntered);
        _eventBus.Subscribe<PlanetEnteredEvent>(OnPlanetEntered);
        _eventBus.Subscribe<StarSystemEnteredEvent>(OnSystemEntered);
        _eventBus.Subscribe<GameTimePauseChangedEvent>(OnGameTimePauseChanged);
    }

    private void OnGalaxyEntered(GalaxyEnteredEvent evt)
    {
        BeginPauseScope(PauseScope.Galaxy);
    }

    private void OnPlanetEntered(PlanetEnteredEvent evt)
    {
        BeginPauseScope(PauseScope.Planet);
    }

    private void OnSystemEntered(StarSystemEnteredEvent evt)
    {
        RestorePauseScopeIfNeeded();
    }

    private void OnGameTimePauseChanged(GameTimePauseChangedEvent evt)
    {
        if (_activeScope != PauseScope.Planet)
            return;

        if (_isInternalPauseChange)
            return;

        _pauseWasManuallyChangedInsidePlanetScope = true;

        LogCustom(
            "[GameTimePauseScopeService] Player changed Play/Pause inside planet scope. " +
            "CurrentPauseState = " + _gameTimeService.IsPaused
        );
    }

    private void BeginPauseScope(PauseScope scope)
    {
        if (_gameTimeService == null)
            return;

        if (!_hasSavedPauseState)
        {
            _savedPauseState = _gameTimeService.IsPaused;
            _hasSavedPauseState = true;
        }

        _activeScope = scope;

        if (scope == PauseScope.Planet)
            _pauseWasManuallyChangedInsidePlanetScope = false;

        SetPausedInternally(true);

        LogCustom(
            "[GameTimePauseScopeService] Pause scope started. " +
            "Scope = " + scope +
            " | SavedPauseState = " + _savedPauseState
        );
    }

    private void RestorePauseScopeIfNeeded()
    {
        if (_gameTimeService == null)
            return;

        if (_activeScope == PauseScope.None)
            return;

        if (!_hasSavedPauseState)
            return;

        PauseScope restoredScope = _activeScope;

        bool pauseStateToRestore = GetPauseStateToRestore();

        SetPausedInternally(pauseStateToRestore);

        ResetScopeState();

        LogCustom(
            "[GameTimePauseScopeService] Pause scope restored. " +
            "Scope = " + restoredScope +
            " | SavedPauseState = " + _savedPauseState +
            " | RestoredPauseState = " + pauseStateToRestore
        );
    }

    private bool GetPauseStateToRestore()
    {
        if (_activeScope == PauseScope.Planet)
            return GetPlanetPauseStateToRestore();

        return _savedPauseState;
    }

    private bool GetPlanetPauseStateToRestore()
    {
        if (_pauseWasManuallyChangedInsidePlanetScope)
            return _gameTimeService.IsPaused;

        return _savedPauseState;
    }

    private void SetPausedInternally(bool paused)
    {
        _isInternalPauseChange = true;

        _gameTimeService.SetPaused(paused);

        _isInternalPauseChange = false;
    }

    private void ResetScopeState()
    {
        _activeScope = PauseScope.None;
        _hasSavedPauseState = false;
        _pauseWasManuallyChangedInsidePlanetScope = false;
        _isInternalPauseChange = false;
    }
}