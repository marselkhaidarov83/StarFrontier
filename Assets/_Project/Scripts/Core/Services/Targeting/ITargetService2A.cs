using UnityEngine;

public interface ITargetService2A
{
    TargetingRuntimeState State { get; }

    bool HasTarget { get; }

    bool TrySelectTarget(
        string targetId,
        SystemGameplayTargetType targetType,
        Vector2 worldPosition,
        bool isInteractable,
        bool isAvailable,
        bool wasSelectedByPlayer,
        out TargetSelectionFailReason2A failReason);

    bool TryRefreshCurrentTarget(
        string targetId,
        Vector2 worldPosition,
        bool isInteractable,
        bool isAvailable,
        out TargetSelectionFailReason2A failReason);

    bool IsCurrentTarget(
        string targetId);

    void ClearTarget();
}