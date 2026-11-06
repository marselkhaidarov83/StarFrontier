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
    private IStationVisitService _stationVisits;
    private Color _planetBackgroundColor = Color.white;
    // private ITravelService _travelService;
    // private GalaxyGraphModel _galaxyGraph;

    // private bool _isInitialized;
    // private bool _isSubscribed;

    private GameObject _researchBlock;
    private StationResearchPanelController _researchPanel;
    private Button _researchButton;
    private void CreateResearchPanel()
    {
        if (_researchPanel != null || planetRoot == null) return;
        var services = Bootstrapper.Instance.ServiceRegistry;
        _researchBlock = new GameObject("StationResearchBlock", typeof(RectTransform));
        _researchBlock.layer = planetRoot.layer; _researchBlock.SetActive(false);
        StationResearchPanelView2A.AttachToLocation(_researchBlock.GetComponent<RectTransform>(), planetRoot.transform,
            governmentBlock != null ? governmentBlock.transform : null, backgroundImage != null ? backgroundImage.transform : null);
        _researchPanel = _researchBlock.AddComponent<StationResearchPanelController>();
        var canvas = planetRoot.GetComponentInParent<Canvas>(true);
        var template = governmentBlock != null ? governmentBlock.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
        if (template == null && canvas != null) template = canvas.GetComponentInChildren<TMPro.TMP_Text>(true);
        _researchPanel.Initialize(template, services.Get<IResearchService>(), gameSessionService,
            services.Get<IStationCardService>(), services.Get<IStationInfrastructureService>(), configService,
            _gameTimeService, eventBus);
        var rect = ResearchPanelUi2A.Rect(planetRoot.transform, "OpenResearchButton");
        var background = rect.gameObject.AddComponent<Image>();
        background.color = Color.clear;
        var icon = ResearchPanelUi2A.Rect(rect, "Atom").gameObject.AddComponent<ResearchButtonIcon2A>();
        ResearchPanelUi2A.Stretch(icon.rectTransform); icon.color = MissionPanelStyle2A.ActionAccent; icon.raycastTarget = false; icon.FilledBackground = true;
        _researchButton = rect.gameObject.AddComponent<Button>(); _researchButton.targetGraphic = icon;
        _researchButton.onClick.AddListener(() => OpenStationResearch()); _researchButton.gameObject.SetActive(false);
        ResearchPanelUi2A.Place(rect, 16, 210, 104, 104);
    }
    public bool OpenStationResearch()
    {
        if (_researchBlock != null)
            StationResearchPanelView2A.AttachToLocation(_researchBlock.GetComponent<RectTransform>(), planetRoot.transform,
                governmentBlock != null ? governmentBlock.transform : null, backgroundImage != null ? backgroundImage.transform : null);
        if (_researchPanel == null || !_researchPanel.TryOpen()) return false;
        if (governmentBlock != null) governmentBlock.SetActive(false);
        if (marketBlock != null) marketBlock.SetActive(false);
        if (hangarBlock != null) hangarBlock.SetActive(false);
        if (fuelBlock != null) fuelBlock.SetActive(false);
        return true;
    }
    private void HideResearch() { if (_researchBlock != null) _researchBlock.SetActive(false); }
    private void RefreshResearchAvailability()
    {
        if (_researchButton == null) return;
        var player = gameSessionService.State?.Player;
        var system = player != null ? configService.GetStarSystemConfigById(player.CurrentSystemId) : null;
        _researchButton.gameObject.SetActive(system?.Station != null &&
            StationLocationRules.IsInside(gameSessionService.State, system));
        _researchButton.transform.SetAsLastSibling();
    }
    private void LateUpdate()
    {
        if (_researchButton == null || !_researchButton.gameObject.activeInHierarchy || openSystemButton == null || planetRoot == null) return;
        var root = planetRoot.transform as RectTransform;
        var source = openSystemButton.transform as RectTransform;
        if (root == null || source == null) return;
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, source);
        // Mirror only the research entry. The serialized takeoff button stays in its original right-hand position.
        float margin = Mathf.Max(10, root.rect.xMax - bounds.max.x);
        float top = Mathf.Max(0, root.rect.yMax - bounds.max.y);
        ResearchPanelUi2A.Place(_researchButton.GetComponent<RectTransform>(), margin, top,
            Mathf.Clamp(bounds.size.x, 56, 160), Mathf.Clamp(bounds.size.y, 56, 160));
    }

    public void Initialize()
    {
        eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _systemTravelService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemTravelService>();
        _orbitalMotionService = Bootstrapper.Instance.ServiceRegistry.Get<IOrbitalMotionService>();
        _gameTimeService = Bootstrapper.Instance.ServiceRegistry.Get<IGameTimeService>();

        _stationVisits = Bootstrapper.Instance.ServiceRegistry.Get<IStationVisitService>();
        if (backgroundImage != null) _planetBackgroundColor = backgroundImage.color;
        CreateResearchPanel();
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
        if (!string.IsNullOrEmpty(gameSessionService.State.Player.CurrentStationId))
        {
            bool success = _stationVisits.TryTakeOff(out string reason);
            AppLog.Info(DebugLogChannel.StationInterface, typeof(PlanetController),
                "Station takeoff | Success=" + success + " | Reason=" + reason);
            return;
        }
        if (string.IsNullOrWhiteSpace(gameSessionService.State.Player.CurrentPlanetId))
        {
            PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.FirstLaunch, false, "CurrentPlanetMissing");
            return;
        }

        Vector2 planetPosition = _orbitalMotionService.GetPlanetCurrentPosition(
                _configService.GetCurrentPlanetConfig().PlanetOrbit);
        _systemTravelService.CancelTravel();
        _systemTravelService.SetCurrentPosition(planetPosition);
        gameSessionService.State.Player.SystemMapShipPosition =
            _systemTravelService.State.GetCurrentPosition();
        gameSessionService.State.Player.CurrentPlanetId = "";

        eventBus.Publish(new StarSystemEnteredEvent(gameSessionService.State.Player.CurrentSystemId));

        if (_systemTravelService.StartPlanetTakeoff())
        {
            PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.FirstLaunch, true);
            // In Play mode time already advances. In Pause mode run exactly one step.
            if (_gameTimeService.IsPaused)
                _gameTimeService.StepOneDay();
        }
        else
        {
            PlayerAnalyticsSafe.Track(PlayerAnalyticsAction.FirstLaunch, false, "TakeoffRouteUnavailable");
            AppLog.Warning(DebugLogChannel.Travel, typeof(PlanetController),
                "Could not build the planet takeoff route. Check ShipMovementConfig and the destination.");
        }
    }

    public void TakeOffFromPlanet() => OnOpenSystemClicked();

    private void OnOpenHangarClicked()
    {
        HideResearch();
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
            AppLog.Info(DebugLogChannel.PlanetUi,
                typeof(PlanetController),
                "Open Hangar clicked");
    }

    private void OnOpenMarketClicked()
    {
        HideResearch();
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
            AppLog.Info(DebugLogChannel.PlanetUi,
                typeof(PlanetController),
                "Open Market clicked");
    }

    private void OnOpenGovernmentClicked()
    {
        PlanetGovernmentEntered();

        if (IsDebug())
            AppLog.Info(DebugLogChannel.PlanetUi,
                typeof(PlanetController),
                "Open Government clicked");
    }

    private void OnRefuelClicked()
    {
        HideResearch();
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
            AppLog.Info(DebugLogChannel.PlanetUi,
                typeof(PlanetController),
                "Refuel clicked");
    }

    private void OnPlanetEntered(PlanetEnteredEvent evt)
    {
        LogPlanetPanelOpening(evt.PlanetId);
        if (IsDebug())
            AppLog.Info(DebugLogChannel.PlanetUi,
                typeof(PlanetController),
                "planet entered started");

        RefreshBackground();
        RefreshActionAvailability();
        RefreshResearchAvailability();
        PlanetGovernmentEntered();

        if (IsDebug())
            AppLog.Info(DebugLogChannel.PlanetUi,
                typeof(PlanetController),
                "planet entered");
    }

    private void LogPlanetPanelOpening(string planetId)
    {
        if (!Bootstrapper.Instance.IsDebugLogEnabled(DebugLogChannel.PlayerMovement))
            return;

        PlanetConfig planet = _configService.GetPlanetConfigById(planetId);
        bool hasPlanetModel = planet != null && planet.PlanetOrbit != null;
        Vector3 planetModel = hasPlanetModel
            ? _orbitalMotionService.GetPlanetCurrentPosition(planet.PlanetOrbit) : Vector3.zero;
        PlayerSystemMapShipView shipView = FindFirstObjectByType<PlayerSystemMapShipView>(FindObjectsInactive.Include);
        PlanetOrbitMotion2 planetView = null;
        foreach (PlanetOrbitMotion2 candidate in FindObjectsByType<PlanetOrbitMotion2>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.PlanetId == planetId)
            {
                planetView = candidate;
                break;
            }
        }

        Vector3 shipModel = _systemTravelService.State.GetCurrentPosition();
        Vector3 shipVisual = shipView != null ? shipView.transform.position : Vector3.zero;
        Vector3 planetVisual = planetView != null ? planetView.transform.position : Vector3.zero;
        AppLog.Info(DebugLogChannel.PlayerMovement,
            "[PLANET_ARRIVAL] Phase=PlanetPanel.BeforeOpen | Planet=" + planetId +
            " | Frame=" + Time.frameCount + " | Tick=" + _gameTimeService.CurrentQuantTick +
            " | TravelStatus=" + _systemTravelService.State.Status +
            " | ShipModel=" + shipModel.ToString("F3") +
            " | PlayerSavedPosition=" + gameSessionService.State.Player.SystemMapShipPosition.ToString("F3") +
            " | HasPlanetModel=" + hasPlanetModel + " | PlanetModel=" + planetModel.ToString("F3") +
            " | HasShipVisual=" + (shipView != null) + " | ShipVisual=" + shipVisual.ToString("F3") +
            " | HasPlanetVisual=" + (planetView != null) + " | PlanetVisual=" + planetVisual.ToString("F3") +
            " | ModelDistance=" + (hasPlanetModel ? Vector2.Distance(shipModel, planetModel) : -1f).ToString("0.###") +
            " | VisualDistance=" + (shipView != null && planetView != null
                ? Vector2.Distance(shipVisual, planetVisual) : -1f).ToString("0.###") +
            " | PlanetVisualModelOffset=" + (hasPlanetModel && planetView != null
                ? Vector2.Distance(planetModel, planetVisual) : -1f).ToString("0.###"));
    }

    private void RefreshBackground()
    {
        string currentPlanetId = gameSessionService.State.Player.CurrentPlanetId;
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

        backgroundImage.color = _planetBackgroundColor;
        backgroundImage.sprite = targetBackground;
        backgroundImage.preserveAspect = false;
    }

    private void PlanetGovernmentEntered()
    {
        HideResearch();
        if (IsDebug())
            AppLog.Info(DebugLogChannel.PlanetUi,
                typeof(PlanetController),
                "PlanetGovernmentEntered() started");

        if (planetRoot != null)
        {
            if (IsDebug())
                AppLog.Info(DebugLogChannel.PlanetUi,
                    typeof(PlanetController),
                    "planetRoot = " + planetRoot);
            planetRoot.SetActive(true);
        }
        if (governmentBlock != null)
        {
            if (IsDebug())
                AppLog.Info(DebugLogChannel.PlanetUi,
                    typeof(PlanetController),
                    "governmentBlock = " + governmentBlock);
            governmentBlock.SetActive(true);
        }
        if (marketBlock != null)
        {
            if (IsDebug())
                AppLog.Info(DebugLogChannel.PlanetUi,
                    typeof(PlanetController),
                    "marketBlock = " + marketBlock);
            marketBlock.SetActive(false);
        }
        if (hangarBlock != null)
        {
            if (IsDebug())
                AppLog.Info(DebugLogChannel.PlanetUi,
                    typeof(PlanetController),
                    "hangarBlock = " + hangarBlock);
            hangarBlock.SetActive(false);
        }
        if (fuelBlock != null)
        {
            if (IsDebug())
                AppLog.Info(DebugLogChannel.PlanetUi,
                    typeof(PlanetController),
                    "fuelBlock = " + fuelBlock);
            fuelBlock.SetActive(false);
        }

        // eventBus.Publish(new MissionEnteredEvent());
        eventBus.Publish(new GovernmentEnteredEvent());

        if (IsDebug())
            LogCustom("PlanetGovernmentEntered() ended");
    }

    private void OnStationVisitChanged(StationVisitChangedEvent evt)
    {
        if (!evt.Inside)
        {
            if (planetRoot != null && string.IsNullOrEmpty(gameSessionService.State?.Player?.CurrentPlanetId))
                planetRoot.SetActive(false);
            return;
        }
        // Reuse the real planet services/menu in a separate station landing window.
        if (backgroundImage != null)
        {
            backgroundImage.sprite = null;
            backgroundImage.color = new Color(0.025f, 0.045f, 0.09f, 1f);
            backgroundImage.preserveAspect = false;
        }
        SetButtonActiveIfAlive(openMarketButton, true);
        SetButtonActiveIfAlive(openGovernmentButton, true);
        SetButtonActiveIfAlive(openHangarButton, true);
        SetButtonActiveIfAlive(refuelButton, true);
        PlanetGovernmentEntered();
        RefreshResearchAvailability();
        AppLog.Info(DebugLogChannel.StationInterface, typeof(PlanetController),
            "Station interior opened | Station=" + evt.StationId);
    }

    private void OnSystemEntered(StarSystemEnteredEvent evt)
    {
        HideResearch();
        if (planetRoot != null)
            planetRoot.SetActive(false);

        if (IsDebug())
            LogCustom("system entered");
    }

    private void SubscribeToEvents()
    {
        if (eventBus == null)
            return;

        eventBus.Subscribe<StationVisitChangedEvent>(OnStationVisitChanged);
        eventBus.Subscribe<PlanetEnteredEvent>(OnPlanetEntered);
        eventBus.Subscribe<StarSystemEnteredEvent>(OnSystemEntered);
    }

    private void UnsubscribeFromEvents()
    {
        if (eventBus == null)
            return;

        eventBus.Unsubscribe<StationVisitChangedEvent>(OnStationVisitChanged);
        eventBus.Unsubscribe<PlanetEnteredEvent>(OnPlanetEntered);
        eventBus.Unsubscribe<StarSystemEnteredEvent>(OnSystemEntered);
    }

    private void RefreshActionAvailability()
    {
        if (gameSessionService == null || configService == null)
            return;

        string planetId = gameSessionService.State.Player.CurrentPlanetId;

        if (string.IsNullOrWhiteSpace(planetId))
            return;

        PlanetConfig currentPlanet = configService.GetPlanetConfigById(planetId);

        if (currentPlanet == null)
            return;

        bool isInhabited = currentPlanet.IsInhabited;

        SetButtonActiveIfAlive(openMarketButton, isInhabited);
        SetButtonActiveIfAlive(openGovernmentButton, isInhabited);
        SetButtonActiveIfAlive(openHangarButton, isInhabited);
        SetButtonActiveIfAlive(refuelButton, isInhabited);
    }

    private void SetButtonActiveIfAlive(Button button, bool isActive)
    {
        if (button == null)
            return;

        if (!button)
            return;

        GameObject buttonObject = button.gameObject;

        if (buttonObject == null)
            return;

        buttonObject.SetActive(isActive);
    }

}
