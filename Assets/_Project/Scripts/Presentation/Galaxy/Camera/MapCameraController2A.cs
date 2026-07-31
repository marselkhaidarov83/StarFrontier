using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

public class MapCameraController2A : MonoBehaviour
{
    [Header("Map Size")]
    [SerializeField] private float mapWidth = 10.8f;
    [SerializeField] private float mapHeight = 19.2f;

    [Header("Zoom")]
    [SerializeField] private float minZoom = 2.5f;

    [Tooltip("Запасное значение до автоматического расчёта")]
    [SerializeField] private float fallbackMaxZoom = 9.6f;
    [SerializeField] private float touchZoomSpeed = 0.5f;
    [SerializeField] private float mouseZoomSpeed = 0.15f;
    private float maxZoom;

    private Camera cam;

    private Vector3 dragOriginWorld;
    private bool isDragging;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (cam == null)
        {
            Debug.LogError(
                "[MapCameraController2A] Скрипт должен находиться на объекте с Camera."
            );

            return;
        }

        maxZoom = Mathf.Max(minZoom, fallbackMaxZoom);
    }

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    private void Start()
    {
        ClampCameraPosition();
    }

    private void Update()
    {
        HandleMouseDrag();
        HandleMouseZoom();

        HandleTouchDrag();
        HandleTouchZoom();

        ClampCameraPosition();
    }

    private void HandleMouseDrag()
    {
        if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
            return;

        if (Mouse.current == null)
            return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            dragOriginWorld = GetWorldPoint(mousePosition);
            isDragging = true;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (Mouse.current.leftButton.isPressed && isDragging)
        {
            Vector3 currentWorld = GetWorldPoint(mousePosition);
            Vector3 difference = dragOriginWorld - currentWorld;

            transform.position += difference;
        }
    }

    private void HandleMouseZoom2()
    {
        if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
            return;

        if (Mouse.current == null)
            return;

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            cam.orthographicSize -= scroll * mouseZoomSpeed * Time.deltaTime;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
        }
    }

    private void HandleMouseZoom()
    {
        if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
            return;

        if (Mouse.current == null)
            return;

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            cam.orthographicSize -= scroll * mouseZoomSpeed;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
        }
    }

    private void HandleTouchDrag()
    {
        var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;

        if (touches.Count != 1)
            return;

        var touch = touches[0];

        Vector2 delta = touch.delta;

        if (delta.sqrMagnitude < 0.01f)
            return;

        float cameraHeight = cam.orthographicSize * 2f;
        float cameraWidth = cameraHeight * cam.aspect;

        float worldDeltaX = delta.x / Screen.width * cameraWidth;
        float worldDeltaY = delta.y / Screen.height * cameraHeight;

        Vector3 move = new Vector3(
            -worldDeltaX,
            -worldDeltaY,
            0f
        );

        transform.position += move;
    }

    private void HandleTouchZoom()
    {
        var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;

        if (touches.Count != 2)
            return;

        isDragging = false;

        var touch1 = touches[0];
        var touch2 = touches[1];

        Vector2 touch1Current = touch1.screenPosition;
        Vector2 touch2Current = touch2.screenPosition;

        Vector2 touch1Previous = touch1Current - touch1.delta;
        Vector2 touch2Previous = touch2Current - touch2.delta;

        float previousDistance = Vector2.Distance(touch1Previous, touch2Previous);
        float currentDistance = Vector2.Distance(touch1Current, touch2Current);

        if (previousDistance <= 0.01f)
            return;

        float zoomFactor = currentDistance / previousDistance;
        float targetSize = cam.orthographicSize / zoomFactor;

        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            targetSize,
            touchZoomSpeed
        );

        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
    }

    private Vector3 GetWorldPoint(Vector2 screenPosition)
    {
        Vector3 position = new Vector3(screenPosition.x, screenPosition.y, Mathf.Abs(transform.position.z));
        return cam.ScreenToWorldPoint(position);
    }

    private void ClampCameraPosition()
    {
        float halfMapWidth = mapWidth / 2f;
        float halfMapHeight = mapHeight / 2f;

        float cameraHalfHeight = cam.orthographicSize;
        float cameraHalfWidth = cam.orthographicSize * cam.aspect;

        float minX = -halfMapWidth + cameraHalfWidth;
        float maxX = halfMapWidth - cameraHalfWidth;

        float minY = -halfMapHeight + cameraHalfHeight;
        float maxY = halfMapHeight - cameraHalfHeight;

        Vector3 position = transform.position;

        position.x = minX > maxX ? 0f : Mathf.Clamp(position.x, minX, maxX);
        position.y = minY > maxY ? 0f : Mathf.Clamp(position.y, minY, maxY);
        position.z = -10f;

        transform.position = position;
    }

    public void SetZoomOutLimit(
     float fitOrthographicSize,
     bool applyImmediately = true)
    {
        if (float.IsNaN(fitOrthographicSize) ||
            float.IsInfinity(fitOrthographicSize) ||
            fitOrthographicSize <= 0f)
        {
            Debug.LogError(
                "[MapCameraController2A] Некорректный размер камеры: " +
                fitOrthographicSize
            );

            return;
        }

        maxZoom = Mathf.Max(
            minZoom,
            fitOrthographicSize
        );

        if (cam == null)
            return;

        if (applyImmediately)
        {
            // При открытии карты сразу показываем всё содержимое.
            cam.orthographicSize = maxZoom;
        }
        else
        {
            // При обычном обновлении лишь удерживаем текущий масштаб
            // внутри допустимых границ.
            cam.orthographicSize = Mathf.Clamp(
                cam.orthographicSize,
                minZoom,
                maxZoom
            );
        }

        ClampCameraPosition();

        Debug.Log(
            "[MapCameraController2A] Zoom out limit updated. " +
            "fitSize = " + fitOrthographicSize +
            " | maxZoom = " + maxZoom +
            " | currentSize = " + cam.orthographicSize
        );
    }
}