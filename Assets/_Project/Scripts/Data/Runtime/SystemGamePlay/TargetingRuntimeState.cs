using System;
using UnityEngine;

/// <summary>
/// Временное состояние выбранной цели.
/// </summary>
[Serializable]
public sealed class TargetingRuntimeState
{
    public string CurrentTargetId { get; private set; } =
        string.Empty;

    public SystemGameplayTargetType CurrentTargetType
    {
        get;
        private set;
    } = SystemGameplayTargetType.None;

    public Vector2 CurrentTargetWorldPosition { get; private set; }
    public float CurrentTargetDistance { get; private set; }

    public bool HasTarget =>
        !string.IsNullOrWhiteSpace(CurrentTargetId)
        && CurrentTargetType != SystemGameplayTargetType.None;

    public bool IsTargetInteractable { get; private set; }
    public bool IsTargetInRange { get; private set; }
    public bool WasSelectedByPlayer { get; private set; }

    public void SetTarget(
        string targetId,
        SystemGameplayTargetType targetType,
        Vector2 worldPosition,
        float distance,
        bool isInteractable,
        bool isInRange,
        bool wasSelectedByPlayer)
    {
        CurrentTargetId = targetId ?? string.Empty;
        CurrentTargetType = targetType;
        CurrentTargetWorldPosition = worldPosition;
        CurrentTargetDistance = Mathf.Max(0f, distance);
        IsTargetInteractable = isInteractable;
        IsTargetInRange = isInRange;
        WasSelectedByPlayer = wasSelectedByPlayer;
    }

    public void UpdateTargetDistance(
        float distance,
        bool isInRange)
    {
        CurrentTargetDistance = Mathf.Max(0f, distance);
        IsTargetInRange = isInRange;
    }

    public void ClearTarget()
    {
        CurrentTargetId = string.Empty;
        CurrentTargetType = SystemGameplayTargetType.None;
        CurrentTargetWorldPosition = Vector2.zero;
        CurrentTargetDistance = 0f;

        IsTargetInteractable = false;
        IsTargetInRange = false;
        WasSelectedByPlayer = false;
    }

    public void ResetAll()
    {
        ClearTarget();
    }
}
