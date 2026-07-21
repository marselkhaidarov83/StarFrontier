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
        if (!TryResolveStateService())
            return;

        if (cameraController == null)
            return;

        if (_stateService
            .Control
            .RecenterCameraPressedThisFrame)
        {
            cameraController.ReturnToShip();
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