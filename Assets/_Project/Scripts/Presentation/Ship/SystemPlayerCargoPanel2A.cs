using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Built from the planet-list panel so the cargo UI follows the existing HUD style.
[DisallowMultipleComponent]
public sealed class SystemPlayerCargoPanel2A : MonoBehaviour
{
    private sealed class CargoCard
    {
        public string ItemId;
        public string EquipmentId;
        public string Name;
        public string OriginLabel;
        public int Count;
        public int UnitVolume;
        public Sprite Icon;
        public Color Background;
        public GameObject Root;
    }

    private const float CardSpacing = 10f;
    [Header("Optional HUD References")]
    [SerializeField] private SystemObjectsListHud2A panelStyleSource;
    [SerializeField] private RectTransform bottomHudPanel;
    private readonly List<CargoCard> _cards = new();
    private readonly Vector3[] _hudCorners = new Vector3[4];
    private IInventoryService _inventory;
    private IGameSessionService _session;
    private IConfigService _configs;
    private SystemSpaceLootService _loot;
    private IShipMovementService _movement;
    private SimpleEventBus _events;
    private GameObject _panel;
    private RectTransform _canvasRect;
    private RectTransform _panelRect;
    private RectTransform _viewport;
    private RectTransform _content;
    private TMP_Text _headerText;
    private TMP_Text _selectedVolumeText;
    private TMP_Text _quantityText;
    private TMP_Text _cardTextStyle;
    private TMP_FontAsset _font;
    private Material _fontMaterial;
    private Color _textColor = Color.white;
    private Color _cardColor = new(0.32f, 0.04f, 0.035f, 0.96f);
    private Sprite _panelSprite;
    private Button _minusButton;
    private Button _plusButton;
    private Button _dropButton;
    private SystemPlayerWeaponListHud2A[] _weaponHuds;
    private SystemPlayerThreatListHud2A[] _threatHuds;
    private string _selectedItemId;
    private string _selectedEquipmentId;
    private int _selectedMax;
    private int _quantity = 1;
    private float _cardSize = 288f;
    private float _refreshAt;
    private int _lastFingerprint;

    public void Toggle()
    {
        if (!EnsurePanel()) return;
        if (_panel.activeSelf) { Close(); return; }
        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();
        SetCombatPanelsSuppressed(true);
        _lastFingerprint = int.MinValue;
        ApplyLayout();
        Refresh();
        AppLog.Info(DebugLogChannel.Loot, "[CARGO_PANEL_OPEN]");
    }

    public void Close()
    {
        if (_panel == null || !_panel.activeSelf) return;
        _panel.SetActive(false);
        SetCombatPanelsSuppressed(false);
        AppLog.Info(DebugLogChannel.Loot, "[CARGO_PANEL_CLOSED]");
    }

    private void Update()
    {
        if (_panel == null || !_panel.activeSelf) return;
        if (_session.State?.Player == null || _session.State.Player.IsOnPlanet() ||
            (_movement?.State != null && _movement.State.IsMoving))
        {
            Close();
            return;
        }
        ApplyLayout();
        if (Time.unscaledTime < _refreshAt) return;
        _refreshAt = Time.unscaledTime + 0.2f;
        Refresh();
    }

    private void OnDisable() => Close();

    private void OnDestroy()
    {
        Close();
        _events?.Unsubscribe<CargoChangedEvent>(OnCargoChanged);
        _events?.Unsubscribe<PlayerDestroyedEvent>(OnPlayerDestroyed);
        _events?.Unsubscribe<SystemTravelStartedEvent>(OnSystemTravelStarted);
        _events?.Unsubscribe<TravelStartedEvent>(OnTravelStarted);
        if (_panel != null) DestroyUiObject(_panel);
    }

    private void OnCargoChanged(CargoChangedEvent evt)
    {
        _lastFingerprint = int.MinValue;
        if (_panel != null && _panel.activeSelf) Refresh();
    }

    private void OnPlayerDestroyed(PlayerDestroyedEvent evt) => Close();
    private void OnSystemTravelStarted(SystemTravelStartedEvent evt) => Close();
    private void OnTravelStarted(TravelStartedEvent evt) => Close();

