using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SystemObjectsListHud2A :
    CustomMonoBehaviour,
    ISystemHudWidgetBinder2A,
    ISystemHudPointerClickReceiver2A
{
    private const string PlanetPrefix = "planet:";
    private const string StationPrefix = "station:";
    private const string ExitPrefix = "exit:";

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text rowsText;
    [SerializeField] private SystemHudPointerClickRelay2A rowsClickRelay;

    [Header("Open Sources")]
    [SerializeField] private SystemHudPointerClickRelay2A systemTitleClickRelay;

    [Header("Layout")]
    [SerializeField] private float headerHeight = 48f;
    [SerializeField] private float rowHeight = 40f;
    [SerializeField] private float verticalPadding = 8f;
    [SerializeField] private float minPanelHeight = 86f;
    [SerializeField] private float maxPanelHeight = 680f;

    private readonly StringBuilder _builder = new();
    private readonly List<PlanetConfig> _planets = new();
    private readonly List<StationConfig> _stations = new();
    private readonly List<RouteConfig> _routes = new();
    private readonly List<StarSystemConfig> _exitTargets = new();

    private SimpleEventBus _eventBus;
    private IGameSessionService _gameSessionService;
    private IConfigService _configService;
    private ITargetService2A _targetService;
    private IOrbitalMotionService _orbitalMotionService;
    private bool _isBound;

    public bool IsBound => _isBound;

    private void Awake()
    {
        ResolveSerializedReferences();
        HidePanel();
    }

    private void OnEnable()
    {
        ResolveSerializedReferences();
    }

    private void OnDisable()
    {
        if (_isBound)
            Unbind();
    }

    public void Bind(SystemHudBindingContext2A context)
    {
        if (context == null)
            return;

        _eventBus = context.Get<SimpleEventBus>();
        _configService = context.Get<IConfigService>();
        context.TryGet(out _gameSessionService);
        context.TryGet(out _targetService);
        context.TryGet(out _orbitalMotionService);

        if (rowsClickRelay != null)
            rowsClickRelay.SetReceiver(this);

        if (systemTitleClickRelay != null)
            systemTitleClickRelay.SetReceiver(this);

        _eventBus.Subscribe<SystemObjectsPanelRequestedEvent2A>(
            OnPanelRequested);

        _eventBus.Subscribe<SystemObjectsPanelCloseRequestedEvent2A>(
            OnPanelCloseRequested);

        _isBound = true;
        HidePanel();
    }

    public void Unbind()
    {
        if (_eventBus != null)
        {
            _eventBus.Unsubscribe<SystemObjectsPanelRequestedEvent2A>(
                OnPanelRequested);

            _eventBus.Unsubscribe<SystemObjectsPanelCloseRequestedEvent2A>(
                OnPanelCloseRequested);
        }

        _eventBus = null;
        _gameSessionService = null;
        _configService = null;
        _targetService = null;
        _orbitalMotionService = null;
        _isBound = false;
    }

    public void OnHudPointerClicked(
        GameObject source,
        PointerEventData eventData)
    {
        if (source == null)
            return;

        if (systemTitleClickRelay != null &&
            source == systemTitleClickRelay.gameObject)
        {
            ShowPanel();
            return;
        }

        if (rowsClickRelay != null &&
            source == rowsClickRelay.gameObject)
        {
            HandleRowsClicked(eventData);
        }
    }

    private void OnPanelRequested(
        SystemObjectsPanelRequestedEvent2A evt)
    {
        ShowPanel();
    }

    private void OnPanelCloseRequested(
        SystemObjectsPanelCloseRequestedEvent2A evt)
    {
        HidePanel();
    }

    private void ShowPanel()
    {
        _eventBus?.Publish(
            new SystemSelectedTargetInfoPanelCloseRequestedEvent2A());

        if (!IsSystemScene())
        {
            HidePanel();
            return;
        }

        RefreshRows();

        if (panelRoot != null)
            panelRoot.SetActive(true);
        else
        {
            Debug.LogWarning("[SystemObjectsHUD] panelRoot is null.");
        }
    }

    private void HidePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void RefreshRows()
    {
        StarSystemConfig system = GetCurrentSystem();

        _planets.Clear();
        _stations.Clear();
        _routes.Clear();
        _exitTargets.Clear();
        _builder.Clear();

        if (headerText != null)
        {
            headerText.SetText(
                system != null
                    ? system.DisplayName.ToUpperInvariant()
                    : "Система");
        }

        int visualRows = 0;

        if (system == null)
        {
            Debug.LogWarning("[SystemObjectsHUD] Current system is null.");
            AppendMutedLine("Система не найдена");
            visualRows++;
            ApplyHeight(visualRows);
            SetRowsText();
            return;
        }

        visualRows += AppendPlanets(system);
        visualRows += AppendStation(system);
        visualRows += AppendExits(system);

        if (visualRows == 0)
        {
            AppendMutedLine("Нет доступных объектов");
            visualRows++;
        }

        ApplyHeight(visualRows);
        SetRowsText();
    }

    private int AppendPlanets(
        StarSystemConfig system)
    {
        PlanetConfig[] planets = system.PlanetRefs;

        if (planets == null || planets.Length == 0)
            return 0;

        int rows = 0;
        AppendGroup("Планеты");
        rows++;

        for (int i = 0; i < planets.Length; i++)
        {
            PlanetConfig planet = planets[i];

            if (planet == null)
                continue;

            int index = _planets.Count;
            _planets.Add(planet);
            AppendLink(
                PlanetPrefix + index,
                planet.DisplayName);
            rows++;
        }

        return rows;
    }

    private int AppendStation(
        StarSystemConfig system)
    {
        StationConfig station = system.Station;

        if (station == null)
            return 0;

        int index = _stations.Count;
        _stations.Add(station);

        AppendGroup("Станция");
        AppendLink(
            StationPrefix + index,
            station.DisplayName);

        return 2;
    }

    private int AppendExits(
        StarSystemConfig system)
    {
        List<RouteConfig> routes =
            GetRoutesForSystem(system.Id);

        if (routes == null || routes.Count == 0)
            return 0;

        int rows = 0;

        for (int i = 0; i < routes.Count; i++)
        {
            RouteConfig route = routes[i];

            if (route == null)
                continue;

            StarSystemConfig targetSystem =
                route.GetOtherSystem(system.Id);

            if (targetSystem == null)
                continue;

            if (rows == 0)
            {
                AppendGroup("Точки выхода");
                rows++;
            }

            int index = _routes.Count;
            _routes.Add(route);
            _exitTargets.Add(targetSystem);

            AppendLink(
                ExitPrefix + index,
                targetSystem.DisplayName);
            rows++;
        }

        return rows;
    }

    private void AppendGroup(string text)
    {
        if (_builder.Length > 0)
            _builder.AppendLine();

        _builder
            .Append("<color=#58BBF6><b>")
            .Append(text)
            .Append("</b></color>")
            .AppendLine();
    }

    private void AppendLink(
        string linkId,
        string text)
    {
        _builder
            .Append("<link=\"")
            .Append(linkId)
            .Append("\"><color=#FFFFFF>")
            .Append(string.IsNullOrWhiteSpace(text)
                ? "-"
                : text)
            .Append("</color></link>")
            .AppendLine();
    }

    private void AppendMutedLine(string text)
    {
        _builder
            .Append("<color=#AAB7C4>")
            .Append(text)
            .Append("</color>");
    }

    private void SetRowsText()
    {
        if (rowsText != null)
            rowsText.SetText(_builder.ToString());
    }

    private void HandleRowsClicked(
        PointerEventData eventData)
    {
        if (rowsText == null || eventData == null)
            return;

        int linkIndex =
            TMP_TextUtilities.FindIntersectingLink(
                rowsText,
                eventData.position,
                eventData.pressEventCamera);

        if (linkIndex < 0 ||
            linkIndex >= rowsText.textInfo.linkCount)
            return;

        TMP_LinkInfo linkInfo =
            rowsText.textInfo.linkInfo[linkIndex];

        string linkId =
            linkInfo.GetLinkID();

        SelectLinkedObject(linkId);
    }

    private void SelectLinkedObject(string linkId)
    {
        if (string.IsNullOrWhiteSpace(linkId))
            return;

        if (TrySelectPlanet(linkId))
            return;

        if (TrySelectStation(linkId))
            return;

        TrySelectExit(linkId);
    }

    private bool TrySelectPlanet(string linkId)
    {
        if (!TryGetIndex(linkId, PlanetPrefix, out int index))
            return false;

        if (index < 0 || index >= _planets.Count)
            return true;

        PlanetConfig planet = _planets[index];

        if (planet == null)
            return true;

        PublishCloseRequest();
        SelectTargetMarkerForPlanet(planet);

        if (_eventBus != null)
            _eventBus.Publish(new PlanetSelectedEvent(planet));

        return true;
    }

    private bool TrySelectStation(string linkId)
    {
        if (!TryGetIndex(linkId, StationPrefix, out int index))
            return false;

        if (index < 0 || index >= _stations.Count)
            return true;

        StationConfig station = _stations[index];

        if (station == null)
            return true;

        PublishCloseRequest();
        SelectTargetMarkerForStation(station);

        if (_eventBus != null)
            _eventBus.Publish(new StationSelectedEvent(station));

        return true;
    }

    private bool TrySelectExit(string linkId)
    {
        if (!TryGetIndex(linkId, ExitPrefix, out int index))
            return false;

        if (index < 0 || index >= _exitTargets.Count)
            return true;

        RouteConfig route = _routes[index];
        StarSystemConfig targetSystem = _exitTargets[index];
        StarSystemConfig currentSystem = GetCurrentSystem();

        if (route == null ||
            targetSystem == null ||
            currentSystem == null)
        {
            return true;
        }

        RouteEndpointConfig endpoint =
            route.GetDepartureEndpoint(
                currentSystem.Id);

        if (endpoint == null)
        {
            Debug.LogWarning(
                "[SystemObjectsHUD] Cannot select exit. Endpoint is null. " +
                "Route=" + route.Id +
                " CurrentSystem=" + currentSystem.Id);
            return true;
        }

        PublishCloseRequest();
        SelectTargetMarkerForSystemExit(
            targetSystem,
            endpoint.ExitPoint);

        if (_eventBus != null)
        {
            _eventBus.Publish(
                new RouteExitMapChangedEvent(
                    route,
                    currentSystem.Id,
                    targetSystem.Id,
                    endpoint.ExitPoint,
                    route.GetEntryPoint(targetSystem.Id),
                    endpoint.VisualSize));
        }

        return true;
    }

    private void SelectTargetMarkerForPlanet(
        PlanetConfig planet)
    {
        if (_targetService == null ||
            planet == null)
            return;

        Vector3 position =
            GetPlanetCurrentPosition(planet);

        _targetService.TrySelectTarget(
                planet.Id,
                SystemGameplayTargetType.Planet,
                new Vector2(position.x, position.y),
                true,
                true,
                true,
                out TargetSelectionFailReason2A failReason);
    }

    private void SelectTargetMarkerForStation(
        StationConfig station)
    {
        if (_targetService == null ||
            station == null)
            return;

        Vector3 position =
            new Vector3(
                station.LocalOffset.x,
                station.LocalOffset.y,
                0f);

        _targetService.TrySelectTarget(
                station.Id,
                SystemGameplayTargetType.Station,
                new Vector2(position.x, position.y),
                true,
                station.IsActive,
                true,
                out TargetSelectionFailReason2A failReason);
    }

    private void SelectTargetMarkerForSystemExit(
        StarSystemConfig targetSystem,
        Vector3 exitPoint)
    {
        if (_targetService == null ||
            targetSystem == null)
            return;

        _targetService.TrySelectTarget(
                targetSystem.Id,
                SystemGameplayTargetType.TravelPoint,
                new Vector2(exitPoint.x, exitPoint.y),
                true,
                true,
                true,
                out TargetSelectionFailReason2A failReason);
    }

    private Vector3 GetPlanetCurrentPosition(
        PlanetConfig planet)
    {
        if (_orbitalMotionService == null ||
            planet == null ||
            planet.PlanetOrbit == null)
        {
            return Vector3.zero;
        }

        return _orbitalMotionService
            .GetPlanetCurrentPosition(
                planet.PlanetOrbit);
    }

    private void PublishCloseRequest()
    {
        HidePanel();

        if (_eventBus != null)
            _eventBus.Publish(
                new SystemObjectsPanelCloseRequestedEvent2A());
    }

    private StarSystemConfig GetCurrentSystem()
    {
        if (_gameSessionService == null ||
            _gameSessionService.State == null ||
            _gameSessionService.State.Player == null ||
            _configService == null)
        {
            Debug.LogWarning(
                "[SystemObjectsHUD] Cannot resolve current system. " +
                "Session=" + (_gameSessionService != null) +
                " Config=" + (_configService != null));
            return null;
        }

        string systemId =
            _gameSessionService
                .State
                .Player
                .CurrentSystemId;

        if (string.IsNullOrWhiteSpace(systemId))
        {
            Debug.LogWarning("[SystemObjectsHUD] CurrentSystemId is empty.");
            return null;
        }

        bool found =
            _configService.TryGetStarSystem(
            systemId,
            out StarSystemConfig system);

        return system;
    }

    private List<RouteConfig> GetRoutesForSystem(
        string systemId)
    {
        List<RouteConfig> result = new();
        HashSet<string> addedRouteIds = new();

        if (string.IsNullOrWhiteSpace(systemId) ||
            _configService == null)
        {
            Debug.LogWarning(
                "[SystemObjectsHUD] Cannot collect routes. " +
                "SystemId=" + systemId +
                " Config=" + (_configService != null));
            return result;
        }

        IReadOnlyList<StarSystemConfig> systems =
            _configService.GetAllStarSystems();

        if (systems == null)
        {
            Debug.LogWarning("[SystemObjectsHUD] GetAllStarSystems returned null.");
            return result;
        }

        for (int systemIndex = 0;
             systemIndex < systems.Count;
             systemIndex++)
        {
            StarSystemConfig system = systems[systemIndex];

            if (system == null ||
                system.Routes == null)
            {
                continue;
            }

            for (int routeIndex = 0;
                 routeIndex < system.Routes.Count;
                 routeIndex++)
            {
                RouteConfig route = system.Routes[routeIndex];

                if (route == null ||
                    string.IsNullOrWhiteSpace(route.Id) ||
                    !route.ContainsSystem(systemId) ||
                    addedRouteIds.Contains(route.Id))
                {
                    continue;
                }

                addedRouteIds.Add(route.Id);
                result.Add(route);
            }
        }

        return result;
    }

    private static bool IsSystemScene()
    {
        return SceneManager
            .GetActiveScene()
            .name == "SystemScene";
    }

    private void ApplyHeight(int visualRows)
    {
        if (panelRect == null)
            return;

        float height =
            headerHeight +
            verticalPadding +
            visualRows * rowHeight;

        height =
            Mathf.Clamp(
                height,
                minPanelHeight,
                maxPanelHeight);

        panelRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            height);
    }

    private static bool TryGetIndex(
        string linkId,
        string prefix,
        out int index)
    {
        index = -1;

        if (!linkId.StartsWith(
                prefix,
                System.StringComparison.Ordinal))
        {
            return false;
        }

        string rawIndex =
            linkId.Substring(prefix.Length);

        return int.TryParse(rawIndex, out index);
    }

    private void ResolveSerializedReferences()
    {
        if (panelRoot == null)
        {
            Transform panel =
                transform.Find("SystemObjectsListPanel");

            if (panel != null)
                panelRoot = panel.gameObject;
        }

        if (panelRect == null && panelRoot != null)
            panelRect = panelRoot.GetComponent<RectTransform>();

        if (headerText == null && panelRoot != null)
        {
            Transform header =
                panelRoot.transform.Find("HeaderText");

            if (header != null)
                headerText = header.GetComponent<TMP_Text>();
        }

        if (rowsText == null && panelRoot != null)
        {
            Transform rows =
                panelRoot.transform.Find("RowsText");

            if (rows != null)
                rowsText = rows.GetComponent<TMP_Text>();
        }

        if (rowsClickRelay == null && rowsText != null)
            rowsClickRelay = rowsText.GetComponent<SystemHudPointerClickRelay2A>();

        if (systemTitleClickRelay == null)
        {
            Transform systemTitle =
                transform.Find("GroupLeft/System/SystemText");

            if (systemTitle != null)
                systemTitleClickRelay =
                    systemTitle.GetComponent<SystemHudPointerClickRelay2A>();
        }
    }
}
