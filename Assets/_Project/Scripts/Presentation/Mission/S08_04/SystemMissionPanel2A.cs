using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Compact mission helper on the system-map Canvas.</summary>
public sealed class SystemMissionPanel2A : MonoBehaviour, ISystemHudPointerClickReceiver2A
{
    private Button _button;
    private RectTransform _buttonRect;
    private GameObject _panel;
    private RectTransform _panelRect;
    private TMP_Text _panelTitle;
    private TMP_Text _panelRows;
    private readonly List<MissionInstanceData> _displayedMissions = new();
    private readonly List<GameObject> _missionHitAreas = new();
    private int _openedFrame;
    private IMissionService _missions;
    private IConfigService _config;
    private IGameSessionService _session;
    private IRouteService _routes;
    private SimpleEventBus _events;

    public void Initialize(Button galaxyButton, Sprite missionIcon)
    {
        if (galaxyButton == null || _button != null) return;
        IServiceRegistry services = Bootstrapper.Instance.ServiceRegistry;
        _missions = services.Get<IMissionService>();
        _config = services.Get<IConfigService>();
        _session = services.Get<IGameSessionService>();
        _routes = services.Get<IRouteService>();
        _events = services.Get<SimpleEventBus>();

        CreateButton(galaxyButton, missionIcon);
        CreatePanel();
        _events.Subscribe<MissionStateChangedEvent>(OnMissionChanged);
        _events.Subscribe<MissionAcceptedEvent>(OnMissionAccepted);
        _events.Subscribe<SystemObjectsPanelRequestedEvent2A>(OnObjectsPanelRequested);
        _events.Subscribe<SystemObjectsPanelCloseRequestedEvent2A>(OnObjectsPanelCloseRequested);
    }

    private void OnDestroy()
    {
        if (_events != null)
        {
            _events.Unsubscribe<MissionStateChangedEvent>(OnMissionChanged);
            _events.Unsubscribe<MissionAcceptedEvent>(OnMissionAccepted);
            _events.Unsubscribe<SystemObjectsPanelRequestedEvent2A>(OnObjectsPanelRequested);
            _events.Unsubscribe<SystemObjectsPanelCloseRequestedEvent2A>(OnObjectsPanelCloseRequested);
        }
        if (_button != null) _button.onClick.RemoveListener(Toggle);
    }

    private void OnMissionChanged(MissionStateChangedEvent evt)
    {
        if (_panel != null && _panel.activeSelf) RefreshRows();
    }

    private void OnMissionAccepted(MissionAcceptedEvent evt)
    {
        if (_panel != null && _panel.activeSelf) RefreshRows();
    }

    private void OnObjectsPanelRequested(SystemObjectsPanelRequestedEvent2A evt) => Close();

    private void OnObjectsPanelCloseRequested(SystemObjectsPanelCloseRequestedEvent2A evt) => Close();

    public void OnHudPointerClicked(GameObject source, PointerEventData eventData)
    {
        if (source == _panelTitle?.gameObject)
            Close();
    }

    private void Update()
    {
        if (_panel == null || !_panel.activeSelf || Time.frameCount == _openedFrame)
            return;
        bool touchPressed = Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        bool mousePressed = Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame;
        if (!touchPressed && !mousePressed) return;
        Canvas canvas = _panelRect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        Vector2 point = touchPressed
            ? Touchscreen.current.primaryTouch.position.ReadValue()
            : Mouse.current.position.ReadValue();
        if (!RectTransformUtility.RectangleContainsScreenPoint(_panelRect, point, camera) &&
            !RectTransformUtility.RectangleContainsScreenPoint(_buttonRect, point, camera))
            _panel.SetActive(false);
    }

