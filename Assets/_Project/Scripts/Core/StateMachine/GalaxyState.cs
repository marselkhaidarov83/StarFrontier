using UnityEngine;

public class GalaxyState : IGameState
{
    private readonly ISceneService _sceneService;
    private bool _debugEnabled;

    public GalaxyState()
    {
        _sceneService = Bootstrapper.Instance.ServiceRegistry.Get<ISceneService>();
    }

    public GalaxyState(ISceneService sceneService)
    {
        _sceneService = Bootstrapper.Instance.ServiceRegistry.Get<ISceneService>();
        _sceneService = sceneService;
    }

    public void Enter()
    {
        if (_debugEnabled)
            Debug.Log("Entered GalaxyState");
        _sceneService.LoadGalaxy();
    }

    public void Exit()
    {
        if (_debugEnabled)
            Debug.Log("Exited GalaxyState");
    }
}