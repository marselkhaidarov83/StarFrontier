using UnityEngine;
using UnityEngine.UI;

public class SystemMapHUDController : CustomMonoBehaviour
{
    [Header("Screen Roots")]
    [SerializeField] private GameObject systemMapHUDRoot;    

    [Header("Objects")]
    [SerializeField] private Button openGalaxyButton;
    [SerializeField] private Button openSystemObjectsButton;

    private SimpleEventBus eventBus;
    private IGameStateMachine gameStateMachine;
    private IConfigService configService;
    private IGameSessionService gameSessionService;

    public void Initialize()
    {
        eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        gameStateMachine = Bootstrapper.Instance.ServiceRegistry.Get<IGameStateMachine>();
        configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        SubscribeToEvents();
        BindButtons();
        RefreshSunButtonIcon();

        if (IsDebug())
            AppLog.Info(DebugLogChannel.SystemHud,
                typeof(SystemMapHUDController),
                "initialized");
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        UnbindButtons();
    }

    private void SubscribeToEvents()
    {
        if (eventBus == null)
            return;

        eventBus.Subscribe<GalaxyEnteredEvent>(OnGalaxyEntered);
        eventBus.Subscribe<StarSystemEnteredEvent>(OnSystemEntered);
        eventBus.Subscribe<PlanetEnteredEvent>(OnPlanetEntered);
        eventBus.Subscribe<ExitMapChangedEvent>(OnExitMapChanged);

        if (IsDebug())
            AppLog.Info(DebugLogChannel.SystemHud,
                typeof(SystemMapHUDController),
                "events subscribed");
    }

    private void UnsubscribeFromEvents()
    {
        if (eventBus == null)
            return;

        eventBus.Unsubscribe<GalaxyEnteredEvent>(OnGalaxyEntered);
        eventBus.Unsubscribe<StarSystemEnteredEvent>(OnSystemEntered);
        eventBus.Unsubscribe<PlanetEnteredEvent>(OnPlanetEntered);
        eventBus.Unsubscribe<ExitMapChangedEvent>(OnExitMapChanged);

        if (IsDebug())
            AppLog.Info(DebugLogChannel.SystemHud,
                typeof(SystemMapHUDController),
                "events unsubscribed");
    }

    private void OnGalaxyEntered(GalaxyEnteredEvent evt)
    {
        if (systemMapHUDRoot != null)
            systemMapHUDRoot.SetActive(false);

        if (IsDebug())
            AppLog.Info(DebugLogChannel.SystemHud,
                typeof(SystemMapHUDController),
                "galaxy entered");
    }

    private void OnSystemEntered(StarSystemEnteredEvent evt)
    {
        RefreshSunButtonIcon(evt.SystemId);
        if (systemMapHUDRoot != null)
            systemMapHUDRoot.SetActive(true);

        if (IsDebug())
            AppLog.Info(DebugLogChannel.SystemHud,
                typeof(SystemMapHUDController),
                "system entered");
    }

    private void OnExitMapChanged(ExitMapChangedEvent evt)
    {
        RefreshSunButtonIcon();
        if (systemMapHUDRoot != null)
            systemMapHUDRoot.SetActive(true);
    }


    private void OnPlanetEntered(PlanetEnteredEvent evt)
    {
        if (systemMapHUDRoot != null)
            systemMapHUDRoot.SetActive(false);

        if (IsDebug())
            AppLog.Info(DebugLogChannel.SystemHud,
                typeof(SystemMapHUDController),
                "planet entered");
    }

    private void BindButtons()
    {
        if (openGalaxyButton != null)
            openGalaxyButton.onClick.AddListener(OnOpenGalaxyClicked);

        if (openSystemObjectsButton != null)
        {
            openSystemObjectsButton.onClick.RemoveListener(OnOpenSystemObjectsClicked);
            openSystemObjectsButton.onClick.AddListener(OnOpenSystemObjectsClicked);
        }
    }

    private void UnbindButtons()
    {
        if (openGalaxyButton != null)
            openGalaxyButton.onClick.RemoveListener(OnOpenGalaxyClicked);

        if (openSystemObjectsButton != null)
            openSystemObjectsButton.onClick.RemoveListener(OnOpenSystemObjectsClicked);
    }    

    private void RefreshSunButtonIcon(string systemId = null)
    {
        if (openSystemObjectsButton == null || configService == null)
            return;

        if (string.IsNullOrWhiteSpace(systemId))
            systemId = gameSessionService?.State?.Player?.CurrentSystemId;

        StarSystemConfig system = configService.GetStarSystemConfigById(systemId);
        Image image = openSystemObjectsButton.targetGraphic as Image;
        if (image == null)
            image = openSystemObjectsButton.GetComponent<Image>();
        if (image == null)
            return;

        image.sprite = system?.Sun?.SunSprite;
        image.preserveAspect = true;
        image.enabled = image.sprite != null;
    }

    private void OnOpenSystemObjectsClicked()
    {
        eventBus?.Publish(new SystemObjectsPanelRequestedEvent2A());
    }

    private void OnOpenGalaxyClicked()
    {
        gameStateMachine.Enter(new GalaxyState());
        // if (eventBus != null)
            // eventBus?.Publish(new GalaxyEnteredEvent());
    }
}