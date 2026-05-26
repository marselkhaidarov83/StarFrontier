using UnityEngine;

public sealed class GameStateService
{
    public GameState State { get; private set; }

    public bool HasState => State != null;

    public void SetState(GameState state)
    {
        if (state == null)
        {
            Debug.LogError("GameStateService: cannot set null GameState.");
            return;
        }

        State = state;
    }

    public void Clear()
    {
        State = null;
    }
}