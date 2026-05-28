using UnityEngine;

public class BootstrapState2A : CustomService, IGameState
{
    private readonly INewGameService _newGameService;

    public BootstrapState2A()
    {
        _newGameService = Bootstrapper2A.Instance.ServiceRegistry.Get<INewGameService>();
        LogCustom("NewGameService = " + _newGameService);
    }

    public void Enter()
    {
        LogCustom("Entered BootstrapState");
        
        _newGameService.StartNewGame();
    }

    public void Exit()
    {
        LogCustom("Exited BootstrapState");
    }
}