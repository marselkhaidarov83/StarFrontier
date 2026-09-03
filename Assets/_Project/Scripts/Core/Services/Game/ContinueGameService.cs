using UnityEngine;

public class ContinueGameService : CustomService, IContinueGameService
{
    private readonly ISaveService _saveService;
    private readonly IGameSessionService _gameSessionService;
    public readonly IGameStateMachine _gameStateMachine;
    private readonly IConfigService _configService;
    private readonly ISystemNpcOfflineRelocationService _npcOfflineRelocationService;
    private readonly ISystemNpcPopulationService _npcPopulationService;

    public ContinueGameService()
    {
        _gameStateMachine = Bootstrapper.Instance.ServiceRegistry.Get<IGameStateMachine>();
        _saveService = Bootstrapper.Instance.ServiceRegistry.Get<ISaveService>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _npcOfflineRelocationService);

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _npcPopulationService);
    }

    public bool CanContinue()
    {
        return _saveService.HasSave();
    }

    public bool ContinueGame()
    {
        if (!_saveService.HasSave())
        {
            if (_debugEnabled)
                Debug.LogWarning("ContinueGame failed: save file does not exist.");
            return false;
        }

        var save = _saveService.Load();

        if (save == null)
        {
            if (_debugEnabled)
                Debug.LogError("ContinueGame failed: save could not be loaded.");
            return false;
        }

        _gameSessionService.LoadSession(save);

        if (_npcOfflineRelocationService != null)
            _npcOfflineRelocationService.TryProcessOffline(save);

        EnsureMinimumPopulationForLoadedCurrentSystem(save);

        if (_gameStateMachine == null)
        {
            if (_debugEnabled)
                Debug.Log("GameStateMachine is null");
        }
        else
        {
            _gameStateMachine.Enter(new SystemState());
        }

        return true;
    }

    private void EnsureMinimumPopulationForLoadedCurrentSystem(
    GameRuntimeState save)
    {
        if (_npcPopulationService == null)
            return;

        if (save == null ||
            save.Galaxy == null ||
            string.IsNullOrWhiteSpace(save.Galaxy.CurrentSystemId))
            return;

        StarSystemConfig currentSystem =
            _configService.GetStarSystemConfigById(
                save.Galaxy.CurrentSystemId);

        if (currentSystem == null)
            return;

        _npcPopulationService.EnsureMinimumAlliesInSystem(
            currentSystem);
    }
}