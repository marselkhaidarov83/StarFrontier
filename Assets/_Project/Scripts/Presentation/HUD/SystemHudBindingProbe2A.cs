using System;
using UnityEngine;

/// <summary>
/// Временная диагностика T02.
///
/// Проверяет:
/// - Registry получен;
/// - обязательные сервисы доступны;
/// - Bind вызывается один раз;
/// - Unbind и cleanup вызываются при отключении.
///
/// Компонент не отображляет игровой интерфейс
/// и не хранит gameplay State.
/// </summary>
public sealed class SystemHudBindingProbe2A :
    SystemHudWidgetBinderBase2A
{
    [Header("Diagnostics")]

    [SerializeField]
    private bool logLifecycle = true;

    [SerializeField]
    private int bindCount;

    [SerializeField]
    private int unbindCount;

    [SerializeField]
    private int cleanupCallbackCount;

    [SerializeField]
    private int registeredServiceCount;

    [SerializeField]
    private bool eventBusResolved;

    [SerializeField]
    private bool configServiceResolved;

    [SerializeField]
    private bool gameplayStateResolved;

    [SerializeField]
    private bool playerControlResolved;

    protected override void OnBind()
    {
        bindCount++;

        registeredServiceCount =
            Context.RegisteredServiceCount;

        eventBusResolved =
            Context.TryGet<SimpleEventBus>(
                out SimpleEventBus eventBus) &&
            eventBus != null;

        configServiceResolved =
            Context.TryGet<IConfigService>(
                out IConfigService configService) &&
            configService != null &&
            configService.SystemHudConfig != null;

        gameplayStateResolved =
            Context.TryGet<
                ISystemGameplayStateService>(
                out ISystemGameplayStateService
                    gameplayStateService) &&
            gameplayStateService != null;

        playerControlResolved =
            Context.TryGet<IPlayerControlService>(
                out IPlayerControlService
                    playerControlService) &&
            playerControlService != null;

        if (!eventBusResolved ||
            !configServiceResolved ||
            !gameplayStateResolved ||
            !playerControlResolved)
        {
            throw new InvalidOperationException(
                "HUD binding probe could not resolve " +
                "one or more required services.");
        }

        /*
         * Диагностический cleanup callback.
         * Он имитирует будущий Unsubscribe и позволяет
         * проверить, что SubscriptionBag очищается.
         */
        TrackSubscription(
            () => cleanupCallbackCount++);

        if (logLifecycle)
        {
            Debug.Log(
                "[2A-S03-03-T02] " +
                "HUD binding probe bound. " +
                $"Registry services: " +
                $"{registeredServiceCount}.",
                this);
        }
    }

    protected override void OnUnbind()
    {
        unbindCount++;

        if (logLifecycle)
        {
            Debug.Log(
                "[2A-S03-03-T02] " +
                "HUD binding probe unbound.",
                this);
        }
    }
}