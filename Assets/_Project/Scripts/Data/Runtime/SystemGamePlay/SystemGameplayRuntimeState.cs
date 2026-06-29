using System;

/// <summary>
/// Общий временный контейнер состояния локального gameplay
/// внутри звёздной системы.
///
/// Не является save-state.
/// Не сохраняется в JSON.
/// Создаётся заново при старте приложения или сбросе runtime-сессии.
/// </summary>
[Serializable]
public sealed class SystemGameplayRuntimeState
{
    public PlayerControlRuntimeState Control { get; } =
        new PlayerControlRuntimeState();

    public ShipMovementRuntimeState Movement { get; } =
        new ShipMovementRuntimeState();

    public SystemCameraRuntimeState Camera { get; } =
        new SystemCameraRuntimeState();

    public TargetingRuntimeState Targeting { get; } =
        new TargetingRuntimeState();

    public InteractionRuntimeState Interaction { get; } =
        new InteractionRuntimeState();

    public bool IsInitialized { get; private set; }

    public void MarkInitialized()
    {
        IsInitialized = true;
    }

    public void ResetAll()
    {
        Control.ResetAll();
        Movement.ResetAll();
        Camera.ResetAll();
        Targeting.ResetAll();
        Interaction.ResetAll();

        IsInitialized = false;
    }

    /// <summary>
    /// Сбрасывает только одноразовые флаги кадра.
    /// Это понадобится будущим сервисам в конце кадра.
    /// </summary>
    public void ResetFrameFlags()
    {
        Control.ResetFrameInput();
        Interaction.ResetFrameFlags();
    }
}