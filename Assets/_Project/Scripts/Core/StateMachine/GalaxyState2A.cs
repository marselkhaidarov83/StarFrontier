using UnityEngine;

public class GalaxyState2A : IGameState
{
    private readonly ISceneService _sceneService;
    private bool _debugEnabled;

    public GalaxyState2A()
    {
        _sceneService = Bootstrapper2A.Instance.ServiceRegistry.Get<ISceneService>();
    }

    public void Enter()
    {
        if (_debugEnabled)
            Debug.Log("Entered GalaxyState");
        _sceneService.LoadGalaxy2A();
    }

    public void Exit()
    {
        if (_debugEnabled)
            Debug.Log("Exited GalaxyState");
    }
}