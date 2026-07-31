public interface ISaveService
{
    bool HasSave();
    void Save();
    void Save(GameRuntimeState state);
    GameRuntimeState Load();
    void DeleteSave();
    void Tick(float deltaTime);
    void EnableSave(bool enable);
}