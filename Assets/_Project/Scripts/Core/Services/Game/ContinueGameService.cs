using UnityEngine;

public class ContinueGameService : IContinueGameService
{
    private readonly ISaveService _saveService;
    private readonly IGameSessionService _gameSessionService;
    public readonly IGameStateMachine _gameStateMachine;
    private bool _debugEnabled = true;

    public ContinueGameService()
    {
        _gameStateMachine = Bootstrapper.Instance.ServiceRegistry.Get<IGameStateMachine>();
        _saveService = Bootstrapper.Instance.ServiceRegistry.Get<ISaveService>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
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
        
        if (_gameStateMachine == null)
        {
            if (_debugEnabled)
                Debug.Log("GameStateMachine is null");
        }
        else 
            _gameStateMachine.Enter(new MetaState());

        return true;
    }
}