public sealed class GameStateService
{
    public GameState State { get; private set; }

    public void SetState(GameState state)
    {
        State = state;
    }
}