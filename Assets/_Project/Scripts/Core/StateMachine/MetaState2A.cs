using UnityEngine;

public class MetaState2A : IGameState
{
    private readonly ISceneService _sceneService;
    private bool _debugEnabled;

    public MetaState2A()
    {
        _sceneService = Bootstrapper2A.Instance.ServiceRegistry.Get<ISceneService>();
    }

    public void Enter()
    {
        if (_debugEnabled)
            Debug.Log("Entered MetaState");
        _sceneService.LoadMeta2A();
    }

    public void Exit()
    {
        if (_debugEnabled)
            Debug.Log("Exited MetaState");
    }
}