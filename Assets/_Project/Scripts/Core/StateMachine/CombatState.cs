using UnityEngine;

public class CombatState : IGameState
{
    private readonly ISceneService _sceneService;

    public CombatState(ISceneService sceneService)
    {
        _sceneService = sceneService;
    }

    public void Enter()
    {
        AppLog.Info(DebugLogChannel.CombatPresentation, "Entered CombatState");
        _sceneService.LoadCombat();
    }

    public void Exit()
    {
        AppLog.Info(DebugLogChannel.CombatPresentation, "Exited CombatState");
    }
}