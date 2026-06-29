/// <summary>
/// Синхронизирует runtime-движение корабля
/// с PlayerState перед сохранением и после загрузки.
/// </summary>
public interface IPlayerShipSaveSyncService
{
    void InitializeMovementFromSave(
        GameRuntimeState state);

    void WriteMovementToSave(
        GameRuntimeState state);
}