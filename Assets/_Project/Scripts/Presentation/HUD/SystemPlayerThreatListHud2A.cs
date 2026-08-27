using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class SystemPlayerThreatListHud2A :
    MonoBehaviour,
    ISystemHudWidgetBinder2A,
    ISystemHudPointerClickReceiver2A
{
    private const string PanelName = "PlayerThreatListPanel";
    private const string HeaderName = "ThreatListHeaderButton";
    private const string HeaderTextName = "ThreatListHeaderText";
    private const string ContentName = "ThreatListContent";
    private const string RowTemplateName = "ThreatRowTemplate";

    [Header("Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button headerButton;
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private GameObject contentRoot;

    [Header("Rows")]
    [SerializeField] private Transform rowsRoot;
    [SerializeField] private TMP_Text threatRowsText;
    [SerializeField] private Button rowsButton;
    [SerializeField] private SystemHudPointerClickRelay2A rowsClickRelay;

    [Header("Layout")]
    [SerializeField] private float headerHeight = 48f;
    [SerializeField] private float rowHeight = 34f;
    [SerializeField] private float rowSpacing = 6f;
    [SerializeField] private float verticalPadding = 24f;
    [SerializeField] private float collapsedHeight = 48f;

    [Header("Refresh")]
    [SerializeField] private float refreshIntervalSeconds = 0.2f;

    [Header("Threat Rules")]
    [SerializeField] private float weaponRangeMultiplier = 5f;
    [SerializeField] private float fallbackShotDistance = 12f;
    [SerializeField] private bool showEmptyPanel;

    [Header("Debug")]
    [SerializeField] private bool logThreatDebug = true;
    [SerializeField] private float debugLogIntervalSeconds = 1f;

    [Header("Runtime")]
    [SerializeField] private bool isCollapsed;
    [SerializeField] private int currentThreatCount;

    private readonly List<SystemNpcRuntimeState> _threats = new();

    private ISystemNpcRuntimeService _npcRuntimeService;
    private IPlayerCombatTargetService _playerTargetService;
    private IPlayerAttackService _playerAttackService;
    private SimpleEventBus _eventBus;

    private float _nextRefreshTime;
    private float _nextDebugLogTime;
    private bool _isBound;
    private int _debugCheckedNpcCount;
    private int _debugRejectedNpcCount;
    private int _debugHostileNpcCount;
    private int _debugInRangeHostileNpcCount;

    public bool IsBound => _isBound;

    private void Awake()
    {
        ResolvePrefabUi();

        if (headerButton != null)
            headerButton.onClick.AddListener(ToggleCollapsed);

        if (rowsClickRelay != null)
            rowsClickRelay.SetReceiver(this);

        RefreshCollapsedState();
        SetPanelVisible(false);
    }

    private void OnDestroy()
    {
        if (headerButton != null)
            headerButton.onClick.RemoveListener(ToggleCollapsed);

        Unbind();
    }

    private void Update()
    {
        if (!_isBound)
            return;

        if (Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime =
            Time.unscaledTime +
            Mathf.Max(0.05f, refreshIntervalSeconds);

        RefreshThreats();
    }

    public void Bind(SystemHudBindingContext2A context)
    {
        if (context == null)
        {
            Debug.LogError(
                "[SystemPlayerThreatListHud2A] Missing HUD binding context.",
                this);
            return;
        }

        ResolvePrefabUi();

        if (!HasRequiredUi())
        {
            Debug.LogError(
                "[SystemPlayerThreatListHud2A] Threat HUD prefab UI is not fully bound.",
                this);
            return;
        }

        _npcRuntimeService =
            context.Get<ISystemNpcRuntimeService>();

        _playerTargetService =
            context.Get<IPlayerCombatTargetService>();

        _playerAttackService =
            context.Get<IPlayerAttackService>();

        _eventBus =
            context.Get<SimpleEventBus>();

        _eventBus.Subscribe<SystemNpcDestroyedEvent>(OnNpcDestroyed);
        _eventBus.Subscribe<PlayerDestroyedEvent>(OnPlayerDestroyed);

        LogThreatDebug(
            "Bind OK. PanelRoot: " + (panelRoot != null) +
            ", Header: " + (headerButton != null) +
            ", RowsText: " + (threatRowsText != null));

        _isBound = true;
        _nextRefreshTime = 0f;

        RefreshThreats();
    }

    public void Unbind()
    {
        if (_eventBus != null)
        {
            _eventBus.Unsubscribe<SystemNpcDestroyedEvent>(OnNpcDestroyed);
            _eventBus.Unsubscribe<PlayerDestroyedEvent>(OnPlayerDestroyed);
        }

        _isBound = false;
        _npcRuntimeService = null;
        _playerTargetService = null;
        _playerAttackService = null;
        _eventBus = null;

        LogThreatDebug("Unbind. Hiding threat list panel.");

        ClearRows();
        SetPanelVisible(false);
    }

    private void OnNpcDestroyed(SystemNpcDestroyedEvent evt)
    {
        RefreshThreats();
    }

    private void OnPlayerDestroyed(PlayerDestroyedEvent evt)
    {
        ClearRows();
        SetPanelVisible(false);
    }

    private void ToggleCollapsed()
    {
        isCollapsed = !isCollapsed;
        RefreshCollapsedState();
    }

    private void RefreshCollapsedState()
    {
        if (contentRoot != null)
            contentRoot.SetActive(!isCollapsed);

        RefreshHeaderText();
        RefreshPanelHeight();
    }

    private void RefreshThreats()
    {
        _threats.Clear();
        _debugCheckedNpcCount = 0;
        _debugRejectedNpcCount = 0;
        _debugHostileNpcCount = 0;
        _debugInRangeHostileNpcCount = 0;

        if (_npcRuntimeService == null ||
            _playerTargetService == null)
        {
            currentThreatCount = 0;
            SetPanelVisible(false);
            return;
        }

        IReadOnlyList<SystemNpcRuntimeState> npcs =
            _npcRuntimeService.Npcs;

        bool hasNearbyHostile = false;

        for (int i = 0; i < npcs.Count; i++)
        {
            SystemNpcRuntimeState npc = npcs[i];

            _debugCheckedNpcCount++;

            if (!IsHostileInThreatZone(npc))
            {
                _debugRejectedNpcCount++;
                continue;
            }

            hasNearbyHostile = true;
            _threats.Add(npc);

            if (IsNpcAimingAtPlayer(npc))
            {
                LogThreatDebug(
                    "AIM " + BuildDebugNpcName(npc) +
                    " combat=" + npc.CombatState +
                    " targetNpc=" + Safe(npc.CurrentTargetRuntimeNpcId));
            }
        }

        SortThreatsByDistanceToPlayer();
        currentThreatCount = _threats.Count;
        RebuildRows();

        SetPanelVisible(hasNearbyHostile || showEmptyPanel);
        RefreshHeaderText();
        RefreshPanelHeight();
        LogRefreshSummary(hasNearbyHostile);
    }

    private bool IsHostileInThreatZone(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return RejectThreat("null npc", null);

        if (!npc.IsAlive)
            return RejectThreat("not alive", npc);

        if (!npc.IsHostileToPlayer)
            return RejectThreat("not hostile", npc);

        _debugHostileNpcCount++;

        if (npc.IsOnPlanet)
            return RejectThreat("on planet", npc);

        if (string.IsNullOrWhiteSpace(npc.CurrentSystemId))
            return RejectThreat("empty system id", npc);

        if (!_playerTargetService.IsPlayerAvailableInSystem(npc.CurrentSystemId))
            return RejectThreat("player not available in npc system", npc);

        float maxShotDistance = ResolveMaxShotDistance(npc);

        if (maxShotDistance <= 0f)
            maxShotDistance = fallbackShotDistance;

        Vector3 playerPosition = _playerTargetService.GetPlayerPosition();

        float distance =
            Vector3.Distance(
                ToFlat(playerPosition),
                ToFlat(npc.CurrentPosition));

        float threatDistance = maxShotDistance * weaponRangeMultiplier;

        if (distance > threatDistance)
        {
            return RejectThreat(
                "hostile too far distance=" + distance.ToString("F1") +
                " limit=" + threatDistance.ToString("F1") +
                " shot=" + maxShotDistance.ToString("F1") +
                " player=" + ToFlat(playerPosition).ToString("F1") +
                " npc=" + ToFlat(npc.CurrentPosition).ToString("F1"),
                npc);
        }

        _debugInRangeHostileNpcCount++;

        LogThreatDebug(
            "ZONE " + BuildDebugNpcName(npc) +
            " distance=" + distance.ToString("F1") +
            " limit=" + threatDistance.ToString("F1") +
            " combat=" + npc.CombatState +
            " targetNpc=" + Safe(npc.CurrentTargetRuntimeNpcId));

        return true;
    }

    private bool IsNpcAimingAtPlayer(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return false;

        if (!string.IsNullOrWhiteSpace(npc.CurrentTargetRuntimeNpcId))
            return false;

        return npc.IsHostileToPlayer;
    }

    private float ResolveMaxShotDistance(SystemNpcRuntimeState npc)
    {
        if (npc == null ||
            npc.Weapons == null ||
            npc.Weapons.Count == 0)
        {
            return 0f;
        }

        float maxDistance = 0f;

        for (int i = 0; i < npc.Weapons.Count; i++)
        {
            SystemNpcWeaponRuntimeState weapon = npc.Weapons[i];

            if (weapon == null)
                continue;

            if (weapon.ShotDistance > maxDistance)
                maxDistance = weapon.ShotDistance;
        }

        if (maxDistance <= 0f)
            return fallbackShotDistance;

        return maxDistance;
    }

    private void SortThreatsByDistanceToPlayer()
    {
        if (_playerTargetService == null)
            return;

        Vector3 playerPosition =
            ToFlat(_playerTargetService.GetPlayerPosition());

        _threats.Sort(
            (left, right) =>
            {
                float leftDistance =
                    Vector3.Distance(
                        playerPosition,
                        ToFlat(left.CurrentPosition));

                float rightDistance =
                    Vector3.Distance(
                        playerPosition,
                        ToFlat(right.CurrentPosition));

                return leftDistance.CompareTo(rightDistance);
            });
    }

    private void RebuildRows()
    {
        if (threatRowsText == null)
            return;

        string rowsText = string.Empty;

        for (int i = 0; i < _threats.Count; i++)
        {
            SystemNpcRuntimeState threat = _threats[i];

            if (i > 0)
                rowsText += System.Environment.NewLine;

            rowsText += BuildThreatRowText(threat);
        }

        threatRowsText.text = rowsText;
        threatRowsText.gameObject.SetActive(_threats.Count > 0);
    }

    private string BuildThreatRowText(SystemNpcRuntimeState threat)
    {
        string displayName = "Враг";

        if (threat != null && !string.IsNullOrWhiteSpace(threat.DisplayName))
            displayName = threat.DisplayName.Trim();

        string color = IsNpcAimingAtPlayer(threat) ? "#FF4A4A" : "#FFFFFF";
        string status = BuildEnemyStatusText(threat);

        return
            "<color=" + color + ">" +
            EscapeRichText(displayName + " " + status) +
            "</color>";
    }

    private static string BuildEnemyStatusText(SystemNpcRuntimeState threat)
    {
        if (threat == null)
            return "(0/0)";

        return
            "(" +
            Mathf.Max(0, threat.CurrentHull) +
            "/" +
            Mathf.Max(0, threat.CurrentShield) +
            ")";
    }

    private static string EscapeRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private void ClearRows()
    {
        if (threatRowsText != null)
        {
            threatRowsText.text = string.Empty;
            threatRowsText.gameObject.SetActive(false);
        }

        _threats.Clear();
        currentThreatCount = 0;
        RefreshHeaderText();
        RefreshPanelHeight();
    }

    public void OnHudPointerClicked(
        GameObject source,
        PointerEventData eventData)
    {
        if (source == null || threatRowsText == null || source != threatRowsText.gameObject)
            return;

        OnThreatRowsClicked(eventData);
    }

    private void OnThreatRowsClicked(PointerEventData eventData)
    {
        if (isCollapsed ||
            _threats.Count == 0 ||
            contentRoot == null ||
            eventData == null)
        {
            return;
        }

        RectTransform contentRect =
            contentRoot.GetComponent<RectTransform>();

        if (contentRect == null)
            return;

        Camera eventCamera = null;
        Canvas canvas = contentRoot.GetComponentInParent<Canvas>();

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                contentRect,
                eventData.position,
                eventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        float yFromTop = contentRect.rect.yMax - localPoint.y;
        float rowStep = Mathf.Max(1f, rowHeight + rowSpacing);
        int rowIndex = Mathf.FloorToInt(yFromTop / rowStep);

        if (rowIndex < 0 || rowIndex >= _threats.Count)
            return;

        SystemNpcRuntimeState threat = _threats[rowIndex];

        if (_playerAttackService != null && threat != null)
        {
            _playerAttackService.SetTarget(threat.RuntimeNpcId);

            _eventBus?.Publish(
                new SystemObjectsPanelCloseRequestedEvent2A());

            _eventBus?.Publish(
                new SystemSelectedTargetInfoPanelRequestedEvent2A(
                    threat.RuntimeNpcId,
                    SystemGameplayTargetType.Enemy));
        }

        LogThreatDebug(
            "Row clicked index=" + rowIndex +
            " npc=" + BuildDebugNpcName(threat),
            force: true);
    }

    private void RefreshHeaderText()
    {
        if (headerText == null)
            return;

        string arrow = isCollapsed ? "+" : "-";
        headerText.text = "УГРОЗЫ " + arrow + " " + currentThreatCount;
    }

    private void RefreshPanelHeight()
    {
        if (panelRoot == null)
            return;

        RectTransform panelRect =
            panelRoot.GetComponent<RectTransform>();

        if (panelRect == null)
            return;

        float targetHeight = Mathf.Max(1f, collapsedHeight);

        if (!isCollapsed)
        {
            int rowCount = Mathf.Max(0, currentThreatCount);
            targetHeight = Mathf.Max(1f, headerHeight + verticalPadding);

            if (rowCount > 0)
            {
                targetHeight +=
                    rowCount * Mathf.Max(1f, rowHeight) +
                    Mathf.Max(0, rowCount - 1) * Mathf.Max(0f, rowSpacing);
            }
        }

        panelRect.sizeDelta =
            new Vector2(panelRect.sizeDelta.x, targetHeight);
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot != null && panelRoot.activeSelf != visible)
            panelRoot.SetActive(visible);
    }

    private void ResolvePrefabUi()
    {
        if (panelRoot == null)
            panelRoot = ResolveChild(PanelName);

        if (panelRoot == null)
            return;

        if (headerButton == null)
        {
            GameObject header = ResolveChild(HeaderName, panelRoot.transform);

            if (header != null)
                headerButton = header.GetComponent<Button>();
        }

        if (headerText == null)
        {
            GameObject headerTextObject =
                ResolveChild(HeaderTextName, panelRoot.transform);

            if (headerTextObject != null)
                headerText = headerTextObject.GetComponent<TMP_Text>();
        }

        if (contentRoot == null)
            contentRoot = ResolveChild(ContentName, panelRoot.transform);

        if (rowsRoot == null && contentRoot != null)
            rowsRoot = contentRoot.transform;

        if (threatRowsText == null && contentRoot != null)
        {
            GameObject rowsTextObject =
                ResolveChild(RowTemplateName, contentRoot.transform);

            if (rowsTextObject != null)
                threatRowsText = rowsTextObject.GetComponent<TMP_Text>();
        }

        if (rowsButton == null && threatRowsText != null)
            rowsButton = threatRowsText.GetComponent<Button>();

        if (rowsClickRelay == null && threatRowsText != null)
            rowsClickRelay = threatRowsText.GetComponent<SystemHudPointerClickRelay2A>();

        if (rowsClickRelay != null)
            rowsClickRelay.SetReceiver(this);

        if (threatRowsText != null)
        {
            threatRowsText.text = string.Empty;
            threatRowsText.gameObject.SetActive(false);
        }
    }

    private bool HasRequiredUi()
    {
        return panelRoot != null &&
               headerButton != null &&
               headerText != null &&
               contentRoot != null &&
               rowsRoot != null &&
               threatRowsText != null &&
               rowsButton != null &&
               rowsClickRelay != null;
    }

    private GameObject ResolveChild(string objectName)
    {
        return ResolveChild(objectName, transform);
    }

    private static GameObject ResolveChild(
        string objectName,
        Transform root)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
            return null;

        Transform[] children =
            root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == objectName)
                return children[i].gameObject;
        }

        return null;
    }

    private bool RejectThreat(string reason, SystemNpcRuntimeState npc)
    {
        if (npc == null)
        {
            LogThreatDebug("SKIP null reason=" + reason);
            return false;
        }

        if (npc.IsHostileToPlayer)
        {
            LogThreatDebug(
                "SKIP HOSTILE " + BuildDebugNpcName(npc) +
                " reason=" + reason +
                " combat=" + npc.CombatState +
                " targetNpc=" + Safe(npc.CurrentTargetRuntimeNpcId),
                force: true);
        }

        return false;
    }

    private void LogRefreshSummary(bool hasNearbyHostile)
    {
        LogThreatDebug(
            "Refresh checked=" + _debugCheckedNpcCount +
            " hostile=" + _debugHostileNpcCount +
            " hostileInRange=" + _debugInRangeHostileNpcCount +
            " rejected=" + _debugRejectedNpcCount +
            " nearbyRows=" + currentThreatCount +
            " panelVisible=" + (hasNearbyHostile || showEmptyPanel),
            force: true);
    }

    private void LogThreatDebug(string message, bool force = false)
    {
        if (!logThreatDebug)
            return;

        if (!force &&
            Application.isPlaying &&
            Time.unscaledTime < _nextDebugLogTime)
        {
            return;
        }

        if (Application.isPlaying)
            _nextDebugLogTime = Time.unscaledTime + debugLogIntervalSeconds;

        Debug.Log("[ThreatHUD] " + message, this);
    }

    private static string BuildDebugNpcName(SystemNpcRuntimeState npc)
    {
        if (npc == null)
            return "<null>";

        if (!string.IsNullOrWhiteSpace(npc.DisplayName))
            return npc.DisplayName;

        if (!string.IsNullOrWhiteSpace(npc.ConfigId))
            return npc.ConfigId;

        return Safe(npc.RuntimeNpcId);
    }

    private static string Safe(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<empty>" : value;
    }

    private static Vector3 ToFlat(Vector3 value)
    {
        value.z = 0f;
        return value;
    }
}
