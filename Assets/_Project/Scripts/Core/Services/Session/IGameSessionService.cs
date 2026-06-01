public interface IGameSessionService
{
    GameState State { get; }
    bool HasActiveSession { get; }

    void StartNewSession(GameState state);
    void LoadSession(GameState state);
    void ClearSession();
}