using UnityEngine;

/// <summary>
/// Управляет расчётом движения корабля игрока
/// внутри локальной сцены системы.
/// </summary>
public interface IShipMovementService : ITickable
{
    bool IsEnabled { get; }

    ShipMovementRuntimeState State { get; }

    void SetEnabled(bool isEnabled);

    void SetShipStats(ShipFinalStats shipStats);

    void ClearShipStats();

    void SetPosition(Vector2 position);

    void SetFacingDirection(Vector2 direction);

    void InitializeFromPlayerState(PlayerState playerState);

    void WriteToPlayerState(PlayerState playerState);

    void StopImmediately();

    void ResetAll();
}