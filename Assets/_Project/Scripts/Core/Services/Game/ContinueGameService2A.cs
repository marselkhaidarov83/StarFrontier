using UnityEngine;

public sealed class ContinueGameService2A : IContinueGameService
{
    private readonly ISaveService _saveService;
    private readonly IGameSessionService _gameSessionService;
    private readonly IGameStateMachine _gameStateMachine;

    public ContinueGameService2A()
    {
        _saveService = Bootstrapper2A.Instance.ServiceRegistry.Get<ISaveService>();
        _gameSessionService = Bootstrapper2A.Instance.ServiceRegistry.Get<IGameSessionService>();
        _gameStateMachine = Bootstrapper2A.Instance.ServiceRegistry.Get<IGameStateMachine>();
    }

    public bool CanContinue()
    {
        return _saveService != null && _saveService.HasSave();
    }

    public bool ContinueGame()
    {
        if (!CanContinue())
        {
            Debug.LogWarning("ContinueGameService2A: no save found.");
            return false;
        }

        var save = _saveService.Load();

        if (save == null)
        {
            Debug.LogError("ContinueGameService2A: save load failed.");
            return false;
        }

        _gameSessionService.LoadSession(save);

        if (_gameStateMachine != null)
        {
            _gameStateMachine.Enter(new MetaState2A());
        }

        Debug.Log("ContinueGameService2A: game continued from save.");
        return true;
    }
}