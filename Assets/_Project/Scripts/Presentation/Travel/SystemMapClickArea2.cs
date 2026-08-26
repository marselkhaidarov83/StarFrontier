using System;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SystemMapClickArea2 : CustomMonoBehaviour,
    IPointerDownHandler,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("Click / Drag Filter")]
    [SerializeField] private float clickMoveThresholdPixels = 25f;

    public event Action<Vector3> EmptyMapClicked;

    private Vector2 _pointerDownPosition;
    private bool _isPointerDown;
    private bool _wasDragged;
    private int _activePointerId;
    private IPlayerAttackService _playerAttackService;

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPointerDown = true;
        _wasDragged = false;
        _activePointerId = eventData.pointerId;
        _pointerDownPosition = eventData.position;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!IsActivePointer(eventData))
            return;

        _wasDragged = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsActivePointer(eventData))
            return;

        float distance = Vector2.Distance(
            _pointerDownPosition,
            eventData.position
        );

        if (distance > clickMoveThresholdPixels)
            _wasDragged = true;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsActivePointer(eventData))
            return;

        _wasDragged = true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.pointerEnter != gameObject)
            return;

        if (ShouldIgnoreAsDrag(eventData))
        {
            if (IsDebug())
            {
                Debug.Log(
                    "[SystemMapClickArea2] Empty map click ignored because pointer movement was drag. " +
                    "Distance = " + Vector2.Distance(_pointerDownPosition, eventData.position)
                );
            }

            return;
        }

        ResolvePlayerAttackService();
        _playerAttackService?.ClearSelectedTargetIfNoAssignedWeapons();

        Vector3 worldPosition = eventData.pointerCurrentRaycast.worldPosition;

        if (IsDebug())
        {
            Debug.Log("[SystemMapClickArea2] Empty map clicked. WorldPosition = " + worldPosition);
        }

        EmptyMapClicked?.Invoke(new Vector3(
            worldPosition.x,
            worldPosition.y,
            -2f
        ));

        ResetPointerState();
    }

    private void ResolvePlayerAttackService()
    {
        if (_playerAttackService != null)
            return;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _playerAttackService);
    }

    private bool ShouldIgnoreAsDrag(PointerEventData eventData)
    {
        if (!_isPointerDown)
            return false;

        if (_wasDragged)
            return true;

        float distance = Vector2.Distance(
            _pointerDownPosition,
            eventData.position
        );

        return distance > clickMoveThresholdPixels;
    }

    private bool IsActivePointer(PointerEventData eventData)
    {
        if (!_isPointerDown)
            return false;

        return eventData.pointerId == _activePointerId;
    }

    private void ResetPointerState()
    {
        _isPointerDown = false;
        _wasDragged = false;
        _activePointerId = 0;
        _pointerDownPosition = Vector2.zero;
    }
}