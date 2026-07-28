using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Универсальный компонент выбора цели.
///
/// Можно устанавливать на станцию,
/// точку выхода или другой объект сцены.
///
/// Для планет используется существующий
/// PlanetSelectableView2.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class TargetSelectableView2A :
    MonoBehaviour,
    IPointerClickHandler
{
    [Header("Target")]

    [SerializeField]
    private string targetId;

    [SerializeField]
    private SystemGameplayTargetType
        targetType =
            SystemGameplayTargetType.None;

    [SerializeField]
    private bool isInteractable =
        true;

    [SerializeField]
    private bool isAvailable =
        true;

    [Header("Visual")]

    [SerializeField]
    private TargetMarkerView2A
        targetMarkerView;

    private ITargetService2A
        _targetService;

    private void Awake()
    {
        ResolveService();

        if (targetMarkerView == null)
        {
            targetMarkerView =
                GetComponent<
                    TargetMarkerView2A>();
        }

        InitializeMarker();
    }

    public void Initialize(
        string id,
        SystemGameplayTargetType type,
        bool interactable,
        bool available)
    {
        targetId =
            id ?? string.Empty;

        targetType =
            type;

        isInteractable =
            interactable;

        isAvailable =
            available;

        ResolveService();
        InitializeMarker();
    }

    public void SetAvailable(
        bool available)
    {
        isAvailable =
            available;

        if (targetMarkerView != null)
        {
            targetMarkerView
                .SetAvailable(
                    available);
        }
    }

    public void SetInteractable(
        bool interactable)
    {
        isInteractable =
            interactable;

        if (targetMarkerView != null)
        {
            targetMarkerView
                .SetInteractable(
                    interactable);
        }
    }

    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (!ResolveService())
            return;

        Vector3 currentPosition =
            transform.position;

        bool selected =
            _targetService
                .TrySelectTarget(
                    targetId,
                    targetType,
                    new Vector2(
                        currentPosition.x,
                        currentPosition.y),
                    isInteractable,
                    isAvailable,
                    true,
                    out TargetSelectionFailReason2A
                        failReason);

        if (selected)
            return;

        Debug.LogWarning(
            "[TargetSelectableView2A] " +
            "Target selection failed. " +
            "TargetId = " +
            targetId +
            " | Type = " +
            targetType +
            " | Reason = " +
            failReason,
            this);
    }

    private bool ResolveService()
    {
        if (_targetService != null)
            return true;

        if (Bootstrapper.Instance == null)
            return false;

        if (Bootstrapper.Instance
                .ServiceRegistry == null)
        {
            return false;
        }

        _targetService =
            Bootstrapper.Instance
                .ServiceRegistry
                .Get<ITargetService2A>();

        return
            _targetService != null;
    }

    private void InitializeMarker()
    {
        if (targetMarkerView == null)
            return;

        targetMarkerView.Initialize(
            targetId,
            isInteractable,
            isAvailable);
    }
}