    private bool EnsurePanel()
    {
        if (_panel != null) return true;
        var services = Bootstrapper.Instance?.ServiceRegistry;
        if (services == null) return false;
        services.TryGet(out _inventory);
        services.TryGet(out _session);
        services.TryGet(out _configs);
        services.TryGet(out _loot);
        services.TryGet(out _movement);
        services.TryGet(out _events);
        if (_inventory == null || _session == null || _configs == null || _loot == null)
            return false;

        var objectsHud = panelStyleSource != null ? panelStyleSource
            : FindFirstObjectByType<SystemObjectsListHud2A>(FindObjectsInactive.Include);
        Transform template = objectsHud != null
            ? objectsHud.transform.Find("SystemObjectsListPanel") : null;
        Canvas canvas = objectsHud != null ? objectsHud.GetComponentInParent<Canvas>(true) : null;
        if (template == null || canvas == null)
        {
            AppLog.Warning(DebugLogChannel.Loot, "[CARGO_PANEL_TEMPLATE_MISSING]");
            return false;
        }

        canvas = canvas.rootCanvas;
        _canvasRect = canvas.transform as RectTransform;
        if (bottomHudPanel == null)
        {
            var bottom = FindFirstObjectByType<SystemOffscreenEnemyIndicatorsHud2A>(FindObjectsInactive.Include);
            bottomHudPanel = bottom != null ? bottom.transform as RectTransform : null;
        }
        _weaponHuds ??= FindObjectsByType<SystemPlayerWeaponListHud2A>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        _threatHuds ??= FindObjectsByType<SystemPlayerThreatListHud2A>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        _panel = Instantiate(template.gameObject, canvas.transform, false);
        _panel.name = "PlayerCargoPanel";
        _panel.SetActive(false);
        _panelRect = _panel.GetComponent<RectTransform>();
        _panelRect.anchorMin = _panelRect.anchorMax = new Vector2(0.5f, 0f);
        _panelRect.pivot = new Vector2(0.5f, 0f);
        foreach (var relay in _panel.GetComponentsInChildren<SystemHudPointerClickRelay2A>(true))
            relay.SetReceiver(null);
        Transform rows = _panel.transform.Find("RowsText");
        if (rows != null)
        {
            _cardTextStyle = rows.GetComponent<TMP_Text>();
            rows.gameObject.SetActive(false);
        }
        TMP_Text title = _panel.transform.Find("HeaderText")?.GetComponent<TMP_Text>();
        _headerText = title;
        if (title != null)
        {
            _font = title.font;
            _fontMaterial = title.fontSharedMaterial;
            _textColor = title.color;
            title.SetText("ТРЮМ");
            title.raycastTarget = false;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(-20f, 0f), new Vector2(-64f, 48f));
        }
        Image background = _panel.GetComponent<Image>();
        _panelSprite = background != null ? background.sprite : null;
        Image headerBackground = _panel.transform.Find("HeaderBackground")?.GetComponent<Image>();
        if (headerBackground != null) _cardColor = headerBackground.color;

        Button close = NewButton("Close", _panel.transform, "", Vector2.zero, new Vector2(48f, 48f));
        SetRect(close.GetComponent<RectTransform>(), Vector2.one, Vector2.one,
            new Vector2(1f, 1f), Vector2.zero, new Vector2(48f, 48f));
        GameObject closeIcon = NewRect("Icon", close.transform, new Vector2(46f, 46f));
        Image closeImage = closeIcon.AddComponent<Image>();
        closeImage.sprite = Resources.Load<Sprite>("ui_panel_close_01");
        closeImage.preserveAspect = true;
        closeImage.raycastTarget = false;
        if (closeImage.sprite == null)
            AppLog.Warning(DebugLogChannel.Loot, "[CARGO_CLOSE_ICON_MISSING]");
        close.onClick.AddListener(Close);

