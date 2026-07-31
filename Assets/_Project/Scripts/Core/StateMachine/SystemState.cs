using UnityEngine;

public class SystemState : IGameState
{
    private readonly ISceneService _sceneService;
    private bool _debugEnabled;

    public SystemState()
    {
        _sceneService = Bootstrapper.Instance.ServiceRegistry.Get<ISceneService>();
    }

    public SystemState(ISceneService sceneService)
    {
        _sceneService = Bootstrapper.Instance.ServiceRegistry.Get<ISceneService>();
        _sceneService = sceneService;
    }

    public void Enter()
    {
        if (_debugEnabled)
            Debug.Log("Entered MetaState");
        _sceneService.LoadSystem();
    }

    public void Exit()
    {
        if (_debugEnabled)
            Debug.Log("Exited MetaState");
    }
}