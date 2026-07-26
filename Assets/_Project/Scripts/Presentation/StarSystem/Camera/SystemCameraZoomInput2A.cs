using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Serialization;

using EnhancedTouch =
    UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Передаёт команды масштабирования
/// в SystemCameraController2A.
///
/// Колесо мыши:
/// - учитывается только направление вращения;
/// - величина raw scroll игнорируется;
/// - каждый принятый шаг имеет одинаковую величину;
/// - частота шагов ограничена;
/// - быстрое вращение не создаёт резкого скачка.
///
/// Компонент не изменяет Camera или Transform напрямую.
/// </summary>
[DisallowMultipleComponent]
public sealed class SystemCameraZoomInput2A :
    MonoBehaviour
{
    [Header("Camera Controller")]

    [SerializeField]
    private SystemCameraController2A
        cameraController;

    [Header("Mouse Wheel")]

    [SerializeField]
    private bool enableMouseWheel = true;

    [Tooltip(
        "Размер одного фиксированного шага колеса. " +
        "Скорость вращения колесика на это значение не влияет.")]
    [FormerlySerializedAs("mouseInputMultiplier")]
    [Min(0.01f)]
    [SerializeField]
    private float mouseZoomStep = 0.5f;

    [Tooltip(
        "Минимальный интервал между zoom-шагами. " +
        "Ограничивает максимальную скорость масштабирования.")]
    [Range(0.02f, 0.5f)]
    [SerializeField]
    private float mouseStepInterval = 0.08f;

    [Tooltip(
        "Значения ниже порога считаются шумом.")]
    [Min(0.0001f)]
    [SerializeField]
    private float mouseScrollThreshold = 0.01f;

    [SerializeField]
    private bool invertMouseWheel = false;

    [Header("Touch Pinch")]

    [SerializeField]
    private bool enablePinch = true;

    [Min(0.01f)]
    [SerializeField]
    private float pinchInputMultiplier = 12f;

    [Tooltip(
        "Минимальное изменение расстояния между пальцами, " +
        "которое считается pinch-командой.")]
    [Min(0.01f)]
    [SerializeField]
    private float pinchDeadZonePixels = 0.5f;

    [Header("Input Blocking")]

    [SerializeField]
    private GameObject[] blockingUiRoots;

    [SerializeField]
    private bool inputEnabled = true;

    [Header("Diagnostics")]

    [SerializeField]
    private bool logMouseSteps = false;

    private bool _enabledEnhancedTouchHere;

    /*
     * Храним максимум одну ожидающую команду.
     *
     * Быстрое колесо не может накопить очередь
     * из большого количества zoom-шагов.
     */
    private int _queuedMouseDirection;

    private float _nextAllowedMouseStepTime;

    private void Reset()
    {
        ResolveController();
    }

    private void Awake()
    {
        ResolveController();
    }

    private void OnValidate()
    {
        mouseZoomStep =
            Mathf.Max(
                0.01f,
                mouseZoomStep);

        mouseStepInterval =
            Mathf.Clamp(
                mouseStepInterval,
                0.02f,
                0.5f);

        mouseScrollThreshold =
            Mathf.Max(
                0.0001f,
                mouseScrollThreshold);

        pinchInputMultiplier =
            Mathf.Max(
                0.01f,
                pinchInputMultiplier);

        pinchDeadZonePixels =
            Mathf.Max(
                0.01f,
                pinchDeadZonePixels);
    }

    private void OnEnable()
    {
        _queuedMouseDirection = 0;
        _nextAllowedMouseStepTime = 0f;

        if (!EnhancedTouchSupport.enabled)
        {
            EnhancedTouchSupport.Enable();
            _enabledEnhancedTouchHere = true;
        }
    }

    private void OnDisable()
    {
        _queuedMouseDirection = 0;

        if (_enabledEnhancedTouchHere &&
            EnhancedTouchSupport.enabled)
        {
            EnhancedTouchSupport.Disable();
        }

        _enabledEnhancedTouchHere = false;
    }

    private void Update()
    {
        if (!inputEnabled ||
            IsInputBlocked())
        {
            _queuedMouseDirection = 0;
            return;
        }

        ResolveController();

        if (cameraController == null ||
            !cameraController.IsSystemCameraActive)
        {
            _queuedMouseDirection = 0;
            return;
        }

        int touchCount =
            EnhancedTouch
                .activeTouches
                .Count;

        if (enablePinch &&
            touchCount == 2)
        {
            /*
             * Во время pinch не выполняем
             * ожидающую команду мыши.
             */
            _queuedMouseDirection = 0;

            HandlePinch();
            return;
        }

        if (enableMouseWheel &&
            touchCount == 0)
        {
            CaptureMouseWheelDirection();
            ProcessQueuedMouseStep();
        }
    }

    public void SetInputEnabled(
        bool isEnabled)
    {
        inputEnabled =
            isEnabled;

        if (!inputEnabled)
        {
            _queuedMouseDirection = 0;
        }
    }

    public void SetCameraController(
        SystemCameraController2A controller)
    {
        cameraController =
            controller;
    }

    /// <summary>
    /// Читает только направление колесика.
    ///
    /// Неважно, вернуло устройство:
    /// 1, 5, 20, 120 или 500.
    ///
    /// Результат всегда:
    /// +1 или -1.
    /// </summary>
    private void CaptureMouseWheelDirection()
    {
        if (Mouse.current == null)
            return;

        float rawScroll =
            Mouse.current
                .scroll
                .ReadValue()
                .y;

        if (!IsFinite(rawScroll) ||
            Mathf.Abs(rawScroll) <=
            mouseScrollThreshold)
        {
            return;
        }

        int direction =
            rawScroll > 0f
                ? 1
                : -1;

        if (invertMouseWheel)
        {
            direction *= -1;
        }

        /*
         * Не увеличиваем очередь.
         *
         * При любом количестве событий
         * хранится только одна следующая команда.
         */
        _queuedMouseDirection =
            direction;
    }

    private void ProcessQueuedMouseStep()
    {
        if (_queuedMouseDirection == 0)
            return;

        if (Time.unscaledTime <
            _nextAllowedMouseStepTime)
        {
            return;
        }

        int direction =
            _queuedMouseDirection;

        _queuedMouseDirection = 0;

        /*
         * Величина всегда фиксированная.
         *
         * rawScroll здесь уже не используется.
         */
        float zoomCommand =
            direction *
            mouseZoomStep;

        cameraController.AdjustZoom(
            zoomCommand);

        _nextAllowedMouseStepTime =
            Time.unscaledTime +
            mouseStepInterval;

        if (logMouseSteps)
        {
            Debug.Log(
                "[SystemCameraZoomInput2A] " +
                $"Fixed mouse zoom step: {zoomCommand}.",
                this);
        }
    }

    private void HandlePinch()
    {
        var touches =
            EnhancedTouch
                .activeTouches;

        if (touches.Count != 2)
            return;

        var firstTouch =
            touches[0];

        var secondTouch =
            touches[1];

        Vector2 firstCurrent =
            firstTouch.screenPosition;

        Vector2 secondCurrent =
            secondTouch.screenPosition;

        Vector2 firstPrevious =
            firstCurrent -
            firstTouch.delta;

        Vector2 secondPrevious =
            secondCurrent -
            secondTouch.delta;

        float currentDistance =
            Vector2.Distance(
                firstCurrent,
                secondCurrent);

        float previousDistance =
            Vector2.Distance(
                firstPrevious,
                secondPrevious);

        if (currentDistance <= 0.01f ||
            previousDistance <= 0.01f)
        {
            return;
        }

        float distanceDelta =
            currentDistance -
            previousDistance;

        if (Mathf.Abs(distanceDelta) <
            pinchDeadZonePixels)
        {
            return;
        }

        float screenReference =
            Mathf.Max(
                1f,
                Mathf.Min(
                    Screen.width,
                    Screen.height));

        float normalizedInput =
            distanceDelta /
            screenReference *
            pinchInputMultiplier;

        cameraController.AdjustZoom(
            normalizedInput);
    }

    private bool IsInputBlocked()
    {
        if (blockingUiRoots == null)
            return false;

        foreach (
            GameObject root
            in blockingUiRoots)
        {
            if (root != null &&
                root.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
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

    private static bool IsFinite(
        float value)
    {
        return
            !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }
}