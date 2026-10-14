using UnityEngine;

public class MenuState : IGameState
{
    private readonly ISceneService _sceneService;

    public MenuState()
    {
        _sceneService = Bootstrapper.Instance.ServiceRegistry.Get<ISceneService>();
    }

    public void Enter()
    {
        AppLog.Info(DebugLogChannel.MainMenu, "Entered MenuState");
        _sceneService.LoadMainMenu();
    }

    public void Exit()
    {
        AppLog.Info(DebugLogChannel.MainMenu, "Exited MenuState");
    }
}