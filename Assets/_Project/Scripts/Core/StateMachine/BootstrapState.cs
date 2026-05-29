using UnityEngine;

public class BootstrapState : IGameState
{
    private readonly IGameStateMachine _stateMachine;
    private readonly ISceneService _sceneService;
    private readonly INewGameService _newGameService;
    private bool _debugEnabled;

    public BootstrapState(IGameStateMachine stateMachine, ISceneService sceneService)
    {
        _stateMachine = stateMachine;
        _sceneService = sceneService;
    }

    public BootstrapState()
    {
        _stateMachine = Bootstrapper.Instance.ServiceRegistry.Get<IGameStateMachine>();
        _sceneService = Bootstrapper.Instance.ServiceRegistry.Get<ISceneService>();

        _newGameService = Bootstrapper.Instance.ServiceRegistry.Get<INewGameService>();
        if (_debugEnabled)
            Debug.Log("BootstrapState | NewGameService = " + _newGameService);
    }

    public void Enter()
    {
        if (_debugEnabled)
            Debug.Log("BootstrapState | Entered BootstrapState");
        
        _newGameService.StartNewGame();
        // _stateMachine.Enter(new MenuState(_sceneService));
    }

    public void Exit()
    {
        if (_debugEnabled)
            Debug.Log("BootstrapState | Exited BootstrapState");
    }
}