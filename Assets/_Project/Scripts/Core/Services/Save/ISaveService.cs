public interface ISaveService
{
    bool HasSave();

    void Save();

    void Save(string reason);

    void Save(GameRuntimeState state);

    void Save(GameRuntimeState state, string reason);

    GameRuntimeState Load();

    void DeleteSave();

    void Tick(float deltaTime);

    void EnableSave(bool enable);
}