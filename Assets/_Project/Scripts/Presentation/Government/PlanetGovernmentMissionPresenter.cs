using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlanetGovernmentMissionPresenter : CustomMonoBehaviour
{
    [SerializeField] private TMP_Text governmentPlanetTitleText;
    [SerializeField] private TMP_Text governmentOfferTitleText;
    [SerializeField] private TMP_Text governmentOfferDescriptionText;
    [SerializeField] private TMP_Text governmentOfferStateText;
    [SerializeField] private GovernmentMissionVisualCatalog visualCatalog;

    private IPlanetGovernmentMissionService _governmentMissionService;
    private IMissionService _missionService;
    private StarSystemConfig _systemConfig;
    private PlanetConfig _planetConfig;
    private IConfigService _configService;
    private IGameSessionService _session;
    private SimpleEventBus _eventBus;
    private bool _isSubscribed;
    private Image _heroImage;
    private Image _heroOverlayImage;
    private RectTransform _heroFrame;
    private MissionScreenController _missionScreen;
    private Sprite _currentSprite;
    public Sprite CurrentLocationIllustration => _currentSprite;
    private float _lastLayoutWidth = -1f;
    // Slightly underlap the HUD artwork so the illustration meets its lower trim.
    private const float TopHudHeight = 194f;

    public void Initialize()
    {
        IServiceRegistry services = Bootstrapper.Instance.ServiceRegistry;
        _eventBus = services.Get<SimpleEventBus>();
        _configService = services.Get<IConfigService>();
        _session = services.Get<IGameSessionService>();
        _governmentMissionService = services.Get<IPlanetGovernmentMissionService>();
        _missionService = services.Get<IMissionService>();
        // Planet action buttons are siblings in this Canvas and must remain above the illustration.
        transform.SetAsFirstSibling();
        CreateHeroImage();
        _missionScreen = GetComponent<MissionScreenController>();
        Transform oldOfferPanel = transform.Find("MissionPanel");
        if (oldOfferPanel != null) oldOfferPanel.gameObject.SetActive(false);
        SubscribeToEvents();
    }

    private void OnEnable() => SubscribeToEvents();

    private void OnDisable() => UnsubscribeFromEvents();

    private void OnDestroy() => UnsubscribeFromEvents();

    private void UnsubscribeFromEvents()
    {
        if (_eventBus == null || !_isSubscribed) return;
        _eventBus.Unsubscribe<GovernmentEnteredEvent>(OnGovernmentEntered);
        _eventBus.Unsubscribe<MissionAcceptedEvent>(OnMissionAccepted);
        _eventBus.Unsubscribe<MissionStateChangedEvent>(OnMissionChanged);
        _isSubscribed = false;
    }

    private void LateUpdate()
    {
        if (_heroFrame == null || _currentSprite == null) return;
        float width = ((RectTransform)transform).rect.width;
        if (Mathf.Abs(width - _lastLayoutWidth) > 0.5f)
            UpdateHeroLayout();
    }

    private void SubscribeToEvents()
    {
        if (_eventBus == null || _isSubscribed || !isActiveAndEnabled) return;
        _eventBus.Subscribe<GovernmentEnteredEvent>(OnGovernmentEntered);
        _eventBus.Subscribe<MissionAcceptedEvent>(OnMissionAccepted);
        _eventBus.Subscribe<MissionStateChangedEvent>(OnMissionChanged);
        _isSubscribed = true;
    }

    private void OnGovernmentEntered(GovernmentEnteredEvent evt)
    {
        if (this == null || !isActiveAndEnabled) return;
        _systemConfig = _configService.GetCurrentSystemConfig();
        _planetConfig = _configService.GetCurrentPlanetConfig();
        CheckOffer();
    }

    private void OnMissionAccepted(MissionAcceptedEvent evt) => RefreshCurrentPlanet();
    private void OnMissionChanged(MissionStateChangedEvent evt) => RefreshCurrentPlanet();

    private void RefreshCurrentPlanet()
    {
        if (this == null || !isActiveAndEnabled) return;
        _systemConfig = _configService.GetCurrentSystemConfig();
        _planetConfig = _configService.GetCurrentPlanetConfig();
        CheckOffer();
    }

    public void CheckOffer()
    {
        if (StationLocationRules.IsInside(_session?.State, _systemConfig))
        {
            if (governmentPlanetTitleText != null) governmentPlanetTitleText.text = _systemConfig.Station.DisplayName;
            if (governmentOfferTitleText != null) governmentOfferTitleText.text = string.Empty;
            if (governmentOfferDescriptionText != null) governmentOfferDescriptionText.text = string.Empty;
            if (governmentOfferStateText != null) governmentOfferStateText.text = string.Empty;
            RefreshStationIllustration(_systemConfig.Station);
            AppLog.Info(DebugLogChannel.StationInterface, typeof(PlanetGovernmentMissionPresenter),
                "Station government | Owned missions only | Station=" + _systemConfig.Station.Id);
            return;
        }
        if (_planetConfig == null || _systemConfig == null) return;
        if (_heroFrame != null) _heroFrame.gameObject.SetActive(true);
        PlanetOfferedMissionData offer =
            _governmentMissionService.GetOrCreateGovernmentOffer(_systemConfig, _planetConfig);
        RefreshUi(offer);
    }

    private void RefreshStationIllustration(StationConfig station)
    {
        Sprite sprite = station?.StationTypeConfig?.MissionIllustration;
        _currentSprite = sprite;
        if (_heroImage != null)
        {
            _heroImage.sprite = sprite;
            _heroImage.enabled = sprite != null;
        }
        // A station has one fixed illustration; never retain a planet mission overlay.
        if (_heroOverlayImage != null)
        {
            _heroOverlayImage.sprite = null;
            _heroOverlayImage.enabled = false;
        }
        if (_heroFrame != null) _heroFrame.gameObject.SetActive(sprite != null);
        if (sprite != null) UpdateHeroLayout();
        else _missionScreen?.SetListTopInset(TopHudHeight);
        AppLog.Info(DebugLogChannel.StationInterface, typeof(PlanetGovernmentMissionPresenter),
            "Station mission illustration | Station=" + station?.Id + " | Sprite=" + sprite?.name);
    }

    public void AcceptOffer()
    {
        if (_planetConfig == null) return;
        bool accepted = _governmentMissionService.AcceptGovernmentOffer(_planetConfig.Id);
        AppLog.Info(DebugLogChannel.Mission, typeof(PlanetGovernmentMissionPresenter),
            "Offer " + (accepted ? "accepted" : "rejected") + " | Planet=" + _planetConfig.Id);
        if (accepted) CheckOffer();
    }

    private void RefreshUi(PlanetOfferedMissionData offer)
    {
        if (governmentPlanetTitleText != null)
            governmentPlanetTitleText.text = _planetConfig?.DisplayName ?? string.Empty;

        MissionInstanceData featured = offer?.OfferedMission;
        if (featured == null && _missionService != null && _planetConfig != null)
        {
            foreach (MissionInstanceData mission in _missionService.GetActiveMissions())
            {
                if (mission != null && mission.SourcePlanetId == _planetConfig.Id &&
                    MissionLifecycleRules.IsActive(mission.Status))
                {
                    // Show the most recently added mission from this planet.
                    featured = mission;
                }
            }
        }

        // Automatic defense and liberation rewards can be claimed on any
        // inhabited planet of their system, even though no planet issued them.
        if (featured == null && _missionService != null && _planetConfig?.IsInhabited == true)
        {
            foreach (MissionInstanceData mission in _missionService.GetActiveMissions())
            {
                if (mission != null && MissionBattleRules.IsBattle(mission.MissionType) &&
                    mission.Status == MissionStatus.ReadyToComplete &&
                    mission.TargetSystemId == _systemConfig?.Id)
                {
                    featured = mission;
                    break;
                }
            }
        }

        if (_heroImage != null)
        {
            Sprite sprite = visualCatalog != null
                ? visualCatalog.Get(_planetConfig.PlanetType, featured)
                : null;
            _heroImage.sprite = sprite;
            _heroImage.enabled = sprite != null;
            _currentSprite = sprite;
            UpdateHeroLayout();
        }

        if (_heroOverlayImage != null)
        {
            Sprite overlay = visualCatalog != null
                ? visualCatalog.GetOverlay(_planetConfig.PlanetType, featured)
                : null;
            _heroOverlayImage.sprite = overlay;
            _heroOverlayImage.enabled = overlay != null;
        }

        if (governmentOfferTitleText != null)
            governmentOfferTitleText.text = offer?.OfferedMission?.Title ?? string.Empty;
        if (governmentOfferDescriptionText != null)
            governmentOfferDescriptionText.text = offer?.OfferedMission == null
                ? string.Empty
                : MissionUiText.Description(offer.OfferedMission, _configService);
        if (governmentOfferStateText != null)
            governmentOfferStateText.text = string.Empty;

        AppLog.Info(DebugLogChannel.Mission, typeof(PlanetGovernmentMissionPresenter),
            "Government visual | Planet=" + _planetConfig?.Id +
            " | Mission=" + (featured?.MissionRuntimeId ?? "neutral"));
    }

    private void CreateHeroImage()
    {
        Transform existing = transform.Find("GovernmentMissionHero");
        if (existing != null)
        {
            _heroFrame = existing as RectTransform;
            Transform existingPicture = existing.Find("MissionIllustration");
            _heroImage = existingPicture != null
                ? existingPicture.GetComponent<Image>()
                : existing.GetComponentInChildren<Image>(true);
            CreateHeroOverlay();
            return;
        }

        GameObject frame = new GameObject("GovernmentMissionHero",
            typeof(RectTransform), typeof(RectMask2D));
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        _heroFrame = frameRect;
        frameRect.SetParent(transform, false);
        frameRect.anchorMin = new Vector2(0f, 1f);
        frameRect.anchorMax = Vector2.one;
        frameRect.pivot = new Vector2(0.5f, 1f);
        frameRect.anchoredPosition = new Vector2(0f, -TopHudHeight);
        frameRect.sizeDelta = Vector2.zero;
        frameRect.SetAsFirstSibling();

        GameObject picture = new GameObject("MissionIllustration",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform imageRect = picture.GetComponent<RectTransform>();
        imageRect.SetParent(frameRect, false);
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.anchoredPosition = Vector2.zero;
        imageRect.sizeDelta = Vector2.zero;
        _heroImage = picture.GetComponent<Image>();
        _heroImage.raycastTarget = false;
        _heroImage.preserveAspect = true;
        CreateHeroOverlay();
    }

    private void CreateHeroOverlay()
    {
        if (_heroFrame == null) return;
        Transform existing = _heroFrame.Find("MissionTabletopOverlay");
        if (existing != null)
        {
            _heroOverlayImage = existing.GetComponent<Image>();
            return;
        }

        GameObject overlay = new GameObject("MissionTabletopOverlay",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.SetParent(_heroFrame, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.anchoredPosition = Vector2.zero;
        overlayRect.sizeDelta = Vector2.zero;
        _heroOverlayImage = overlay.GetComponent<Image>();
        _heroOverlayImage.raycastTarget = false;
        _heroOverlayImage.preserveAspect = true;
        _heroOverlayImage.enabled = false;
    }

    private void UpdateHeroLayout()
    {
        if (_heroFrame == null || _currentSprite == null) return;
        float width = ((RectTransform)transform).rect.width;
        if (width <= 0f) return;
        _lastLayoutWidth = width;
        float aspect = _currentSprite.rect.width / _currentSprite.rect.height;
        float height = width / aspect;
        _heroFrame.sizeDelta = new Vector2(0f, height);
        if (_missionScreen != null)
            _missionScreen.SetListTopInset(TopHudHeight + height);
    }
}
