using UnityEngine;

/// <summary>
/// Передаёт одноразовые команды из runtime state
/// компонентам представления.
/// </summary>
[DisallowMultipleComponent]
public sealed class SystemControlPresentationBridge2A :
    MonoBehaviour
{
    [SerializeField]
    private SystemCameraController2A cameraController;

    private ISystemGameplayStateService
        _stateService;

    private void LateUpdate()
    {
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double resolveServiceMs = 0.0;
        double returnToShipMs = 0.0;

        bool hasStateService = false;
        bool hasCameraController = false;
        bool recentered = false;

        try
        {
            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            hasStateService =
                TryResolveStateService();

            resolveServiceMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            if (!hasStateService)
                return;

            hasCameraController =
                cameraController != null;

            if (!hasCameraController)
                return;

            if (_stateService
                .Control
                .RecenterCameraPressedThisFrame)
            {
                phaseStartedAt =
                    Time.realtimeSinceStartupAsDouble;

                cameraController.ReturnToShip();

                returnToShipMs =
                    (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

                recentered = true;
            }
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            string details =
                "Name=" + name +
                " | HasStateService=" + hasStateService +
                " | HasCameraController=" + hasCameraController +
                " | Recentered=" + recentered +
                " | ResolveServiceMs=" + resolveServiceMs.ToString("F3") +
                " | ReturnToShipMs=" + returnToShipMs.ToString("F3");

            VisualUpdateAggregateLog.Record(
                "SystemControlPresentationBridge2A.LateUpdate",
                elapsedMs,
                details);

            VisualUpdatePerfLog.LogIfSlow(
                "SystemControlPresentationBridge2A.LateUpdate",
                startedAt,
                details);
        }
    }

    private bool TryResolveStateService()
    {
        if (_stateService != null)
            return true;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return false;
        }

        return Bootstrapper.Instance
            .ServiceRegistry
            .TryGet(out _stateService);
    }
}