    private void CreateButton(Button galaxyButton, Sprite missionIcon)
    {
        GameObject clone = Instantiate(galaxyButton.gameObject, galaxyButton.transform.parent, false);
        clone.name = "OpenMissionsButton";
        clone.transform.SetSiblingIndex(galaxyButton.transform.GetSiblingIndex() + 1);
        _button = clone.GetComponent<Button>();
        _button.onClick = new Button.ButtonClickedEvent();
        _button.onClick.AddListener(Toggle);
        _buttonRect = clone.GetComponent<RectTransform>();
        Image image = clone.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = missionIcon;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
        }
    }

    private void CreatePanel()
    {
        SystemObjectsListHud2A objectsHud = FindFirstObjectByType<SystemObjectsListHud2A>(
            FindObjectsInactive.Include);
        Transform source = objectsHud != null
            ? objectsHud.transform.Find("SystemObjectsListPanel") : null;
        if (source == null)
        {
            AppLog.Error(DebugLogChannel.Mission, typeof(SystemMissionPanel2A),
                "SystemObjectsListPanel is missing; mission panel cannot use its style.");
            return;
        }
        GameObject go = Instantiate(source.gameObject, source.parent, false);
        go.name = "SystemMissionPanel";
        go.transform.SetAsLastSibling();
        _panel = go;
        _panelRect = go.GetComponent<RectTransform>();
        _panelTitle = go.transform.Find("HeaderText")?.GetComponent<TMP_Text>();
        _panelRows = go.transform.Find("RowsText")?.GetComponent<TMP_Text>();
        if (_panelTitle != null)
            _panelTitle.GetComponent<SystemHudPointerClickRelay2A>()?.SetReceiver(this);
        _panel.SetActive(false);

    }

    private void Toggle()
    {
        if (_panel == null) return;
        bool open = !_panel.activeSelf;
        if (open)
        {
            _events?.Publish(new SystemObjectsPanelCloseRequestedEvent2A());
            _events?.Publish(new SystemSelectedTargetInfoPanelCloseRequestedEvent2A());
            RefreshRows();
        }
        _panel.SetActive(open);
        _openedFrame = Time.frameCount;
        AppLog.Info(DebugLogChannel.Mission, typeof(SystemMissionPanel2A),
            "System mission panel " + (open ? "opened" : "closed"));
    }

    public void Close()
    {
        if (_panel != null) _panel.SetActive(false);
    }

    private void RefreshRows()
    {
        if (_panel == null || _missions == null || _panelRows == null) return;
        foreach (GameObject previous in _missionHitAreas)
        {
            if (previous == null) continue;
            previous.SetActive(false);
            Destroy(previous);
        }
        _missionHitAreas.Clear();
        _displayedMissions.Clear();
        foreach (MissionInstanceData mission in _missions.GetActiveMissions())
        {
            if (mission == null || !MissionLifecycleRules.IsActive(mission.Status))
                continue;
            if (MissionBattleRules.IsBattle(mission.MissionType) &&
                mission.Status != MissionStatus.ReadyToComplete)
                continue;
            _displayedMissions.Add(mission);
        }
        if (_panelTitle != null)
            _panelTitle.SetText("<color=#FFFFFF>МИССИИ</color>");
        if (_displayedMissions.Count == 0)
        {
            _panelRows.gameObject.SetActive(true);
            _panelRows.SetText("<color=#AAB7C4>Активных миссий нет</color>");
            _panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 120f);
            return;
        }
        _panelRows.gameObject.SetActive(false);
        float rowTop = 56f;
        foreach (MissionInstanceData mission in _displayedMissions)
        {
            bool routeMission = mission.MissionType == MissionType.RouteSearch;
            bool recon = mission.MissionType == MissionType.Recon;
            bool returningForReward = routeMission &&
                mission.Objective?.RouteIndirectSurveyed == true &&
                (!mission.Objective.RouteIsUpgrade || mission.Objective.RouteDirectSurveyed);
            bool reconSurveying = MissionUiText.IsReconSurveying(
                mission, _session?.State?.Player);
            bool reconRewardReady = recon &&
                mission.Status == MissionStatus.ReadyToComplete;
            bool battleRewardReady = MissionBattleRules.IsBattle(mission.MissionType) &&
                mission.Status == MissionStatus.ReadyToComplete;
            var text = new StringBuilder();
            text.Append("<color=")
                .Append(returningForReward || reconRewardReady || battleRewardReady
                    ? "#70E394" : "#FFFFFF")
                .Append(">")
                .Append(MissionUiText.Title(mission, _config)).Append("</color>");
            if (reconSurveying)
                text.Append(" <color=#AAB7C4>· Разведка</color>");
            else if (reconRewardReady)
                text.Append(" <color=#70E394>· Заберите награду</color>");
            text.Append("\n<color=#AAB7C4>");
            if (!routeMission && !recon && mission.MissionType != MissionType.Delivery &&
                !battleRewardReady)
                text.Append(MissionUiText.Goal(mission, _config)).Append(" → ");
            text.Append(battleRewardReady
                    ? "Заберите награду на ближайшей населенной планете"
                    : MissionUiText.NextStep(mission, _config, _session?.State?.Player))
                .Append("</color>");
            float textWidth = Mathf.Max(100f, _panelRect.rect.width - 44f);
            float rowHeight = Mathf.Max(86f,
                _panelRows.GetPreferredValues(text.ToString(), textWidth, Mathf.Infinity).y + 18f);
            CreateMissionHitArea(mission, text.ToString(), rowTop, rowHeight);
            rowTop += rowHeight + 6f;
        }
        _panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Clamp(rowTop + 8f, 120f, 680f));
    }

    private void CreateMissionHitArea(MissionInstanceData mission, string content,
        float top, float height)
    {
        GameObject row = new GameObject("MissionRow", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(Button));
        row.transform.SetParent(_panel.transform, false);
        RectTransform rect = row.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(-20f, height);
        Image background = row.GetComponent<Image>();
        background.color = new Color(0.12f, 0.25f, 0.36f, 0.14f);
        Button button = row.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(() => SelectMission(mission));

        GameObject label = new GameObject("MissionRowText", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        label.transform.SetParent(row.transform, false);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 8f);
        labelRect.offsetMax = new Vector2(-10f, -8f);
        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
        text.font = _panelRows.font;
        text.fontSharedMaterial = _panelRows.fontSharedMaterial;
        text.fontSize = _panelRows.fontSize;
        text.fontStyle = _panelRows.fontStyle;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.SetText(content);
        _missionHitAreas.Add(row);
    }

    private void SelectMission(MissionInstanceData mission)
    {
        Close();
        if (mission == null || _session?.State?.Player == null) return;
        MissionNavigationPlanner.Destination(mission, out string targetSystemId, out string targetPlanetId);
        string currentSystemId = _session.State.Player.CurrentSystemId;
        if (string.IsNullOrWhiteSpace(targetSystemId))
        {
            ShowNotice("Цель миссии недоступна.");
            return;
        }

        if (currentSystemId == targetSystemId)
        {
            if (string.IsNullOrWhiteSpace(targetPlanetId) &&
                mission.Status == MissionStatus.ReadyToComplete &&
                MissionBattleRules.IsBattle(mission.MissionType))
                targetPlanetId = NearestInhabitedPlanet();
            if (string.IsNullOrWhiteSpace(targetPlanetId))
            {
                ShowNotice(MissionUiText.NextStep(mission, _config, _session?.State?.Player));
                return;
            }
            foreach (PlanetSelectableView2 view in FindObjectsByType<PlanetSelectableView2>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (view != null && view.Planet != null && view.Planet.Id == targetPlanetId)
                {
                    view.OnPointerClick(null);
                    LogRoute(mission, "Planet=" + targetPlanetId);
                    return;
                }
            }
            ShowNotice("Планета цели сейчас недоступна.");
            return;
        }

        bool indirect = mission.MissionType == MissionType.RouteSearch &&
                        mission.Objective != null && mission.Objective.RouteIsUpgrade &&
                        mission.Objective.RouteDirectSurveyed &&
                        mission.Objective.RouteReturnedAfterDirectSurvey &&
                        !mission.Objective.RouteIndirectSurveyed;
        bool directRequired = mission.MissionType == MissionType.RouteSearch &&
                              mission.Objective != null && mission.Objective.RouteIsUpgrade &&
                              !mission.Objective.RouteDirectSurveyed &&
                              currentSystemId == mission.SourceSystemId &&
                              _routes.HasUnlockedRoute(currentSystemId, targetSystemId);
        List<string> path = directRequired
            ? new List<string> { currentSystemId, targetSystemId }
            : MissionNavigationPlanner.FindShortestUnlockedPath(
                _config.GetAllStarSystems(), _session.State.Galaxy, _routes,
                currentSystemId, targetSystemId, indirect,
                indirect ? mission.SourceSystemId : null,
                indirect ? mission.Objective.RouteSurveySystemIds : null);
        if (path.Count < 2)
        {
            ShowNotice("Доступный маршрут к цели не найден.");
            LogRoute(mission, "Unavailable | System=" + targetSystemId);
            return;
        }
        string firstHop = path[1];
        foreach (SystemExitNodeView2A exit in FindObjectsByType<SystemExitNodeView2A>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (exit != null && exit.TargetSystemId == firstHop)
            {
                exit.SelectForMission();
                LogRoute(mission, "FirstExit=" + firstHop);
                return;
            }
        }
        ShowNotice("Первая точка выхода сейчас недоступна.");
        LogRoute(mission, "ExitMissing | FirstHop=" + firstHop);
    }

    private string NearestInhabitedPlanet()
    {
        Vector2 playerPosition = (Vector2)_session.State.Player.SystemMapShipPosition;
        float best = float.MaxValue;
        string chosen = null;
        foreach (PlanetSelectableView2 view in FindObjectsByType<PlanetSelectableView2>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (view?.Planet == null || !view.Planet.IsInhabited) continue;
            float distance = Vector2.Distance(playerPosition, (Vector2)view.transform.position);
            if (distance >= best) continue;
            best = distance;
            chosen = view.Planet.Id;
        }
        return chosen;
    }

    private void ShowNotice(string message)
    {
        SystemTransitionController2 transition = FindFirstObjectByType<SystemTransitionController2>(
            FindObjectsInactive.Include);
        transition?.ShowMessage(message);
    }

    private static void PlaceRow(RectTransform rect, float top, float height, float left, float right)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2((left - right) * 0.5f, -top);
        rect.sizeDelta = new Vector2(-left - right, height);
    }

    private static void Stretch(RectTransform rect, float horizontal, float vertical)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontal, vertical);
        rect.offsetMax = new Vector2(-horizontal, -vertical);
    }

    private static void LogRoute(MissionInstanceData mission, string result)
    {
        AppLog.Info(DebugLogChannel.Mission, typeof(SystemMissionPanel2A),
            "Mission navigation | Id=" + mission.MissionRuntimeId + " | " + result);
    }
}
