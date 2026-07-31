/// <summary>
/// Хранит временное состояние локального gameplay.
/// </summary>
public sealed class SystemGameplayStateService :
    ISystemGameplayStateService
{
    public SystemGameplayRuntimeState State { get; } =
        new SystemGameplayRuntimeState();

    public PlayerControlRuntimeState Control =>
        State.Control;

    public ShipMovementRuntimeState Movement =>
        State.Movement;

    public SystemBoundsRuntimeState Bounds =>
        State.Bounds;

    public SystemCameraRuntimeState Camera =>
        State.Camera;

    public TargetingRuntimeState Targeting =>
        State.Targeting;

    public InteractionRuntimeState Interaction =>
        State.Interaction;

    public void ResetAll()
    {
        State.ResetAll();
    }

    public void MarkInitialized()
    {
        State.MarkInitialized();
    }

    public void ResetFrameFlags()
    {
        State.ResetFrameFlags();
    }
}