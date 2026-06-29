using System;

/// <summary>
/// Временное состояние взаимодействия с текущей целью.
/// </summary>
[Serializable]
public sealed class InteractionRuntimeState
{
    public string CurrentInteractionTargetId { get; private set; } =
        string.Empty;

    public SystemGameplayTargetType CurrentInteractionTargetType
    {
        get;
        private set;
    } = SystemGameplayTargetType.None;

    public bool CanInteract { get; private set; }
    public bool IsInteractionInProgress { get; private set; }
    public bool InteractionPressedThisFrame { get; private set; }
    public bool InteractionCompletedThisFrame { get; private set; }
    public bool InteractionFailedThisFrame { get; private set; }

    public float HoldProgressNormalized { get; private set; }
    public float CooldownRemainingSeconds { get; private set; }

    public string FailReason { get; private set; } =
        string.Empty;

    public void SetAvailableInteraction(
        string targetId,
        SystemGameplayTargetType targetType,
        bool canInteract,
        string failReason = "")
    {
        CurrentInteractionTargetId = targetId ?? string.Empty;
        CurrentInteractionTargetType = targetType;
        CanInteract = canInteract;
        FailReason = failReason ?? string.Empty;
    }

    public void MarkPressedThisFrame()
    {
        InteractionPressedThisFrame = true;
    }

    public void SetInteractionInProgress(
        bool isInProgress,
        float holdProgressNormalized)
    {
        IsInteractionInProgress = isInProgress;
        HoldProgressNormalized =
            Clamp01(holdProgressNormalized);
    }

    public void MarkCompleted()
    {
        InteractionCompletedThisFrame = true;
        InteractionFailedThisFrame = false;
        IsInteractionInProgress = false;
        HoldProgressNormalized = 0f;
        FailReason = string.Empty;
    }

    public void MarkFailed(string reason)
    {
        InteractionFailedThisFrame = true;
        InteractionCompletedThisFrame = false;
        IsInteractionInProgress = false;
        HoldProgressNormalized = 0f;
        FailReason = reason ?? string.Empty;
    }

    public void SetCooldown(float cooldownSeconds)
    {
        CooldownRemainingSeconds =
            Math.Max(0f, cooldownSeconds);
    }

    public void TickCooldown(float deltaTime)
    {
        CooldownRemainingSeconds =
            Math.Max(0f, CooldownRemainingSeconds - deltaTime);
    }

    public void ResetFrameFlags()
    {
        InteractionPressedThisFrame = false;
        InteractionCompletedThisFrame = false;
        InteractionFailedThisFrame = false;
    }

    public void ResetAll()
    {
        CurrentInteractionTargetId = string.Empty;
        CurrentInteractionTargetType = SystemGameplayTargetType.None;

        CanInteract = false;
        IsInteractionInProgress = false;
        InteractionPressedThisFrame = false;
        InteractionCompletedThisFrame = false;
        InteractionFailedThisFrame = false;

        HoldProgressNormalized = 0f;
        CooldownRemainingSeconds = 0f;
        FailReason = string.Empty;
    }

    private static float Clamp01(float value)
    {
        if (value < 0f)
            return 0f;

        if (value > 1f)
            return 1f;

        return value;
    }
}