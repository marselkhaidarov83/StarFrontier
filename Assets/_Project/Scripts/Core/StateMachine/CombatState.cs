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
        Debug.Log("Entered CombatState");
        _sceneService.LoadCombat();
    }

    public void Exit()
    {
        Debug.Log("Exited CombatState");
    }
}