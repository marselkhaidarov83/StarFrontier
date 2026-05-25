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
        Debug.Log("Entered MenuState");
        _sceneService.LoadMainMenu();
    }

    public void Exit()
    {
        Debug.Log("Exited MenuState");
    }
}