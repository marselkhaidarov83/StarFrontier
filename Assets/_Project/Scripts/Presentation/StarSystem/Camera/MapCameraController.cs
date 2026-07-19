using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

public class MapCameraController : MonoBehaviour
{
    [Header("System Camera")]
    [SerializeField]
    private SystemCameraController2A systemCameraController;

    [Header("Input Modes")]
    [SerializeField] private bool handleDrag = true;
    [SerializeField] private bool handleZoom = true;
    [SerializeField] private bool clampPosition = true;

    [Header("Map Size")]
    [Min(0.01f)]
    [SerializeField] private float mapWidth = 1200f;

    [Min(0.01f)]
    [SerializeField] private float mapHeight = 2000f;

    [Header("Zoom")]
    [Min(0.01f)]
    [SerializeField] private float minZoom = 30f;

    [Min(0.01f)]
    [SerializeField] private float maxZoom = 2000f;

    [Tooltip("100 — обычная чувствительность. 150–200 — быстрый зум.")]
    [Min(0.01f)]
    [SerializeField] private float touchZoomSpeed = 150f;

    [Tooltip("Изменение Orthographic Size за один шаг колеса.")]
    [Min(0.01f)]
    [SerializeField] private float mouseZoomSpeed = 150f;

    [Tooltip("Чем меньше значение, тем быстрее камера достигает выбранного масштаба.")]
    [Min(0.001f)]
    [SerializeField] private float zoomSmoothTime = 0.03f;

    private Camera cam;

    private float targetZoom;
    private float zoomVelocity;

    private Vector3 dragOriginWorld;
    private bool isDragging;

    public float MapWidth => mapWidth;
    public float MapHeight => mapHeight;

    /// <summary>
    /// Максимальный Orthographic Size, при котором камера
    /// ещё полностью помещается внутри карты.
    /// </summary>
    public float MaxOrthographicSizeWithoutEmptySpace
    {
        get
        {
            ResolveCamera();

            if (cam == null)
                return Mathf.Max(0.01f, maxZoom);

            float safeMapWidth = Mathf.Max(0.01f, mapWidth);
            float safeMapHeight = Mathf.Max(0.01f, mapHeight);
            float safeAspect = Mathf.Max(0.01f, cam.aspect);

            float maximumByHeight = safeMapHeight * 0.5f;
            float maximumByWidth = safeMapWidth * 0.5f / safeAspect;

            return Mathf.Max(
                0.01f,
                Mathf.Min(
                    maxZoom,
                    maximumByHeight,
                    maximumByWidth
                )
            );
        }
    }

    private void OnValidate()
    {
        mapWidth = Mathf.Max(0.01f, mapWidth);
        mapHeight = Mathf.Max(0.01f, mapHeight);

        minZoom = Mathf.Max(0.01f, minZoom);
        maxZoom = Mathf.Max(minZoom, maxZoom);

        touchZoomSpeed = Mathf.Max(0.01f, touchZoomSpeed);
        mouseZoomSpeed = Mathf.Max(0.01f, mouseZoomSpeed);
        zoomSmoothTime = Mathf.Max(0.001f, zoomSmoothTime);
    }

    private void Awake()
    {
        ResolveCamera();

        if (cam == null)
        {
            Debug.LogError(
                "MapCameraController должен висеть на объекте с Camera."
            );

            return;
        }

        SyncTargetZoomWithCamera();
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
        ResolveCamera();
        SyncTargetZoomWithCamera();

        if (clampPosition &&
            !IsSystemCameraControllingPosition())
        {
            ClampCameraPosition();
        }
    }

    private void Update()
    {
        if (cam == null)
            return;

        int touchCount =
            UnityEngine.InputSystem.EnhancedTouch.Touch
                .activeTouches.Count;

        /*
         * На системной карте ручное перемещение обрабатывает
         * SystemCameraDragInput2A через SystemCameraController2A.
         *
         * MapCameraController напрямую двигает камеру только там,
         * где системный контроллер не активен.
         */
        if (handleDrag &&
            !IsSystemCameraControllingPosition())
        {
            if (touchCount == 0)
                HandleMouseDrag();
            else if (touchCount == 1)
                HandleTouchDrag();
        }

        if (handleZoom)
        {
            if (touchCount == 0)
                HandleMouseZoom();
            else if (touchCount == 2)
                HandleTouchZoom();

            ApplyZoom();
        }

        /*
         * Для обычных карт MapCameraController ограничивает позицию сам.
         * Для системной карты это сделает SystemCameraController2A
         * в LateUpdate после применения нового зума.
         */
        if (clampPosition &&
            !IsSystemCameraControllingPosition())
        {
            ClampCameraPosition();
        }
    }