        GameObject viewport = NewRect("Viewport", _panel.transform, Vector2.zero);
        _viewport = viewport.GetComponent<RectTransform>();
        _viewport.anchorMin = Vector2.zero;
        _viewport.anchorMax = Vector2.one;
        _viewport.offsetMin = new Vector2(16f, 64f);
        _viewport.offsetMax = new Vector2(-16f, -56f);
        viewport.AddComponent<RectMask2D>();
        viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
        _content = NewRect("Cards", viewport.transform, Vector2.zero).GetComponent<RectTransform>();
        SetRect(_content, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(0f, _cardSize));
        var scroll = viewport.AddComponent<ScrollRect>();
        scroll.viewport = _viewport;
        scroll.content = _content;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.scrollSensitivity = 28f;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        AddHorizontalScrollbar(scroll);
        _selectedVolumeText = NewText("SelectedCargoSpace", _panel.transform,
            "места в трюме 0 ед", 26f, Vector2.zero, Vector2.zero);
        _selectedVolumeText.textWrappingMode = TextWrappingModes.NoWrap;
        _selectedVolumeText.alignment = TextAlignmentOptions.Left;

        _minusButton = NewButton("Less", _panel.transform, "−", Vector2.zero, new Vector2(44f, 36f));
        _minusButton.onClick.AddListener(() => ChangeQuantity(-1));
        _quantityText = NewText("Quantity", _panel.transform, "—", 18f, Vector2.zero, new Vector2(88f, 36f));
        _plusButton = NewButton("More", _panel.transform, "+", Vector2.zero, new Vector2(44f, 36f));
        _plusButton.onClick.AddListener(() => ChangeQuantity(1));
        _dropButton = NewButton("Drop", _panel.transform, "Выбросить", Vector2.zero, new Vector2(220f, 36f));
        _dropButton.onClick.AddListener(DropSelected);
        _events?.Subscribe<CargoChangedEvent>(OnCargoChanged);
        _events?.Subscribe<PlayerDestroyedEvent>(OnPlayerDestroyed);
        _events?.Subscribe<SystemTravelStartedEvent>(OnSystemTravelStarted);
        _events?.Subscribe<TravelStartedEvent>(OnTravelStarted);
        ApplyLayout();
        UpdateSelection();
        return true;
    }

    private void ApplyLayout()
    {
        if (_canvasRect == null || _panelRect == null) return;
        float bottom = 158f;
        if (bottomHudPanel != null)
        {
            bottomHudPanel.GetWorldCorners(_hudCorners);
            bottom = _canvasRect.InverseTransformPoint(_hudCorners[1]).y - _canvasRect.rect.yMin + 8f;
        }
        float width = Mathf.Max(1f, _canvasRect.rect.width - 32f);
        float height = Mathf.Min(436f, Mathf.Max(1f, _canvasRect.rect.height - bottom - 16f));
        if (!Mathf.Approximately(_panelRect.rect.width, width))
            _lastFingerprint = int.MinValue;
        _panelRect.sizeDelta = new Vector2(width, height);
        _panelRect.anchoredPosition = new Vector2(0f, bottom);
        float cardSize = Mathf.Min(288f, Mathf.Max(1f, width - 32f));
        if (!Mathf.Approximately(cardSize, _cardSize))
            _lastFingerprint = int.MinValue;
        _cardSize = cardSize;
        SetRect(_minusButton.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero,
            new Vector2(0.5f, 0.5f), new Vector2(38f, 25f), new Vector2(44f, 36f));
        SetRect(_quantityText.rectTransform, Vector2.zero, Vector2.zero,
            new Vector2(0.5f, 0.5f), new Vector2(134f, 25f), new Vector2(144f, 36f));
        SetRect(_plusButton.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero,
            new Vector2(0.5f, 0.5f), new Vector2(230f, 25f), new Vector2(44f, 36f));
        SetRect(_selectedVolumeText.rectTransform, Vector2.zero, Vector2.zero,
            new Vector2(0f, 0.5f), new Vector2(268f, 25f),
            new Vector2(Mathf.Max(1f, width - 520f), 40f));
        SetRect(_dropButton.GetComponent<RectTransform>(), Vector2.right, Vector2.right,
            new Vector2(1f, 0.5f), new Vector2(-16f, 25f),
            new Vector2(Mathf.Clamp(width - 284f, 44f, 220f), 48f));
    }

    private void Refresh()
    {
        int used = _inventory.GetUsedCargo();
        int capacity = _inventory.GetMaxCargo();
        _headerText?.SetText("ТРЮМ (" + used + " / " + capacity + ")");
        var entries = _inventory.GetItems();
        var ship = _session.State?.Player?.GetActiveShip();
        var equipment = ship?.CargoEquipment;
        int fingerprint = unchecked(17 * 31 + capacity + used + (ship?.ShipId?.GetHashCode() ?? 0));
        foreach (RuntimeCargoEntryData entry in entries)
            if (entry != null) fingerprint = unchecked(fingerprint * 31 + (entry.ItemId?.GetHashCode() ?? 0) + entry.Quantity);
        if (equipment != null)
            foreach (var item in equipment)
                if (item != null) fingerprint = unchecked(fingerprint * 31 +
                    (item.InstanceId?.GetHashCode() ?? 0) + item.RolledStats.CargoSize);
        if (fingerprint == _lastFingerprint) return;
        _lastFingerprint = fingerprint;
        foreach (CargoCard card in _cards)
            if (card.Root != null) { card.Root.SetActive(false); DestroyUiObject(card.Root); }
        _cards.Clear();
        Transform empty = _content.Find("EmptyCargo");
        if (empty != null) { empty.gameObject.SetActive(false); DestroyUiObject(empty.gameObject); }

        var totals = new Dictionary<string, int>();
        foreach (RuntimeCargoEntryData entry in entries)
        {
            if (entry == null || entry.Quantity <= 0 || string.IsNullOrWhiteSpace(entry.ItemId)) continue;
            totals.TryGetValue(entry.ItemId, out int previous);
            totals[entry.ItemId] = (int)Math.Min(int.MaxValue, (long)previous + entry.Quantity);
        }
        foreach (var pair in totals)
        {
            ItemConfig config = _configs.GetItemConfigById(pair.Key);
            AddCard(new CargoCard { ItemId = pair.Key, Name = config != null ? config.DisplayName : "Товар",
                Count = pair.Value, Icon = config != null ? config.Icon : null,
                UnitVolume = config != null ? config.CargoSize : 0,
                Background = ResolveItemBackground(pair.Key), OriginLabel = ResolveItemOriginLabel(pair.Key) });
        }
        if (equipment != null)
            foreach (var item in equipment)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.InstanceId)) continue;
                WeaponConfig config = _configs.GetWeaponConfigById(item.WeaponConfigId);
                AddCard(new CargoCard { EquipmentId = item.InstanceId, Count = 1,
                    Name = config != null ? config.DisplayName : "Оборудование",
                    Icon = config != null ? config.Icon : null,
                    UnitVolume = item.RolledStats.CargoSize > 0 ? item.RolledStats.CargoSize
                        : config != null ? config.CargoSize : 0,
                    Background = ResolveEquipmentBackground(item), OriginLabel = ResolveEquipmentOriginLabel(item) });
            }
        _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            Mathf.Max(_viewport.rect.width, _cards.Count * (_cardSize + CardSpacing) - CardSpacing));
        _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            _cardSize);
        if (_cards.Count == 0)
        {
            TMP_Text label = NewText("EmptyCargo", _content, "Трюм пуст", 20f,
                new Vector2(0f, -48f), new Vector2(300f, 48f));
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        }

        CargoCard selected = FindSelectedCard();
        if (selected == null)
        {
            ClearSelection();
            if (_cards.Count > 0)
            {
                selected = _cards[0];
                _selectedItemId = selected.ItemId;
                _selectedEquipmentId = selected.EquipmentId;
                _selectedMax = selected.Count;
            }
        }
        else { _selectedMax = selected.Count; _quantity = Mathf.Clamp(_quantity, 1, _selectedMax); }
        UpdateSelection();
    }

    private void AddCard(CargoCard card)
    {
        int index = _cards.Count;
        Button button = NewButton("CargoCard_" + index, _content, "", Vector2.zero,
            new Vector2(_cardSize, _cardSize));
        Transform label = button.transform.Find("Label");
        if (label != null) label.gameObject.SetActive(false);
        card.Root = button.gameObject;
        SetRect(button.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(_cardSize * 0.5f + index * (_cardSize + CardSpacing),
                -_cardSize * 0.5f),
            new Vector2(_cardSize, _cardSize));
        GameObject header = NewRect("CardHeader", button.transform, Vector2.zero);
        SetRect(header.GetComponent<RectTransform>(), new Vector2(0f, 1f), Vector2.one,
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 44f));
        Image headerImage = header.AddComponent<Image>();
        headerImage.sprite = _panelSprite;
        headerImage.type = Image.Type.Sliced;
        headerImage.color = new Color(card.Background.r * 0.6f, card.Background.g * 0.6f,
            card.Background.b * 0.6f, 0.98f);
        headerImage.raycastTarget = false;
        var iconObject = NewRect("Icon", button.transform, new Vector2(84f, 84f));
        iconObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, _cardSize * 0.5f - 87f);
        Image icon = iconObject.AddComponent<Image>();
        icon.sprite = card.Icon;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = card.Icon != null;
        TMP_Text originText = NewCardText("Origin", button.transform, card.OriginLabel,
            new Vector2(0f, _cardSize * 0.5f - 22f),
            new Vector2(_cardSize - 16f, 34f));
        originText.alignment = TextAlignmentOptions.Center;
        originText.textWrappingMode = TextWrappingModes.NoWrap;
        TMP_Text volume = NewCardText("Volume", button.transform, ((long)card.UnitVolume * card.Count).ToString(),
            Vector2.zero, Vector2.zero);
        SetRect(volume.rectTransform, Vector2.one, Vector2.one, Vector2.one,
            new Vector2(-8f, -4f), new Vector2(88f, 36f));
        volume.textWrappingMode = TextWrappingModes.NoWrap;
        volume.alignment = TextAlignmentOptions.Right;
        NewCardText("Name", button.transform, card.Name, new Vector2(0f, -43f),
            new Vector2(_cardSize - 16f, 70f));
        NewCardText("Count", button.transform, "Кол-во " + card.Count,
            new Vector2(0f, -99f), new Vector2(_cardSize - 16f, 34f));
        var outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.Lerp(card.Background, Color.white, 0.55f);
        outline.effectDistance = new Vector2(2f, 2f);
        outline.enabled = false;
        button.onClick.AddListener(() =>
        {
            _selectedItemId = card.ItemId;
            _selectedEquipmentId = card.EquipmentId;
            _selectedMax = card.Count;
            _quantity = 1;
            UpdateSelection();
            AppLog.Info(DebugLogChannel.Loot, "[CARGO_CARD_SELECTED] Item=" + card.ItemId +
                " | Equipment=" + card.EquipmentId);
        });
        _cards.Add(card);
    }

    private CargoCard FindSelectedCard() => _cards.Find(card =>
        (_selectedItemId != null && card.ItemId == _selectedItemId) ||
        (_selectedEquipmentId != null && card.EquipmentId == _selectedEquipmentId));

    private void ChangeQuantity(int delta)
    {
        if (_selectedItemId == null || _selectedMax <= 0) return;
        _quantity = Mathf.Clamp(_quantity + delta, 1, _selectedMax);
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        CargoCard selected = FindSelectedCard();
        bool hasSelection = selected != null;
        _dropButton.interactable = hasSelection;
        _minusButton.interactable = hasSelection && _selectedItemId != null && _quantity > 1;
        _plusButton.interactable = hasSelection && _selectedItemId != null && _quantity < _selectedMax;
        _quantityText.SetText(hasSelection ? _quantity + " / " + _selectedMax : "—");
        _selectedVolumeText.SetText("места в трюме " +
            (hasSelection ? (long)selected.UnitVolume * _quantity : 0L) + " ед");
        foreach (CargoCard card in _cards)
        {
            if (card.Root == null) continue;
            bool active = card == selected;
            card.Root.GetComponent<Image>().color = active
                ? Color.Lerp(card.Background, Color.white, 0.15f) : card.Background;
            card.Root.GetComponent<Outline>().enabled = active;
        }
    }

    private void DropSelected()
    {
        if (_loot == null || FindSelectedCard() == null) return;
        string error;
        bool dropped = _selectedEquipmentId != null
            ? _loot.TryDropPlayerEquipment(_selectedEquipmentId, out error)
            : _loot.TryDropPlayerCargo(_selectedItemId, _quantity, out error);
        if (!dropped)
        {
            var transition = FindFirstObjectByType<SystemTransitionController2>(FindObjectsInactive.Include);
            transition?.ShowMessage(error);
            AppLog.Warning(DebugLogChannel.Loot, "[CARGO_DROP_REJECTED] Reason=" + error);
            return;
        }
        AppLog.Info(DebugLogChannel.Loot, "[CARGO_DROPPED] Item=" + _selectedItemId +
            " | Equipment=" + _selectedEquipmentId + " | Quantity=" + _quantity);
        ClearSelection();
        _lastFingerprint = int.MinValue;
        Refresh();
    }

    private void SetCombatPanelsSuppressed(bool suppressed)
    {
        if (_weaponHuds != null)
            foreach (var hud in _weaponHuds) if (hud != null) hud.SetCargoPanelSuppressed(suppressed);
        if (_threatHuds != null)
            foreach (var hud in _threatHuds) if (hud != null) hud.SetCargoPanelSuppressed(suppressed);
    }

    private void ClearSelection()
    {
        _selectedItemId = null;
        _selectedEquipmentId = null;
        _selectedMax = 0;
        _quantity = 1;
    }

    private void AddHorizontalScrollbar(ScrollRect scroll)
    {
        GameObject track = NewRect("HorizontalScrollbar", _viewport, Vector2.zero);
        RectTransform rect = track.GetComponent<RectTransform>();
        SetRect(rect, Vector2.zero, Vector2.right, new Vector2(0.5f, 0f),
            new Vector2(0f, 2f), new Vector2(0f, 12f));
        Image background = track.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.35f);
        GameObject sliding = NewRect("SlidingArea", track.transform, Vector2.zero);
        RectTransform slidingRect = sliding.GetComponent<RectTransform>();
        slidingRect.anchorMin = Vector2.zero;
        slidingRect.anchorMax = Vector2.one;
        slidingRect.offsetMin = new Vector2(2f, 2f);
        slidingRect.offsetMax = new Vector2(-2f, -2f);
        GameObject handle = NewRect("Handle", sliding.transform, Vector2.zero);
        var handleImage = handle.AddComponent<Image>();
        handleImage.sprite = _panelSprite;
        handleImage.type = Image.Type.Sliced;
        handleImage.color = Color.Lerp(_cardColor, Color.white, 0.35f);
        var scrollbar = track.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.LeftToRight;
        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handleImage;
        scroll.horizontalScrollbar = scrollbar;
        scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    private TMP_Text NewCardText(string name, Transform parent, string value,
        Vector2 position, Vector2 size)
    {
        return NewText(name, parent, value, 26f, position, size);
    }

    private void ApplyListTextStyle(TMP_Text text)
    {
        if (_cardTextStyle != null)
        {
            text.font = _cardTextStyle.font;
            text.fontSharedMaterial = _cardTextStyle.fontSharedMaterial;
            text.fontSize = _cardTextStyle.fontSize;
            text.fontStyle = _cardTextStyle.fontStyle;
            text.fontWeight = _cardTextStyle.fontWeight;
            text.color = _cardTextStyle.color;
            text.characterSpacing = _cardTextStyle.characterSpacing;
            text.wordSpacing = _cardTextStyle.wordSpacing;
            text.lineSpacing = _cardTextStyle.lineSpacing;
        }
    }

    private static string ResolveItemOriginLabel(string itemId)
    {
        switch (itemId)
        {
            case "item_astralite_ai_01": return "ИИ";
            case "item_astralite_ancients_01": return "Древние";
            case "item_astralite_infected_01": return "Заражённые";
            default: return "Товар";
        }
    }

    private string ResolveEquipmentOriginLabel(SystemNpcWeaponRuntimeState equipment)
    {
        if (equipment.HasLootOrigin)
        {
            if (equipment.LootOriginNpcType != SystemNpcType.Enemy) return "Союзники";
            EnemyConfig enemy = _configs.GetEnemyConfigById(equipment.LootOriginNpcConfigId);
            if (enemy != null)
                return enemy.LootSpecies == EnemyFaction2A.Ancients ? "Древние"
                    : enemy.LootSpecies == EnemyFaction2A.Infected ? "Заражённые" : "ИИ";
            string source = equipment.LootOriginNpcConfigId ?? string.Empty;
            if (source.StartsWith("enemy_ancients_", StringComparison.Ordinal)) return "Древние";
            if (source.StartsWith("enemy_infected_", StringComparison.Ordinal)) return "Заражённые";
            if (source.StartsWith("enemy_ai_", StringComparison.Ordinal)) return "ИИ";
            return "Враги";
        }
        string id = equipment.WeaponConfigId ?? string.Empty;
        if (id.StartsWith("weapon_ai_", StringComparison.Ordinal)) return "ИИ";
        if (id.StartsWith("weapon_ancient_", StringComparison.Ordinal)) return "Древние";
        if (id.StartsWith("weapon_infected_", StringComparison.Ordinal)) return "Заражённые";
        return "Союзники";
    }

    private Color ResolveItemBackground(string itemId)
    {
        switch (itemId)
        {
            case "item_astralite_ai_01": return MutedBackground(ResolveEnemyTint(EnemyFaction2A.AI));
            case "item_astralite_ancients_01": return MutedBackground(ResolveEnemyTint(EnemyFaction2A.Ancients));
            case "item_astralite_infected_01": return MutedBackground(ResolveEnemyTint(EnemyFaction2A.Infected));
            default: return MutedBackground(new Color(0.25f, 0.65f, 0.38f));
        }
    }

    private Color ResolveEquipmentBackground(SystemNpcWeaponRuntimeState equipment)
    {
        Color npcBlue = new Color(0.25f, 0.5f, 0.9f);
        if (equipment.HasLootOrigin)
        {
            if (equipment.LootOriginNpcType != SystemNpcType.Enemy)
                return MutedBackground(npcBlue);
            EnemyConfig enemy = _configs.GetEnemyConfigById(equipment.LootOriginNpcConfigId);
            Color tint = enemy != null ? ResolveEnemyTint(enemy.LootSpecies)
                : _configs.CombatFxVisualConfig != null
                    ? _configs.CombatFxVisualConfig.ResolveEnemyColor(equipment.LootOriginNpcConfigId)
                    : new Color(1f, 0.25f, 0.18f);
            return MutedBackground(tint);
        }
        // Older cargo lacks provenance: use its weapon family when available.
        string id = equipment.WeaponConfigId ?? string.Empty;
        if (id.StartsWith("weapon_ai_", StringComparison.Ordinal))
            return MutedBackground(ResolveEnemyTint(EnemyFaction2A.AI));
        if (id.StartsWith("weapon_ancient_", StringComparison.Ordinal))
            return MutedBackground(ResolveEnemyTint(EnemyFaction2A.Ancients));
        if (id.StartsWith("weapon_infected_", StringComparison.Ordinal))
            return MutedBackground(ResolveEnemyTint(EnemyFaction2A.Infected));
        return MutedBackground(npcBlue);
    }

    private Color ResolveEnemyTint(EnemyFaction2A species)
    {
        CombatFxVisualConfig colors = _configs.CombatFxVisualConfig;
        switch (species)
        {
            case EnemyFaction2A.Ancients:
                return colors != null ? colors.AncientsEnemyColor : new Color(1f, 0.72f, 0.18f);
            case EnemyFaction2A.Infected:
                return colors != null ? colors.InfectedEnemyColor : new Color(0.62f, 1f, 0.22f);
            default:
                return colors != null ? colors.AiEnemyColor : new Color(1f, 0.22f, 0.16f);
        }
    }

    private static Color MutedBackground(Color tint)
    {
        Color result = Color.Lerp(new Color(0.07f, 0.09f, 0.12f), tint, 0.22f);
        result.a = 0.94f;
        return result;
    }

    private static void DestroyUiObject(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max,
        Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static GameObject NewRect(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = size;
        return go;
    }

    private TMP_Text NewText(string name, Transform parent, string value,
        float fontSize, Vector2 position, Vector2 size)
    {
        GameObject go = NewRect(name, parent, size);
        go.GetComponent<RectTransform>().anchoredPosition = position;
        var label = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) label.font = _font;
        if (_fontMaterial != null) label.fontSharedMaterial = _fontMaterial;
        label.fontSize = fontSize;
        label.color = _textColor;
        ApplyListTextStyle(label);
        label.enableAutoSizing = false;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        label.SetText(value);
        return label;
    }

    private Button NewButton(string name, Transform parent, string value, Vector2 position, Vector2 size)
    {
        GameObject go = NewRect(name, parent, size);
        go.GetComponent<RectTransform>().anchoredPosition = position;
        var image = go.AddComponent<Image>();
        image.sprite = _panelSprite;
        image.type = Image.Type.Sliced;
        image.color = _cardColor;
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.45f);
        button.colors = colors;
        NewText("Label", go.transform, value, 17f, Vector2.zero, size);
        return button;
    }
}
