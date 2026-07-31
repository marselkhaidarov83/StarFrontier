using System;

/// <summary>
/// Записывает позицию и направление корабля
/// из ShipMovementRuntimeState в PlayerState
/// и восстанавливает движение из PlayerState после загрузки.
/// </summary>
public sealed class PlayerShipSaveSyncService2A :
    IPlayerShipSaveSyncService
{
    private readonly IShipMovementService _shipMovementService;

    public PlayerShipSaveSyncService2A()
    {
        _shipMovementService = Bootstrapper.Instance.ServiceRegistry.Get<IShipMovementService>();
    }

    public PlayerShipSaveSyncService2A(
        IShipMovementService shipMovementService)
    {
        _shipMovementService =
            shipMovementService
            ?? throw new ArgumentNullException(
                nameof(shipMovementService));
    }

    public void InitializeMovementFromSave(
        GameRuntimeState state)
    {
        if (state == null || state.Player == null)
            return;

        _shipMovementService.InitializeFromPlayerState(
            state.Player);
    }

    public void WriteMovementToSave(
        GameRuntimeState state)
    {
        if (state == null || state.Player == null)
            return;

        _shipMovementService.WriteToPlayerState(
            state.Player);
    }
}