    private void ResolveCamera()
    {
        if (cam == null)
            cam = GetComponent<Camera>();
    }

    private void ApplyZoom()
    {
        targetZoom = ClampZoom(targetZoom);

        cam.orthographicSize = Mathf.SmoothDamp(
            cam.orthographicSize,
            targetZoom,
            ref zoomVelocity,
            zoomSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime
        );
    }

    private void HandleMouseDrag()
    {
        if (UnityEngine.InputSystem.EnhancedTouch.Touch
                .activeTouches.Count > 0)
        {
            return;
        }

        if (Mouse.current == null)
            return;

        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            dragOriginWorld = GetWorldPoint(mousePosition);
            isDragging = true;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (!Mouse.current.leftButton.isPressed ||
            !isDragging)
        {
            return;
        }

        Vector3 currentWorld =
            GetWorldPoint(mousePosition);

        Vector3 difference =
            dragOriginWorld - currentWorld;

        transform.position += difference;
    }

    private void HandleMouseZoom()
    {
        if (Mouse.current == null)
            return;

        float scroll =
            Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) <= 0.01f)
            return;

        BeginManualZoom();

        /*
         * Стандартное колесо Input System часто возвращает шаг 120.
         * Нормализация предотвращает слишком резкий скачок.
         */
        float normalizedScroll = scroll / 120f;

        targetZoom -=
            normalizedScroll * mouseZoomSpeed;

        targetZoom = ClampZoom(targetZoom);
    }

    private void HandleTouchDrag()
    {
        var touches =
            UnityEngine.InputSystem.EnhancedTouch.Touch
                .activeTouches;

        if (touches.Count != 1)
            return;

        var touch = touches[0];

        Vector2 delta = touch.delta;

        if (delta.sqrMagnitude < 0.01f)
            return;

        float cameraHeight =
            cam.orthographicSize * 2f;

        float cameraWidth =
            cameraHeight * cam.aspect;

        float worldDeltaX =
            delta.x / Screen.width * cameraWidth;

        float worldDeltaY =
            delta.y / Screen.height * cameraHeight;

        Vector3 move = new Vector3(
            -worldDeltaX,
            -worldDeltaY,
            0f
        );

        transform.position += move;
    }

    private void HandleTouchZoom()
    {
        var touches =
            UnityEngine.InputSystem.EnhancedTouch.Touch
                .activeTouches;

        if (touches.Count != 2)
            return;

        isDragging = false;

        var touch1 = touches[0];
        var touch2 = touches[1];

        Vector2 touch1Current =
            touch1.screenPosition;

        Vector2 touch2Current =
            touch2.screenPosition;

        Vector2 touch1Previous =
            touch1Current - touch1.delta;

        Vector2 touch2Previous =
            touch2Current - touch2.delta;

        float previousDistance = Vector2.Distance(
            touch1Previous,
            touch2Previous
        );

        float currentDistance = Vector2.Distance(
            touch1Current,
            touch2Current
        );

        if (previousDistance <= 0.01f ||
            currentDistance <= 0.01f)
        {
            return;
        }

        float distanceDelta =
            currentDistance - previousDistance;

        /*
         * Игнорируем микродрожание пальцев.
         */
        if (Mathf.Abs(distanceDelta) < 0.1f)
            return;

        BeginManualZoom();

        /*
         * 100 даёт sensitivity = 1.
         * 150 даёт sensitivity = 1.5.
         *
         * Пальцы расходятся:
         * Orthographic Size уменьшается — камера приближается.
         *
         * Пальцы сходятся:
         * Orthographic Size увеличивается — камера отдаляется.
         */
        float sensitivity =
            Mathf.Max(0.01f, touchZoomSpeed) * 0.01f;

        float zoomFactor = Mathf.Pow(
            previousDistance / currentDistance,
            sensitivity
        );

        targetZoom *= zoomFactor;
        targetZoom = ClampZoom(targetZoom);
    }

    private void BeginManualZoom()
    {
        /*
         * При ручном зуме отменяем автоматическое
         * восстановление масштаба кнопкой «К кораблю».
         */
        if (systemCameraController != null &&
            systemCameraController.Mode ==
                SystemCameraMode2A.ReturningToShip)
        {
            systemCameraController.NotifyManualZoomStarted();

            /*
             * Начинаем жест от фактического масштаба,
             * а не от старой автоматической цели.
             */
            SyncTargetZoomWithCamera();
            return;
        }

        systemCameraController?.NotifyManualZoomStarted();
    }

    public void SetTargetZoom(float value)
    {
        targetZoom = ClampZoom(value);
        zoomVelocity = 0f;
    }

    public void SetZoomImmediate(float value)
    {
        ResolveCamera();

        if (cam == null)
            return;

        float clampedValue = ClampZoom(value);

        targetZoom = clampedValue;
        cam.orthographicSize = clampedValue;
        zoomVelocity = 0f;
    }

    public void SyncTargetZoomWithCamera()
    {
        ResolveCamera();

        if (cam == null)
            return;

        targetZoom = ClampZoom(
            cam.orthographicSize
        );

        zoomVelocity = 0f;
    }

    public bool IsZoomAtTarget(float tolerance)
    {
        if (cam == null)
            return true;

        return Mathf.Abs(
            cam.orthographicSize - targetZoom
        ) <= tolerance;
    }

    private float ClampZoom(float value)
    {
        float effectiveMaximum =
            MaxOrthographicSizeWithoutEmptySpace;

        /*
         * Защита на случай, если Min Zoom случайно
         * больше безопасного максимума.
         */
        float effectiveMinimum =
            Mathf.Min(minZoom, effectiveMaximum);

        return Mathf.Clamp(
            value,
            effectiveMinimum,
            effectiveMaximum
        );
    }

    private bool IsSystemCameraControllingPosition()
    {
        return systemCameraController != null &&
               systemCameraController.IsSystemCameraActive;
    }

    private Vector3 GetWorldPoint(
        Vector2 screenPosition
    )
    {
        Vector3 position = new Vector3(
            screenPosition.x,
            screenPosition.y,
            Mathf.Abs(transform.position.z)
        );

        return cam.ScreenToWorldPoint(position);
    }

    /// <summary>
    /// Ограничивает переданную позицию размерами карты.
    /// Карта считается расположенной по центру координат:
    /// X от -mapWidth/2 до +mapWidth/2,
    /// Y от -mapHeight/2 до +mapHeight/2.
    /// </summary>
    public Vector3 ClampPositionToMap(
        Vector3 position,
        float orthographicSize
    )
    {
        ResolveCamera();

        if (cam == null)
            return position;

        float halfMapWidth =
            mapWidth * 0.5f;

        float halfMapHeight =
            mapHeight * 0.5f;

        float cameraHalfHeight =
            Mathf.Max(0.01f, orthographicSize);

        float cameraHalfWidth =
            cameraHalfHeight *
            Mathf.Max(0.01f, cam.aspect);

        float minX =
            -halfMapWidth + cameraHalfWidth;

        float maxX =
            halfMapWidth - cameraHalfWidth;

        float minY =
            -halfMapHeight + cameraHalfHeight;

        float maxY =
            halfMapHeight - cameraHalfHeight;

        Vector3 result = position;

        if (minX > maxX)
        {
            result.x = 0f;
        }
        else
        {
            result.x = Mathf.Clamp(
                result.x,
                minX,
                maxX
            );
        }

        if (minY > maxY)
        {
            result.y = 0f;
        }
        else
        {
            result.y = Mathf.Clamp(
                result.y,
                minY,
                maxY
            );
        }

        return result;
    }

    private void ClampCameraPosition()
    {
        if (cam == null)
            return;

        transform.position = ClampPositionToMap(
            transform.position,
            cam.orthographicSize
        );
    }
}