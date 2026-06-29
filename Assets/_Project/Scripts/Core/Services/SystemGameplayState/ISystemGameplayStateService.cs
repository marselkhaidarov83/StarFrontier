/// <summary>
/// Сервис доступа к временным состояниям локального gameplay.
/// </summary>
public interface ISystemGameplayStateService
{
    SystemGameplayRuntimeState State { get; }

    PlayerControlRuntimeState Control { get; }
    ShipMovementRuntimeState Movement { get; }
    SystemCameraRuntimeState Camera { get; }
    TargetingRuntimeState Targeting { get; }
    InteractionRuntimeState Interaction { get; }

    void ResetAll();
    void MarkInitialized();
    void ResetFrameFlags();
}