using UnityEngine;
using UnityEngine.UI;

public class PlanetController : CustomMonoBehaviour
{
    [Header("Screen Roots")]
    [SerializeField] private GameObject planetRoot;
    [SerializeField] private GameObject governmentBlock;
    [SerializeField] private GameObject marketBlock;
    [SerializeField] private GameObject hangarBlock;
    [SerializeField] private GameObject fuelBlock;

    [Header("Background")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite hubBackground;
    [SerializeField] private Sprite frontierBackground;
    [SerializeField] private Sprite miningBackground;
    [SerializeField] private Sprite industrialBackground;
    [SerializeField] private Sprite scienceBackground;
    [SerializeField] private Sprite deadBackground;
    [SerializeField] private Sprite fallbackBackground;
        // [Header("Texts")]

        // [SerializeField]
        // private TMP_Text systemNameText;

        // [SerializeField]
        // private TMP_Text economyText;

        // [SerializeField]
        // private TMP_Text dangerText;

        // [Header("Buttons")]

        [SerializeField] private Button openSystemButton;

        [SerializeField] private Button openHangarButton;

        [SerializeField] private Button openMarketButton;

        [SerializeField] private Button openGovernmentButton;
        [SerializeField] private Button refuelButton;

        // [Header("Optional Root References")]

        // [SerializeField]
        // private GameObject systemScreenRoot;

        // [SerializeField]
        // private GameObject galaxyMapScreenRoot;

        // [Header("Controllers")]
        // [SerializeField] private MarketScreenController marketScreenController;

        private SimpleEventBus eventBus;
        private IGameSessionService gameSessionService;
        // private IRefuelService refuelService;
        private IConfigService configService;
        private ISystemTravelService _systemTravelService;
        private IOrbitalMotionService _orbitalMotionService;
        private IGameTimeService _gameTimeService;
        private IConfigService _configService;
        // private ITravelService _travelService;
        // private GalaxyGraphModel _galaxyGraph;

        // private bool _isInitialized;
        // private bool _isSubscribed;

        public void Initialize()
        {
            eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
            _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
            gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
            configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
            _systemTravelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();
            _orbitalMotionService = Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();
            _gameTimeService = Bootstrapper.Instance.ServiceRegistry.Get<IGameTimeService>();

            BindButtons();
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            UnbindButtons();
        }

        private void BindButtons()
        {
            if (openSystemButton != null)
                openSystemButton.onClick.AddListener(OnOpenSystemClicked);

            if (openHangarButton != null)
                openHangarButton.onClick.AddListener(OnOpenHangarClicked);

            if (openMarketButton != null)
                openMarketButton.onClick.AddListener(OnOpenMarketClicked);

            if (openGovernmentButton != null)
                openGovernmentButton.onClick.AddListener(OnOpenGovernmentClicked);

            if (refuelButton != null)
                refuelButton.onClick.AddListener(OnRefuelClicked);
        }

        private void UnbindButtons()
        {
            if (openSystemButton != null)
                openSystemButton.onClick.RemoveAllListeners();

            if (openHangarButton != null)
                openHangarButton.onClick.RemoveAllListeners();

            if (openMarketButton != null)
                openMarketButton.onClick.RemoveAllListeners();

            if (openGovernmentButton != null)
                openGovernmentButton.onClick.RemoveAllListeners();                

            if (refuelButton != null)
                refuelButton.onClick.RemoveAllListeners();
        }

    private void OnOpenSystemClicked()
    {
        Vector2 planetPosition = _orbitalMotionService.GetPlanetCurrentPosition(
                _configService.GetCurrentPlanetConfig().PlanetOrbit);
        _systemTravelService.SetCurrentPosition(planetPosition);
        gameSessionService.CurrentSave.PlayerProfile.CurrentPlanetId = "";

        eventBus.Publish(new SystemEnteredEvent(gameSessionService.CurrentSave.PlayerProfile.CurrentSystemId));
    }

    private void OnOpenHangarClicked()
    {
        if (planetRoot != null)
            planetRoot.SetActive(true);
        if (governmentBlock != null)
            governmentBlock.SetActive(false);
        if (marketBlock != null)
            marketBlock.SetActive(false);
        if (hangarBlock != null)
            hangarBlock.SetActive(true);
        if (fuelBlock != null)
            fuelBlock.SetActive(false);

        eventBus.Publish(new HangarEnteredEvent());

        if (IsDebug())
            Debug.Log("[PlanetController] Open Hangar clicked");
    }

    private void OnOpenMarketClicked()
    {
        if (planetRoot != null)
            planetRoot.SetActive(true);
        if (governmentBlock != null)
            governmentBlock.SetActive(false);
        if (marketBlock != null)
            marketBlock.SetActive(true);
        if (hangarBlock != null)
            hangarBlock.SetActive(false);
        if (fuelBlock != null)
            fuelBlock.SetActive(false);

        eventBus.Publish(new MarketEnteredEvent());

        if (IsDebug())
            Debug.Log("[PlanetController] Open Market clicked");
    }

    private void OnOpenGovernmentClicked()
    {
        PlanetGovernmentEntered();

        if (IsDebug())
            Debug.Log("[PlanetController] Open Government clicked");
    }    

    private void OnRefuelClicked()
    {
        if (planetRoot != null)
            planetRoot.SetActive(true);
        if (governmentBlock != null)
            governmentBlock.SetActive(false);
        if (marketBlock != null)
            marketBlock.SetActive(false);
        if (hangarBlock != null)
            hangarBlock.SetActive(false);
        if (fuelBlock != null)
            fuelBlock.SetActive(true);

        eventBus.Publish(new RefuelEnteredEvent());

        if (IsDebug())
            Debug.Log("[PlanetController] Refuel clicked");
    }            

    private void OnPlanetEntered(PlanetEnteredEvent evt)
    {
        if (IsDebug())
            Debug.Log("[PlanetController] planet entered started");

        RefreshBackground();
        RefreshActionAvailability();
        PlanetGovernmentEntered();

        if (IsDebug())
            Debug.Log("[PlanetController] planet entered");
    }

    private void RefreshBackground()
    {
        string currentPlanetId = gameSessionService.CurrentSave.PlayerProfile.CurrentPlanetId;
        PlanetConfig planetConfig = configService.GetPlanetConfigById(currentPlanetId);

        if (backgroundImage == null || planetConfig == null)
            return;

        Sprite targetBackground = fallbackBackground;
        switch (planetConfig.PlanetType)
        {
            case PlanetType.Hub:
                targetBackground = hubBackground != null ? hubBackground : fallbackBackground;
                break;

            case PlanetType.Frontier:
                targetBackground = frontierBackground != null ? frontierBackground : fallbackBackground;
                break;

            case PlanetType.Industrial:
                targetBackground = industrialBackground != null ? industrialBackground : fallbackBackground;
                break;

            case PlanetType.Mining:
                targetBackground = miningBackground != null ? miningBackground : fallbackBackground;
                break;

            case PlanetType.Science:
                targetBackground = scienceBackground != null ? scienceBackground : fallbackBackground;
                break;

            default:
                targetBackground = deadBackground;
                break;
        }

        backgroundImage.sprite = targetBackground;
        backgroundImage.preserveAspect = false;
    }

    private void PlanetGovernmentEntered()
    {
        if (IsDebug())
            Debug.Log("[PlanetController] PlanetGovernmentEntered() started");

        if (planetRoot != null)
        {
            if (IsDebug())
                Debug.Log("[PlanetController] planetRoot = " + planetRoot);
            planetRoot.SetActive(true);
        }
        if (governmentBlock != null)
        {
            if (IsDebug())
                Debug.Log("[PlanetController] governmentBlock = " + governmentBlock);
            governmentBlock.SetActive(true);
        }
        if (marketBlock != null)
        {
            if (IsDebug())
                Debug.Log("[PlanetController] marketBlock = " + marketBlock);
            marketBlock.SetActive(false);
        }
        if (hangarBlock != null)
        {
            if (IsDebug())
                Debug.Log("[PlanetController] hangarBlock = " + hangarBlock);
            hangarBlock.SetActive(false);
        }
        if (fuelBlock != null)
        {
            if (IsDebug())
                Debug.Log("[PlanetController] fuelBlock = " + fuelBlock);
            fuelBlock.SetActive(false);
        }

        // eventBus.Publish(new MissionEnteredEvent());
        eventBus.Publish(new GovernmentEnteredEvent());

        if (IsDebug())
            LogCustom("PlanetGovernmentEntered() ended");
    }

    private void OnSystemEntered(SystemEnteredEvent evt)
    {
        if (planetRoot != null)
            planetRoot.SetActive(false);

        if (IsDebug())
            LogCustom("system entered");
    }

    private void SubscribeToEvents()
    {
        if (eventBus == null)
            return;

        eventBus.Subscribe<PlanetEnteredEvent>(OnPlanetEntered);
        eventBus.Subscribe<SystemEnteredEvent>(OnSystemEntered);
    }

    private void UnsubscribeFromEvents()
    {
        if (eventBus == null)
            return;

           eventBus.Unsubscribe<PlanetEnteredEvent>(OnPlanetEntered);
        eventBus.Unsubscribe<SystemEnteredEvent>(OnSystemEntered);
    }

    private void RefreshActionAvailability()
    {
        var planetId = gameSessionService.CurrentSave.PlayerProfile.CurrentPlanetId;
        PlanetConfig currentPlanet = configService.GetPlanetConfigById(planetId);

        if (currentPlanet == null)
            return;

        bool isInhabited = currentPlanet.IsInhabited;

        openMarketButton.gameObject.SetActive(isInhabited);
        openGovernmentButton.gameObject.SetActive(isInhabited);
        openHangarButton.gameObject.SetActive(isInhabited);
        refuelButton.gameObject.SetActive(isInhabited);
    }
    
}