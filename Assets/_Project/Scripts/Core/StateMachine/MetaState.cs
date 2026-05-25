using UnityEngine;

public class MetaState : IGameState
{
    private readonly ISceneService _sceneService;
    private bool _debugEnabled;

    public MetaState()
    {
        _sceneService = Bootstrapper.Instance.ServiceRegistry.Get<ISceneService>();
    }

    public MetaState(ISceneService sceneService)
    {
        _sceneService = Bootstrapper.Instance.ServiceRegistry.Get<ISceneService>();
        _sceneService = sceneService;
    }

    public void Enter()
    {
        if (_debugEnabled)
            Debug.Log("Entered MetaState");
        _sceneService.LoadMeta();
    }

    public void Exit()
    {
        if (_debugEnabled)
            Debug.Log("Exited MetaState");
    }
}