using UnityEngine;

/// <summary>
/// Инициализирует карту галактики и синхронизирует
/// её выбор с назначением на системной карте.
/// </summary>
public class GalaxySceneController : CustomMonoBehaviour
{
    [Header("Screen Controllers")]
    [SerializeField]
    private MetaHudController metaHudController;

    [SerializeField]
    private GalaxyMapSystemBuilder galaxyMapSystemBuilder;

    [SerializeField]
    private GalaxyMapSectorBuilder galaxyMapSectorBuilder;

    [SerializeField]
    private GalaxyMapRoutesBuilder2A
        galaxyMapRoutesBuilder2A;

    [SerializeField]
    private GalaxyMapBackgroundClickHandler
        galaxyMapBackgroundClickHandler;

    [SerializeField]
    private GalaxySystemInfoPanel2A
        galaxySystemInfoPanel2A;

    [Header("Screen Roots")]
    [SerializeField]
    private GameObject galaxyMapScreenRoot2;

    private SimpleEventBus eventBus;
    private bool isInitialized;
    private bool isSubscribed;

    private void Start()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void Initialize()
    {
        if (isInitialized)
            return;

        if (Bootstrapper.Instance == null)
        {
            Debug.LogError(
                "[GalaxySceneController] " +
                "Bootstrapper.Instance is null."
            );

            return;
        }

        if (Bootstrapper.Instance.ServiceRegistry == null)
        {
            Debug.LogError(
                "[GalaxySceneController] " +
                "ServiceRegistry is null."
            );

            return;
        }

        eventBus =
            Bootstrapper.Instance.ServiceRegistry
                .Get<SimpleEventBus>();

        /*
         * Сначала запускаем все элементы карты.
         */
        metaHudController?.Initialize();
        galaxyMapSectorBuilder?.Initialize();
        galaxyMapSystemBuilder?.Initialize();
        galaxyMapRoutesBuilder2A?.Initialize();
        galaxyMapBackgroundClickHandler?.Initialize();

        /*
         * Панель должна подписаться на
         * GalaxyMapSystemSelectedEvent раньше,
         * чем будет восстановлен выбор.
         */
        galaxySystemInfoPanel2A?.Initialize();

        SubscribeToEvents();

        isInitialized = true;

        /*
         * Покрывает первый запуск, если GalaxyEnteredEvent
         * был опубликован раньше Start этого компонента.
         */
        RefreshGalaxySelection();

        LogCustom(
            "All galaxy systems initialized."
        );
    }

    private void SubscribeToEvents()
    {
        if (isSubscribed)
            return;

        if (eventBus == null)
            return;

        eventBus.Subscribe<GalaxyEnteredEvent>(
            OnGalaxyEntered
        );

        isSubscribed = true;
    }

    private void UnsubscribeFromEvents()
    {
        if (!isSubscribed)
            return;

        if (eventBus == null)
            return;

        eventBus.Unsubscribe<GalaxyEnteredEvent>(
            OnGalaxyEntered
        );

        isSubscribed = false;
    }

    private void OnGalaxyEntered(
        GalaxyEnteredEvent eventData
    )
    {
        RefreshGalaxySelection();
    }

    private void RefreshGalaxySelection()
    {
        /*
         * Сначала очищаем старый визуальный выбор.
         * Это важно, если до этого была выбрана планета,
         * свободная точка или другое системное назначение.
         */
        galaxySystemInfoPanel2A?.Hide();
        galaxyMapSystemBuilder?.ClearSelectedSystem();
        galaxyMapSystemBuilder?.Refresh();

        bool restored =
            galaxyMapSystemBuilder != null &&
            galaxyMapSystemBuilder
                .RestoreSelectionFromSystemTravel();

        LogCustom(
            "[GalaxySceneController] " +
            "Galaxy destination restored = " +
            restored
        );
    }
}