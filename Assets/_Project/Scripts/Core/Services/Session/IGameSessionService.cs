public interface IGameSessionService
{
    GameRuntimeState State { get; }
    bool HasActiveSession { get; }

    void StartNewSession(GameRuntimeState state);
    void LoadSession(GameRuntimeState state);
    void ClearSession();
}