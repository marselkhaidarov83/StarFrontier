public class GameStateMachine : IGameStateMachine
{
    private IGameState _currentState;

    public void Enter(IGameState newState)
    {
        _currentState?.Exit();
        _currentState = newState;
        _currentState.Enter();
    }
}