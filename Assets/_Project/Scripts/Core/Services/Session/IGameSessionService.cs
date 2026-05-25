public interface IGameSessionService
{
    SaveData CurrentSave { get; }
    bool HasActiveSession { get; }

    void StartNewSession(SaveData saveRoot);
    void LoadSession(SaveData saveRoot);
    void ClearSession();
}