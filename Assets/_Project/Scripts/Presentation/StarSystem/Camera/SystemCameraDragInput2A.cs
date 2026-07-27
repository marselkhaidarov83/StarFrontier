using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SystemCameraDragInput2A :
    MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [SerializeField] private SystemCameraController2A cameraController;

    private bool _isDragging;

    public void SetCameraController(SystemCameraController2A controller)
    {
        cameraController = controller;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (cameraController == null)
            return;

        if (!cameraController.IsSystemCameraActive)
            return;

        _isDragging = true;
        cameraController.EnterFreeLook();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging)
            return;

        if (cameraController == null)
            return;

        cameraController.MoveFreeLookByScreenDelta(eventData.delta);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _isDragging = false;
    }
}