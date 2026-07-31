using UnityEngine;

/// <summary>
/// Принимает сырой ввод игрока и обновляет
/// PlayerControlRuntimeState через TickService.
/// </summary>
public interface IPlayerControlService : ITickable
{
    bool IsEnabled { get; }

    Vector2 RawMoveInput { get; }

    Vector2 ProcessedMoveInput { get; }

    void SetEnabled(bool isEnabled);

    void SetRawMoveInput(Vector2 rawMoveInput);

    void PressInteract();

    void ReleaseInteract();

    void PressRecenterCamera();

    void ResetAll();
}