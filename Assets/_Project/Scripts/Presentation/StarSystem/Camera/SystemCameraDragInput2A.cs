using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Передаёт drag с карты в SystemCameraController2A.
///
/// Не изменяет Camera или Transform напрямую.
/// </summary>
[DisallowMultipleComponent]
public sealed class SystemCameraDragInput2A :
    MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [SerializeField]
    private SystemCameraController2A
        cameraController;

    [Header("Input Blocking")]

    [SerializeField]
    private GameObject[] blockingUiRoots;

    [SerializeField]
    private bool inputEnabled = true;

    private bool _isDragging;

    private void Reset()
    {
        ResolveController();
    }

    private void Awake()
    {
        ResolveController();
    }

    private void OnDisable()
    {
        _isDragging = false;
    }

    public void SetCameraController(
        SystemCameraController2A controller)
    {
        cameraController =
            controller;
    }

    public void SetInputEnabled(
        bool isEnabled)
    {
        inputEnabled =
            isEnabled;

        if (!inputEnabled)
        {
            _isDragging =
                false;
        }
    }

    public void OnBeginDrag(
        PointerEventData eventData)
    {
        if (!CanProcessInput())
            return;

        _isDragging =
            true;

        cameraController
            .EnterFreeLook();
    }

    public void OnDrag(
        PointerEventData eventData)
    {
        if (!_isDragging ||
            !CanProcessInput())
        {
            return;
        }

        cameraController
            .MoveFreeLookByScreenDelta(
                eventData.delta);
    }

    public void OnEndDrag(
        PointerEventData eventData)
    {
        _isDragging =
            false;
    }

    private bool CanProcessInput()
    {
        ResolveController();

        if (!inputEnabled ||
            cameraController == null ||
            !cameraController
                .IsSystemCameraActive)
        {
            return false;
        }

        if (blockingUiRoots == null)
            return true;

        foreach (
            GameObject root
            in blockingUiRoots)
        {
            if (root != null &&
                root.activeInHierarchy)
            {
                return false;
            }
        }

        return true;
    }

    private void ResolveController()
    {
        if (cameraController != null)
            return;

        cameraController =
            GetComponent<
                SystemCameraController2A>();

        if (cameraController == null)
        {
            cameraController =
                GetComponentInParent<
                    SystemCameraController2A>();
        }
    }
}