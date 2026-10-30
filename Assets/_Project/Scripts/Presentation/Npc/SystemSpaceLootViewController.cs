using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SystemSpaceLootViewController : MonoBehaviour,
    ISystemHudPointerClickReceiver2A
{
    [Header("World icons")]
    [SerializeField, Min(1f)] private float iconWorldSize = 28f;
    [SerializeField] private Sprite creditsIcon;
    [SerializeField] private Sprite diamondsIcon;
    [SerializeField] private Sprite collectionMarkerIcon;
    [SerializeField] private Material collectionBeamMaterial;
    [SerializeField] private GameObject selectedTargetMarkerPrefab;
    [SerializeField, Min(0.02f)] private float refreshIntervalSeconds = 0.05f;

    private readonly Dictionary<string, SystemSpaceLootView> _views = new();
    private readonly HashSet<string> _visibleIds = new();
    private SystemSpaceLootService _lootService;
    private IGameSessionService _gameSessionService;
    private IConfigService _configService;
    private IPlayerAttackService _playerAttackService;
    private ITargetService2A _targetService;
    private ISystemTravelService _systemTravelService;
    private ISystemNpcRuntimeService _npcRuntimeService;
    private IGameTimeService _gameTimeService;
    private SimpleEventBus _eventBus;
    private SystemCameraController2A _cameraController;
    private Transform _viewsRoot;
    private GameObject _panel;
    private RectTransform _panelRect;
    private TMP_Text _headerText;
    private TMP_Text _rowsText;
    private Image _headerIcon;
    private string _selectedLootId;
    private float _refreshTimer;

    private void Awake()
    {
        var services = Bootstrapper.Instance.ServiceRegistry;
        _lootService = services.Get<SystemSpaceLootService>();
        _gameSessionService = services.Get<IGameSessionService>();
        _configService = services.Get<IConfigService>();
        _eventBus = services.Get<SimpleEventBus>();
        services.TryGet(out _playerAttackService);
        services.TryGet(out _targetService);
        services.TryGet(out _systemTravelService);
        services.TryGet(out _npcRuntimeService);
        services.TryGet(out _gameTimeService);
        if (collectionBeamMaterial == null)
            AppLog.Warning(DebugLogChannel.Loot,
                "[LOOT_BEAM_MATERIAL_MISSING] Assign collectionBeamMaterial in SystemScene.");

        var root = new GameObject("SpaceLootViewsRoot");
        _viewsRoot = root.transform;
        _viewsRoot.SetParent(transform, false);
    }

    private void Start()
    {
        _cameraController = FindFirstObjectByType<SystemCameraController2A>(
            FindObjectsInactive.Include);
        TryCreatePanel();
        RefreshViews();
    }

    private void OnEnable()
    {
        _eventBus?.Subscribe<SystemObjectsPanelCloseRequestedEvent2A>(OnOtherPanelRequested);
        _eventBus?.Subscribe<SystemObjectsPanelRequestedEvent2A>(OnObjectsPanelRequested);
        _eventBus?.Subscribe<SystemSelectedTargetInfoPanelRequestedEvent2A>(OnTargetPanelRequested);
    }

    private void OnDisable()
    {
        foreach (SystemSpaceLootView view in _views.Values)
            if (view != null)
                view.SetPullBeam(false, Vector3.zero, collectionBeamMaterial, 0f);
        _eventBus?.Unsubscribe<SystemObjectsPanelCloseRequestedEvent2A>(OnOtherPanelRequested);
        _eventBus?.Unsubscribe<SystemObjectsPanelRequestedEvent2A>(OnObjectsPanelRequested);
        _eventBus?.Unsubscribe<SystemSelectedTargetInfoPanelRequestedEvent2A>(OnTargetPanelRequested);
    }

    private void Update()
    {
        // Positions and beams follow the simulation every frame; panel refresh is throttled.
        _visibleIds.Clear();
        if (_lootService != null)
            foreach (SpaceLootRuntimeState loot in _lootService.Loot)
                if (loot != null && loot.SystemId == CurrentSystemId() &&
                    _views.TryGetValue(loot.LootId, out SystemSpaceLootView view) &&
                    view != null)
                {
                    _visibleIds.Add(loot.LootId);
                    view.transform.position = loot.Position;
                    bool pulling = TryGetPullOrigin(loot, out Vector3 origin);
                    view.SetPullBeam(pulling, origin, collectionBeamMaterial,
                        _gameTimeService?.SimulationTimeSeconds ?? 0f);
                }
        foreach (KeyValuePair<string, SystemSpaceLootView> pair in _views)
            if (!_visibleIds.Contains(pair.Key) && pair.Value != null)
                pair.Value.SetPullBeam(false, Vector3.zero, collectionBeamMaterial, 0f);
        _refreshTimer -= Time.unscaledDeltaTime;
        if (_refreshTimer > 0f)
            return;

        _refreshTimer = Mathf.Max(0.02f, refreshIntervalSeconds);
        RefreshViews();
        if (_panel != null && _panel.activeSelf)
            RefreshPanel();
    }

    private bool TryGetPullOrigin(SpaceLootRuntimeState loot, out Vector3 origin)
    {
        origin = Vector3.zero;
        if (string.IsNullOrEmpty(loot.PullCollectorId))
            return false;
        if (loot.PullCollectorId == "player")
        {
            PlayerState player = _gameSessionService?.State?.Player;
            if (player == null || player.IsOnPlanet() ||
                !_lootService.IsPlayerCollectionMarked(loot.LootId))
                return false;
            origin = player.SystemMapShipPosition;
            return true;
        }
        if (_npcRuntimeService == null ||
            !_npcRuntimeService.TryGetNpc(loot.PullCollectorId, out SystemNpcRuntimeState npc) ||
            npc == null || !npc.IsAlive || npc.IsOnPlanet ||
            npc.CurrentSystemId != loot.SystemId ||
            npc.CurrentBehavior != SystemNpcBehaviorType.CollectDebris ||
            npc.LootTargetId != loot.LootId)
            return false;
        origin = npc.CurrentPosition;
        return true;
    }

    private void OnDestroy()
    {
        if (_panel != null)
            Destroy(_panel);
    }

    public void OpenLoot(string lootId)
    {
        SpaceLootRuntimeState loot = FindLoot(lootId);
        if (loot == null || loot.SystemId != CurrentSystemId())
            return;

        if (_selectedLootId == lootId && _targetService != null &&
            _targetService.IsCurrentTarget(lootId))
        {
            if (!_lootService.TryTogglePlayerCollectionMark(lootId, out string error))
            {
                ShowTransientError(error);
                AppLog.Warning(DebugLogChannel.Loot,
                    "[LOOT_MARK_REJECTED] Id=" + lootId + " | Reason=" + error);
            }
            else
            {
                AppLog.Info(DebugLogChannel.Loot,
                    "[LOOT_MARK_TOGGLED] Id=" + lootId +
                    " | Marked=" + _lootService.IsPlayerCollectionMarked(lootId));
            }
            if (_views.TryGetValue(lootId, out SystemSpaceLootView selectedView) &&
                selectedView != null)
                selectedView.SetCollectionMarked(
                    _lootService.IsPlayerCollectionMarked(lootId));
            return;
        }

        _eventBus?.Publish(new SystemObjectsPanelCloseRequestedEvent2A());
        _eventBus?.Publish(new SystemSelectedTargetInfoPanelCloseRequestedEvent2A());
        _selectedLootId = lootId;

        if (TryCreatePanel())
        {
            _panel.SetActive(true);
            RefreshPanel();
        }

        _playerAttackService?.SetLootTarget(lootId);
        _targetService?.TrySelectTarget(lootId, SystemGameplayTargetType.Loot,
            new Vector2(loot.Position.x, loot.Position.y), true, true, true, out _);
        if (_cameraController == null)
            _cameraController = FindFirstObjectByType<SystemCameraController2A>(
                FindObjectsInactive.Include);
        _cameraController?.FocusOnLoot(lootId);
        _systemTravelService?.SetMapPointDestination(loot.Position);
        AppLog.Info(DebugLogChannel.Loot,
            "[LOOT_ROUTE_REQUESTED] Id=" + lootId + " | Position=" + loot.Position);
    }

    public void OnHudPointerClicked(GameObject source, PointerEventData eventData)
    {
        if (_headerText != null && source == _headerText.gameObject)
            HidePanel();
    }

    private void RefreshViews()
    {
        if (_lootService == null)
            return;

        string systemId = CurrentSystemId();
        _visibleIds.Clear();
        foreach (SpaceLootRuntimeState loot in _lootService.Loot)
        {
            if (loot == null || loot.SystemId != systemId ||
                string.IsNullOrWhiteSpace(loot.LootId))
                continue;

            _visibleIds.Add(loot.LootId);
            if (_views.TryGetValue(loot.LootId, out SystemSpaceLootView existing))
            {
                if (existing != null)
                {
                    existing.transform.position = loot.Position;
                    existing.SetCollectionMarked(
                        _lootService.IsPlayerCollectionMarked(loot.LootId));
                    continue;
                }
                _views.Remove(loot.LootId);
            }

            Sprite icon = ResolveIcon(loot);
            if (icon == null)
                continue;

            var objectView = new GameObject("SpaceLoot_" + loot.LootId);
            objectView.layer = 10; // Same clickable world layer as NPCs.
            objectView.transform.SetParent(_viewsRoot, false);
            var view = objectView.AddComponent<SystemSpaceLootView>();
            view.Initialize(loot.LootId, icon, loot.Position, iconWorldSize,
                selectedTargetMarkerPrefab, this, collectionMarkerIcon);
            view.SetCollectionMarked(
                _lootService.IsPlayerCollectionMarked(loot.LootId));
            _views.Add(loot.LootId, view);
        }

        var stale = new List<string>();
        foreach (KeyValuePair<string, SystemSpaceLootView> pair in _views)
            if (!_visibleIds.Contains(pair.Key))
                stale.Add(pair.Key);

        foreach (string id in stale)
        {
            if (id == _selectedLootId)
                HidePanel();
            if (_targetService != null && _targetService.IsCurrentTarget(id))
                _targetService.ClearTarget();
            if (_views[id] != null)
                Destroy(_views[id].gameObject);
            _views.Remove(id);
        }
    }

    private bool TryCreatePanel()
    {
        if (_panel != null)
            return true;

        SystemObjectsListHud2A source = FindFirstObjectByType<SystemObjectsListHud2A>(
            FindObjectsInactive.Include);
        Transform template = source != null
            ? source.transform.Find("SystemObjectsListPanel")
            : null;
        if (template == null)
            return false;

        _panel = Instantiate(template.gameObject, template.parent);
        _panel.name = "SpaceLootInfoPanel";
        _panel.transform.SetAsLastSibling();
        _panelRect = _panel.GetComponent<RectTransform>();
        _headerText = _panel.transform.Find("HeaderText")?.GetComponent<TMP_Text>();
        _rowsText = _panel.transform.Find("RowsText")?.GetComponent<TMP_Text>();

        if (_headerText != null)
        {
            _headerText.alignment = TextAlignmentOptions.Midline;
            _headerText.margin = new Vector4(52f, 0f, 52f, 0f);
            _headerText.fontSizeMax = _headerText.fontSize;
            _headerText.fontSizeMin = 16f;
            _headerText.enableAutoSizing = true;
            var click = _headerText.GetComponent<SystemHudPointerClickRelay2A>();
            click?.SetReceiver(this);

            var iconObject = new GameObject("LootHeaderIcon", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(_panel.transform, false);
            RectTransform rect = iconObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-8f, -8f);
            rect.sizeDelta = new Vector2(32f, 32f);
            _headerIcon = iconObject.GetComponent<Image>();
            _headerIcon.raycastTarget = false;
        }

        if (_rowsText != null)
            _rowsText.GetComponent<SystemHudPointerClickRelay2A>()?.SetReceiver(null);

        _panel.SetActive(false);
        return true;
    }

    private void RefreshPanel()
    {
        SpaceLootRuntimeState loot = FindLoot(_selectedLootId);
        if (loot == null || loot.SystemId != CurrentSystemId())
        {
            HidePanel();
            return;
        }

        ResolveDescription(loot, out string name, out int unitCargoSize);
        if (_headerText != null && _headerText.text != name)
        {
            _headerText.SetText(name);
            _headerText.ForceMeshUpdate();
            if (_headerIcon != null)
                _headerIcon.rectTransform.anchoredPosition = new Vector2(
                    -_headerText.textBounds.size.x * 0.5f - 8f, -8f);
        }
        _rowsText?.SetText(SpaceLootDisplayFormatter.Format(
            loot.Quantity, unitCargoSize));
        if (_headerIcon != null)
        {
            _headerIcon.sprite = ResolveIcon(loot);
            _headerIcon.enabled = _headerIcon.sprite != null;
        }
        if (_panelRect != null)
            _panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 150f);
    }

    private void HidePanel()
    {
        if (!string.IsNullOrWhiteSpace(_selectedLootId))
        {
            if (_targetService != null && _targetService.IsCurrentTarget(_selectedLootId))
                _targetService.ClearTarget();
            _playerAttackService?.ClearSelectedTargetIfNoAssignedWeapons();
        }
        _selectedLootId = null;
        if (_panel != null)
            _panel.SetActive(false);
    }

    private void OnOtherPanelRequested(SystemObjectsPanelCloseRequestedEvent2A evt) => HidePanel();
    private void OnObjectsPanelRequested(SystemObjectsPanelRequestedEvent2A evt) => HidePanel();
    private void OnTargetPanelRequested(SystemSelectedTargetInfoPanelRequestedEvent2A evt) => HidePanel();

    private void ResolveDescription(SpaceLootRuntimeState loot,
        out string name, out int unitCargoSize)
    {
        if (loot.Kind == SpaceLootKind.Equipment)
        {
            WeaponConfig config = _configService.GetWeaponConfigById(loot.EquipmentConfigId);
            name = config != null ? config.DisplayName : "Оборудование";
            unitCargoSize = loot.Equipment != null && loot.Equipment.RolledStats.CargoSize > 0
                ? loot.Equipment.RolledStats.CargoSize
                : config != null ? config.CargoSize : 0;
            return;
        }

        if (loot.Kind == SpaceLootKind.Credits || loot.Kind == SpaceLootKind.Diamonds)
        {
            name = loot.Kind == SpaceLootKind.Credits ? "Кредиты" : "Алмазы";
            unitCargoSize = 0;
            return;
        }

        ItemConfig item = _configService.GetItemConfigById(loot.ItemConfigId);
        name = item != null ? item.DisplayName : "Товар";
        unitCargoSize = item != null ? item.CargoSize : 0;
    }

    private Sprite ResolveIcon(SpaceLootRuntimeState loot)
    {
        if (loot.Kind == SpaceLootKind.Equipment)
            return _configService.GetWeaponConfigById(loot.EquipmentConfigId)?.Icon;
        if (loot.Kind == SpaceLootKind.Credits)
            return creditsIcon;
        if (loot.Kind == SpaceLootKind.Diamonds)
            return diamondsIcon;
        return _configService.GetItemConfigById(loot.ItemConfigId)?.Icon;
    }

    private SpaceLootRuntimeState FindLoot(string lootId)
    {
        if (string.IsNullOrWhiteSpace(lootId) || _lootService == null)
            return null;
        foreach (SpaceLootRuntimeState loot in _lootService.Loot)
            if (loot != null && loot.LootId == lootId)
                return loot;
        return null;
    }

    private string CurrentSystemId()
    {
        return _gameSessionService?.State?.Player?.CurrentSystemId;
    }

    private static void ShowTransientError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        var transition = FindFirstObjectByType<SystemTransitionController2>(
            FindObjectsInactive.Include);
        transition?.ShowMessage(message);
    }
}
