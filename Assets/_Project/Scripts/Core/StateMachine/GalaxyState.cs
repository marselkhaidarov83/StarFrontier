using UnityEngine;

public class GalaxyState : IGameState
{
    private readonly ISceneService _sceneService;
    private readonly SimpleEventBus _simpleEventBus;
    private bool _debugEnabled;

    public GalaxyState()
    {
        _simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
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
        _simpleEventBus.Publish(new GalaxyEnteredEvent());
    }

    public void Exit()
    {
        if (_debugEnabled)
            Debug.Log("Exited GalaxyState");
    }
}