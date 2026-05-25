public interface ISaveService
{
    bool HasSave();
    void Save();
    void Save(SaveData saveRoot);
    SaveData Load();
    void DeleteSave();
    void Tick(float deltaTime);
    void EnableSave(bool enable);
}