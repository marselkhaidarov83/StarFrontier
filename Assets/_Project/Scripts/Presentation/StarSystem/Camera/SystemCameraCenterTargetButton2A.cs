using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Кнопка однократного центрирования камеры
/// на цели движения корабля.
/// </summary>
[DisallowMultipleComponent]
public sealed class SystemCameraCenterTargetButton2A :
    MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private Button button;

    [SerializeField]
    private SystemCameraController2A
        cameraController;

    private bool _isBound;

    private void Reset()
    {
        button =
            GetComponent<Button>();
    }

    private void Awake()
    {
        if (button == null)
        {
            button =
                GetComponent<Button>();
        }
    }

    private void OnEnable()
    {
        Bind();
        RefreshInteractable();
    }

    private void Update()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double refreshMs = 0.0;

        try
        {
            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            RefreshInteractable();

            refreshMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            string details =
                "Name=" + name +
                " | IsBound=" + _isBound +
                " | HasButton=" + (button != null) +
                " | HasCameraController=" + (cameraController != null) +
                " | RefreshMs=" + refreshMs.ToString("F3");

            VisualUpdateAggregateLog.Record(
                "SystemCameraCenterTargetButton2A.Update",
                elapsedMs,
                details);

            VisualUpdatePerfLog.LogIfSlow(
                "SystemCameraCenterTargetButton2A.Update",
                startedAt,
                details);
        }
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    public void SetCameraController(
        SystemCameraController2A controller)
    {
        cameraController =
            controller;

        RefreshInteractable();
    }

    private void Bind()
    {
        if (_isBound)
            return;

        if (button == null)
        {
            button =
                GetComponent<Button>();
        }

        if (button == null)
            return;

        button.onClick.AddListener(
            OnClicked);

        _isBound =
            true;
    }

    private void Unbind()
    {
        if (!_isBound)
            return;

        if (button != null)
        {
            button.onClick.RemoveListener(
                OnClicked);
        }

        _isBound =
            false;
    }

    private void OnClicked()
    {
        if (cameraController == null)
            return;

        cameraController
            .CenterOnMovementTarget();

        RefreshInteractable();
    }

    private void RefreshInteractable()
    {
        if (button == null)
            return;

        button.interactable =
            cameraController != null &&
            cameraController
                .CanCenterOnMovementTarget;
    }
}