using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionScreenController : CustomMonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Transform missionListContent;
    [SerializeField] private MissionCardView missionCardPrefab;

    [Header("Icons")]
    [SerializeField] private Sprite deliveryIcon;
    [SerializeField] private Sprite eliminationIcon;
    [SerializeField] private Sprite reconIcon;

    private IMissionService _missionService;
    private IPlanetGovernmentMissionService _governmentMissionService;
    private IConfigService _configService;
    private IGameSessionService _session;
    private SimpleEventBus _eventBus;
    private readonly List<MissionCardView> _spawnedCards = new();
    private TMP_Text _emptyText;
    private MissionInstanceData _selectedMission;
    private ScrollRect _missionScroll;
    private float _listTopInset = 200f;
    // The bottom HUD draws above the board; a small overlap removes the seam.
    private const float BottomHudHeight = 142f;

    public void Initialize()
    {
        IServiceRegistry services = Bootstrapper.Instance.ServiceRegistry;
        _eventBus = services.Get<SimpleEventBus>();
        _missionService = services.Get<IMissionService>();
        _governmentMissionService = services.Get<IPlanetGovernmentMissionService>();
        _configService = services.Get<IConfigService>();
        _session = services.Get<IGameSessionService>();
        ApplyFullWidthLayout();
        _eventBus.Subscribe<GovernmentEnteredEvent>(OnGovernmentEntered);
        _eventBus.Subscribe<MissionAcceptedEvent>(OnMissionAccepted);
        _eventBus.Subscribe<MissionStateChangedEvent>(OnMissionChanged);
    }

    private void OnDestroy()
    {
        if (_eventBus == null) return;
        _eventBus.Unsubscribe<GovernmentEnteredEvent>(OnGovernmentEntered);
        _eventBus.Unsubscribe<MissionAcceptedEvent>(OnMissionAccepted);
        _eventBus.Unsubscribe<MissionStateChangedEvent>(OnMissionChanged);
    }

    private void OnGovernmentEntered(GovernmentEnteredEvent evt) => RefreshMissionBoard();
    private void OnMissionChanged(MissionStateChangedEvent evt) => RefreshMissionBoard();
    private void OnMissionAccepted(MissionAcceptedEvent evt) => RefreshMissionBoard();

    public void RefreshMissionBoard()
    {
        if (_missionService == null || missionListContent == null || missionCardPrefab == null)
            return;

        ClearCards();
        List<MissionInstanceData> visible = new();
        PlanetConfig planet = _configService.GetCurrentPlanetConfig();
        StarSystemConfig system = _configService.GetCurrentSystemConfig();
        if (_missionScroll == null) ApplyFullWidthLayout();
        GameObject listPanel = _missionScroll != null ? _missionScroll.gameObject : missionListContent.gameObject;
        bool insideStation = StationLocationRules.IsInside(_session?.State, system);
        bool showList = insideStation || planet?.IsInhabited == true;
        listPanel.SetActive(showList);
        if (!showList)
        {
            _selectedMission = null;
            ShowEmptyState(false);
            AppLog.Info(DebugLogChannel.Mission, typeof(MissionScreenController),
                "Planet mission list hidden | Planet=" + planet?.Id + " | Reason=Uninhabited");
            return;
        }
        if (!insideStation && system != null)
        {
            MissionInstanceData offer = _governmentMissionService
                .GetOrCreateGovernmentOffer(system, planet)?.OfferedMission;
            if (offer != null && offer.Status == MissionStatus.Available)
                visible.Add(offer);
        }

        foreach (MissionInstanceData mission in _missionService.GetActiveMissions())
        {
            if (mission == null || !MissionLifecycleRules.IsActive(mission.Status))
                continue;
            if (MissionBattleRules.IsBattle(mission.MissionType) &&
                mission.Status != MissionStatus.ReadyToComplete)
                continue;
            if (MissionListVisibilityRules.ShouldShow(mission))
                visible.Add(mission);
        }

        if (_selectedMission != null &&
            !visible.Exists(mission => mission.MissionRuntimeId == _selectedMission.MissionRuntimeId))
            _selectedMission = null;

        foreach (MissionInstanceData mission in visible)
        {
            MissionCardView card = Instantiate(missionCardPrefab, missionListContent);
            bool selected = _selectedMission != null &&
                _selectedMission.MissionRuntimeId == mission.MissionRuntimeId;
            card.Bind(mission, GetMissionIcon(mission.MissionType), _configService,
                HandleMissionSelected, HandleOfferAccepted, HandleMissionDone, selected,
                HandleMissionFly);
            _spawnedCards.Add(card);
        }

        ShowEmptyState(visible.Count == 0);
        AppLog.Info(DebugLogChannel.Mission, typeof(MissionScreenController),
            "Planet mission list refreshed | Planet=" + planet?.Id +
            " | Station=" + (insideStation ? system.Station.Id : "") + " | Visible=" + visible.Count);
    }

    private void HandleMissionSelected(MissionInstanceData mission)
    {
        _selectedMission = mission;
        RefreshMissionBoard();
    }

    private void HandleMissionFly(MissionInstanceData mission)
    {
        if (mission == null ||
            (mission.MissionType != MissionType.Delivery &&
             mission.MissionType != MissionType.Recon &&
             mission.MissionType != MissionType.RouteSearch) ||
            (mission.Status != MissionStatus.Accepted && mission.Status != MissionStatus.InProgress))
            return;
        IServiceRegistry services = Bootstrapper.Instance?.ServiceRegistry;
        PlayerState player = services?.Get<IGameSessionService>()?.State?.Player;
        if (player == null || string.IsNullOrWhiteSpace(player.CurrentSystemId)) return;
        MissionNavigationPlanner.Destination(mission, out string targetSystemId, out _);
        if (!string.IsNullOrWhiteSpace(targetSystemId) && targetSystemId != player.CurrentSystemId)
        {
            IRouteService routes = services.Get<IRouteService>();
            bool upgrade = mission.MissionType == MissionType.RouteSearch &&
                mission.Objective?.RouteIsUpgrade == true;
            bool directRequired = upgrade && !mission.Objective.RouteDirectSurveyed &&
                player.CurrentSystemId == mission.SourceSystemId &&
                routes.HasUnlockedRoute(player.CurrentSystemId, targetSystemId);
            bool indirectRequired = upgrade && mission.Objective.RouteDirectSurveyed &&
                mission.Objective.RouteReturnedAfterDirectSurvey &&
                !mission.Objective.RouteIndirectSurveyed;
            var path = directRequired
                ? new System.Collections.Generic.List<string> { player.CurrentSystemId, targetSystemId }
                : MissionNavigationPlanner.FindShortestUnlockedPath(
                    _configService.GetAllStarSystems(),
                    services.Get<IGameSessionService>().State.Galaxy,
                    routes, player.CurrentSystemId, targetSystemId, indirectRequired,
                    indirectRequired ? mission.SourceSystemId : null,
                    indirectRequired ? mission.Objective.RouteSurveySystemIds : null);
            SystemTransitionController2 transition = FindFirstObjectByType<SystemTransitionController2>(
                FindObjectsInactive.Include);
            if (path.Count < 2)
            {
                transition?.ShowMessage("Доступный маршрут к цели не найден.", this);
                return;
            }
            TravelFailReason reason = services.Get<ITravelService>()
                .GetTravelFailReason(player.CurrentSystemId, path[1]);
            if (reason == TravelFailReason.NotEnoughFuel)
            {
                transition?.ShowTravelFailReason(reason, this);
                AppLog.Info(DebugLogChannel.Mission, typeof(MissionScreenController),
                    "Takeoff blocked by first-hop fuel | From=" + player.CurrentSystemId +
                    " | To=" + path[1]);
                return;
            }
        }
        PlanetController planetController = FindFirstObjectByType<PlanetController>(
            FindObjectsInactive.Include);
        if (planetController != null)
            planetController.TakeOffFromPlanet();
        else
            AppLog.Warning(DebugLogChannel.Mission, typeof(MissionScreenController),
                "Cannot take off from mission card: PlanetController is missing.");
    }

    private void HandleOfferAccepted(MissionInstanceData mission)
    {
        PlanetConfig planet = _configService.GetCurrentPlanetConfig();
        if (mission == null || planet == null) return;
        bool accepted = _governmentMissionService.AcceptGovernmentOffer(planet.Id);
        AppLog.Info(DebugLogChannel.Mission, typeof(MissionScreenController),
            "Accept offer | Planet=" + planet.Id + " | Mission=" + mission.MissionRuntimeId +
            " | Success=" + accepted);
        if (accepted)
            _eventBus.Publish(new GovernmentEnteredEvent());
        else
            RefreshMissionBoard();
    }

    private void HandleMissionDone(MissionInstanceData mission)
    {
        if (mission == null) return;
        bool completed = _missionService.CompleteMission(mission.MissionRuntimeId);
        AppLog.Info(DebugLogChannel.Mission, typeof(MissionScreenController),
            "Claim reward | Mission=" + mission.MissionRuntimeId + " | Success=" + completed);
        if (completed) _selectedMission = null;
        _eventBus.Publish(new GovernmentEnteredEvent());
    }

    private void ApplyFullWidthLayout()
    {
        if (missionListContent == null) return;
        ScrollRect scroll = missionListContent.GetComponentInParent<ScrollRect>();
        if (scroll == null) return;
        _missionScroll = scroll;
        RectTransform rect = scroll.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(0f, BottomHudHeight);
        rect.offsetMax = new Vector2(0f, -_listTopInset);
        Image background = scroll.GetComponent<Image>();
        if (background != null)
        {
            background.sprite = null;
            background.type = Image.Type.Simple;
            background.color = MissionPanelStyle2A.Workspace;
        }
        scroll.horizontal = false;
        scroll.vertical = true;
        VerticalLayoutGroup layout = missionListContent.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.padding.left = 16;
            layout.padding.right = 16;
            layout.padding.top = 14;
            layout.padding.bottom = 14;
            layout.spacing = 16f;
        }
    }

    public void SetListTopInset(float inset)
    {
        _listTopInset = inset;
        if (_missionScroll == null) ApplyFullWidthLayout();
        if (_missionScroll != null)
            _missionScroll.GetComponent<RectTransform>().offsetMax = new Vector2(0f, -inset);
    }

    private void ShowEmptyState(bool show)
    {
        if (!show)
        {
            if (_emptyText != null) _emptyText.gameObject.SetActive(false);
            return;
        }
        if (_emptyText == null)
        {
            GameObject go = new GameObject("MissionEmptyState",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(missionListContent, false);
            _emptyText = go.GetComponent<TextMeshProUGUI>();
            _emptyText.text = "Миссий пока нет";
            _emptyText.fontSize = 32f;
            _emptyText.color = new Color(0.75f, 0.88f, 0.98f, 1f);
            _emptyText.alignment = TextAlignmentOptions.Center;
            go.GetComponent<LayoutElement>().preferredHeight = 90f;
        }
        _emptyText.gameObject.SetActive(true);
    }

    private Sprite GetMissionIcon(MissionType missionType)
    {
        switch (missionType)
        {
            case MissionType.Delivery: return deliveryIcon;
            case MissionType.Recon: return reconIcon;
            case MissionType.Defense:
            case MissionType.Liberation: return eliminationIcon;
            case MissionType.RouteSearch: return reconIcon;
            default: return null;
        }
    }

    private void ClearCards()
    {
        foreach (MissionCardView card in _spawnedCards)
            if (card != null) Destroy(card.gameObject);
        _spawnedCards.Clear();
    }
}
