using System;
using UnityEngine;

/// <summary>
/// Создаёт runtime-границы системы из ShipMovementConfig.
///
/// Позже источник границ можно заменить на StarSystemConfig,
/// но в S3-13 используем уже существующий ShipMovementConfig.
/// </summary>
public sealed class SystemBoundsService2A :
    ISystemBoundsService
{
    private readonly ShipMovementConfig _config;
    private readonly ISystemGameplayStateService _stateService;

    public SystemBoundsService2A()
    {
        IConfigService configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _config = configService.ShipMovementConfig;
        _stateService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemGameplayStateService>();
    }

    public SystemBoundsService2A(
        ShipMovementConfig shipMovementConfig,
        ISystemGameplayStateService systemGameplayStateService)
    {
        if (shipMovementConfig == null)
        {
            throw new ArgumentNullException(
                nameof(shipMovementConfig));
        }

        _config = shipMovementConfig;

        _stateService =
            systemGameplayStateService
            ?? throw new ArgumentNullException(
                nameof(systemGameplayStateService));
    }

    public SystemBoundsRuntimeState State =>
        _stateService.Bounds;

    public void InitializeFromConfig()
    {
        SetBounds(
            Vector2.zero,
            _config.SystemBoundsHalfSize,
            _config.ClampToSystemBounds);
    }

    public void SetBounds(
        Vector2 center,
        Vector2 halfSize,
        bool isEnabled)
    {
        State.SetBounds(
            center,
            halfSize,
            isEnabled);
    }

    public Vector2 ClampPosition(Vector2 position)
    {
        return State.ClampPosition(position);
    }

    public bool Contains(Vector2 position)
    {
        return State.Contains(position);
    }

    public void ResetAll()
    {
        State.ResetAll();
    }
}