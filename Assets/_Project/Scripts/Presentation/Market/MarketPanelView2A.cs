using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared, pooled market layout for landed planets and stations. No data work in Update.</summary>
public sealed class MarketPanelView2A : MonoBehaviour
{
    private RectTransform _screen, _work, _left, _right, _groups, _categoryContent, _leftContent, _rightContent;
    private ScrollRect _categories, _marketScroll, _ownedScroll;
    private GridLayoutGroup _grid;
    private TMP_Text _template, _buyInfo, _sellInfo, _ownedEmpty, _marketEmpty, _storageLabel, _transferLabel;
    private readonly List<Button> _kindButtons = new List<Button>(), _categoryButtons = new List<Button>();
    private readonly List<TMP_Text> _categoryLabels = new List<TMP_Text>();
    private readonly List<string> _categoryKeys = new List<string>();
    private readonly List<MarketCardView2A> _marketCards = new List<MarketCardView2A>(), _ownedCards = new List<MarketCardView2A>();
    private Button _cargo, _storage, _buy, _sell, _transfer, _minus, _plus, _maximum, _saleMinus, _salePlus, _saleMaximum, _minimum, _saleMinimum;
    private RectTransform _quantityPicker, _purchaseRow, _saleRow, _salePicker;
    private TMP_Text _quantityLabel, _buyLabel, _saleQuantityLabel, _sellLabel;
    private Action<string> _category, _leftSelect, _rightSelect;
    private Vector2 _lastSize;
    private Sprite _lastHero;
    private bool _equipmentVisible;
    private RectTransform _infoWindow, _infoHeading;
    private Image _infoOverlay, _infoPicture, _infoDurabilityTrack, _infoDurabilityFill;
    private ScrollRect _infoScroll;
    private TMP_Text _infoTitle, _infoText, _infoDurability, _infoTradeLabel;
    private Button _infoClose, _infoTrade;
    private string _infoKey;
    private bool _infoOwned;
    public Image Header { get; private set; }
    public bool IsReady => _screen != null && Header != null && _buy != null && _sell != null;
    public int MarketCardPoolCount => _marketCards.Count;
    public int OwnedCardPoolCount => _ownedCards.Count;

    public void Build(TMP_Text template, Action<MarketKindFilter2A> kind, Action<string> category,
        Action<bool> storage, Action<string> leftSelect, Action<string> rightSelect, Action buy, Action sell, Action transfer, Action<int> quantityChanged = null, Action purchaseMax = null, Action<int> saleQuantityChanged = null, Action saleMax = null, Action purchaseMin = null, Action saleMin = null)
    {
        if (_screen != null) return;
        _template = template; _category = category; _leftSelect = leftSelect; _rightSelect = rightSelect;
        foreach (Transform child in transform) child.gameObject.SetActive(false);
        if (transform.parent is RectTransform) ResearchPanelUi2A.Stretch((RectTransform)transform);
        transform.SetAsFirstSibling();
        _screen = ResearchPanelUi2A.Rect(transform, "SharedMarketLayout"); ResearchPanelUi2A.Stretch(_screen);
        Header = ResearchPanelUi2A.Picture(_screen, "MarketIllustration", Color.white); Header.preserveAspect = true;
        _work = ResearchPanelUi2A.Picture(_screen, "MarketWorkspace", MissionPanelStyle2A.Workspace).rectTransform;
        _work.GetComponent<Image>().raycastTarget = true;
        _left = Panel(_work, "MarketInventory"); _right = Panel(_work, "PersonalInventory");
        string[] labels = { "Товары", "Корпуса", "Оружие", "Модули", "Все" };
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i; var button = ResearchPanelUi2A.Button(_left, template, "Kind_" + i, labels[i], out _);
            button.onClick.AddListener(() => kind((MarketKindFilter2A)index)); _kindButtons.Add(button);
        }
        _categories = ResearchPanelUi2A.Scroll(_left, "CategoryFilters", true, out _categoryContent);
        _groups = (RectTransform)_categories.transform;
        _marketScroll = GridScroll(_left, out _leftContent);
        _grid = _leftContent.gameObject.AddComponent<GridLayoutGroup>(); _grid.spacing = new Vector2(14, 14);
        _grid.padding = new RectOffset(4, 4, 4, 12); _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _ownedScroll = ResearchPanelUi2A.Scroll(_right, "OwnedItems", false, out _rightContent);
        _cargo = ResearchPanelUi2A.Button(_right, template, "CargoTab", "Трюм", out _);
        _storage = ResearchPanelUi2A.Button(_right, template, "StorageTab", "Планета", out _storageLabel);
        _cargo.onClick.AddListener(() => storage(false)); _storage.onClick.AddListener(() => storage(true));
        _purchaseRow = ResearchPanelUi2A.Rect(_left, "PurchaseRow");
        _saleRow = ResearchPanelUi2A.Rect(_right, "SaleRow");
        _buyInfo = ResearchPanelUi2A.Text(_purchaseRow, template, "PurchaseSelection", "Выбери товар на рынке");
        _sellInfo = ResearchPanelUi2A.Text(_saleRow, template, "SaleSelection", "Выбери предмет");
        _quantityPicker = CreateQuantityPicker(_purchaseRow, "PurchaseQuantity", quantityChanged, purchaseMax, purchaseMin,
            out _minus, out _plus, out _maximum, out _minimum, out _quantityLabel);
        _salePicker = CreateQuantityPicker(_saleRow, "SaleQuantity", saleQuantityChanged, saleMax, saleMin,
            out _saleMinus, out _salePlus, out _saleMaximum, out _saleMinimum, out _saleQuantityLabel);
        _buy = ResearchPanelUi2A.Button(_left, template, "Buy", "Купить за 0 кр", out _buyLabel); _buy.onClick.AddListener(() => buy());
        _sell = ResearchPanelUi2A.Button(_right, template, "Sell", "Продать за 0 кр", out _sellLabel); _sell.onClick.AddListener(() => sell());
        _transfer = ResearchPanelUi2A.Button(_right, template, "Transfer", "На склад", out _transferLabel); _transfer.onClick.AddListener(() => transfer());
        _marketEmpty = ResearchPanelUi2A.Text(_left, template, "MarketEmpty", "Нет предложений этого типа");
        _ownedEmpty = ResearchPanelUi2A.Text(_right, template, "OwnedEmpty", "Здесь пока пусто");
        foreach (var text in new[] { _buyInfo, _sellInfo, _marketEmpty, _ownedEmpty })
        { text.fontSize = 24; text.fontSizeMax = 24; text.fontSizeMin = 18; }
        BuildInfoWindow();
        Layout();
    }
    private static ScrollRect GridScroll(Transform parent, out RectTransform content)
    {
        var host = ResearchPanelUi2A.Rect(parent, "AvailableItems");
        host.gameObject.AddComponent<Image>().color = Color.clear;
        var scroll = host.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        var viewport = ResearchPanelUi2A.Rect(host, "Viewport"); ResearchPanelUi2A.Stretch(viewport);
        viewport.gameObject.AddComponent<Image>().color = Color.clear; viewport.gameObject.AddComponent<RectMask2D>();
        content = ResearchPanelUi2A.Rect(viewport, "Content"); content.anchorMin = Vector2.up; content.anchorMax = Vector2.one;
        content.pivot = Vector2.up; content.offsetMin = content.offsetMax = Vector2.zero;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = content; return scroll;
    }
    private static RectTransform Panel(Transform parent, string name)
    {
        var image = ResearchPanelUi2A.Picture(parent, name, MissionPanelStyle2A.CardBackground);
        MissionPanelStyle2A.Frame(image, MissionPanelStyle2A.CardAccent); return image.rectTransform;
    }
    public void ResetCategoryScroll()
    { if (_categories != null) { _categories.StopMovement(); _categories.horizontalNormalizedPosition = 0; } }
    public void ResetScrolls()
    {
        if (_marketScroll != null) { _marketScroll.StopMovement(); _marketScroll.verticalNormalizedPosition = 1; }
        if (_ownedScroll != null) { _ownedScroll.StopMovement(); _ownedScroll.verticalNormalizedPosition = 1; }
    }
    public void SetHeader(Sprite sprite)
    { Header.sprite = sprite; Header.enabled = sprite != null; Layout(); }
    public void SetFilters(MarketKindFilter2A kind, IReadOnlyList<KeyValuePair<string, string>> categories, string selected, bool warehouse, bool station)
    {
        for (int i = 0; i < _kindButtons.Count; i++) Highlight(_kindButtons[i], i == (int)kind);
        Highlight(_cargo, !warehouse); Highlight(_storage, warehouse); _storageLabel.SetText(station ? "Станция" : "Планета");
        _transferLabel.SetText(warehouse ? "В трюм" : "На склад");
        while (_categoryButtons.Count < categories.Count)
        {
            int index = _categoryButtons.Count;
            var button = ResearchPanelUi2A.Button(_categoryContent, _template, "Category_" + index, "", out var label);
            var size = button.gameObject.AddComponent<LayoutElement>(); size.minHeight = 64; size.preferredHeight = 64;
            button.onClick.AddListener(() => { if (index < _categoryKeys.Count) _category(_categoryKeys[index]); });
            _categoryButtons.Add(button); _categoryLabels.Add(label);
        }
        _categoryKeys.Clear();
        for (int i = 0; i < _categoryButtons.Count; i++)
        {
            bool visible = i < categories.Count; _categoryButtons[i].gameObject.SetActive(visible); if (!visible) continue;
            _categoryKeys.Add(categories[i].Key); _categoryLabels[i].SetText(categories[i].Value);
            _categoryButtons[i].GetComponent<LayoutElement>().preferredWidth = Mathf.Max(170, categories[i].Value.Length * 15 + 36);
            Highlight(_categoryButtons[i], categories[i].Key == selected);
        }
    }
    private static void Highlight(Button button, bool selected)
    {
        button.GetComponent<Image>().color = selected ? MissionPanelStyle2A.ActionBackground : MissionPanelStyle2A.CardBackground;
        button.GetComponent<Outline>().effectColor = selected ? Color.white : MissionPanelStyle2A.CardAccent;
    }
    public void SetCards(IReadOnlyList<MarketCardData2A> market, IReadOnlyList<MarketCardData2A> owned, string selectedLeft, string selectedRight)
    {
        _equipmentVisible = false;
        foreach (var item in market) if (item.Equipment || item.MaximumDurability > 0) { _equipmentVisible = true; break; }
        Fill(_marketCards, _leftContent, market, selectedLeft, _leftSelect);
        Fill(_ownedCards, _rightContent, owned, selectedRight, _rightSelect);
        if (_infoKey != null)
        {
            var item = FindCard(_infoOwned ? owned : market, _infoKey);
            if (item == null || _infoKey != (_infoOwned ? selectedRight : selectedLeft)) CloseInfo();
            else BindInfo(item);
        }
        _marketEmpty.gameObject.SetActive(market.Count == 0); _ownedEmpty.gameObject.SetActive(owned.Count == 0);
        Layout();
    }
    private void Fill(List<MarketCardView2A> pool, RectTransform parent, IReadOnlyList<MarketCardData2A> items, string selected, Action<string> select)
    {
        while (pool.Count < items.Count)
        {
            var rect = ResearchPanelUi2A.Rect(parent, "MarketCard_" + pool.Count);
            var view = rect.gameObject.AddComponent<MarketCardView2A>(); view.Build(_template, select, item => OpenInfo(item, pool == _ownedCards)); pool.Add(view);
        }
        for (int i = 0; i < pool.Count; i++)
        { pool[i].gameObject.SetActive(i < items.Count); if (i < items.Count) pool[i].Bind(items[i], items[i].Key == selected); }
    }
    private void BuildInfoWindow()
    {
        var rootCanvas = _screen.GetComponentInParent<Canvas>()?.rootCanvas;
        var host = rootCanvas != null ? (RectTransform)rootCanvas.transform : _screen;
        _infoOverlay = ResearchPanelUi2A.Picture(host, "EquipmentModal", new Color(0, 0, 0, .75f));
        ResearchPanelUi2A.Stretch(_infoOverlay.rectTransform);
        _infoOverlay.raycastTarget = true;
        var canvas = _infoOverlay.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        if (rootCanvas != null) canvas.sortingLayerID = rootCanvas.sortingLayerID;
        canvas.sortingOrder = 100;
        _infoOverlay.gameObject.AddComponent<GraphicRaycaster>();

        var backdrop = ResearchPanelUi2A.Picture(_infoOverlay.transform, "CloseBackdrop", Color.clear);
        ResearchPanelUi2A.Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = true;
        var closeBackdrop = backdrop.gameObject.AddComponent<Button>();
        closeBackdrop.transition = Selectable.Transition.None;
        closeBackdrop.navigation = new Navigation { mode = Navigation.Mode.None };
        closeBackdrop.onClick.AddListener(CloseInfo);

        _infoWindow = Panel(_infoOverlay.transform, "EquipmentWindow");
        _infoWindow.GetComponent<Image>().raycastTarget = true;
        _infoHeading = Panel(_infoWindow, "Heading");
        _infoTitle = ResearchPanelUi2A.Text(_infoHeading, _template, "Title", string.Empty);
        _infoTitle.alignment = TextAlignmentOptions.Center;
        _infoTitle.textWrappingMode = TextWrappingModes.Normal;
        _infoClose = ResearchPanelUi2A.Button(_infoWindow, _template, "Close", "×", out _);
        _infoClose.navigation = new Navigation { mode = Navigation.Mode.None };
        _infoClose.onClick.AddListener(CloseInfo);

        _infoScroll = ResearchPanelUi2A.Scroll(_infoWindow, "EquipmentDetails", false, out var content);
        content.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(16, 16, 12, 12);
        _infoPicture = ResearchPanelUi2A.Picture(content, "Image", Color.white);
        _infoPicture.preserveAspect = true;
        _infoPicture.gameObject.AddComponent<LayoutElement>().preferredHeight = 280;
        _infoText = ResearchPanelUi2A.Text(content, _template, "EquipmentInfo", string.Empty);
        _infoText.gameObject.AddComponent<LayoutElement>();
        _infoText.alignment = TextAlignmentOptions.Center;
        _infoText.enableAutoSizing = false;
        _infoText.textWrappingMode = TextWrappingModes.Normal;
        _infoText.overflowMode = TextOverflowModes.Overflow;

        _infoDurabilityTrack = ResearchPanelUi2A.Picture(_infoWindow, "DurabilityTrack", MissionPanelStyle2A.Workspace);
        MissionPanelStyle2A.Frame(_infoDurabilityTrack, MissionPanelStyle2A.CardAccent);
        _infoDurabilityFill = ResearchPanelUi2A.Picture(_infoDurabilityTrack.transform, "Fill", MissionPanelStyle2A.ActionBackground);
        _infoDurability = ResearchPanelUi2A.Text(_infoDurabilityTrack.transform, _template, "Durability", string.Empty);
        ResearchPanelUi2A.Stretch(_infoDurability.rectTransform);
        _infoDurability.alignment = TextAlignmentOptions.Center;
        _infoTrade = ResearchPanelUi2A.Button(_infoWindow, _template, "Trade", string.Empty, out _infoTradeLabel);
        _infoTrade.onClick.AddListener(() =>
        {
            var action = _infoOwned ? _sell : _buy;
            if (_infoKey != null && _infoTrade.interactable && action.interactable) action.onClick.Invoke();
        });
        CloseInfo();
    }

    private static MarketCardData2A FindCard(IReadOnlyList<MarketCardData2A> cards, string key)
    {
        foreach (var card in cards)
            if (card.Key == key) return card;
        return null;
    }

    private void OpenInfo(MarketCardData2A item, bool owned)
    {
        _infoKey = item.Key;
        _infoOwned = owned;
        BindInfo(item);
        UpdateInfoTrade();
        _infoOverlay.gameObject.SetActive(true);
        _infoOverlay.transform.SetAsLastSibling();
        LayoutInfoWindow();
        _infoScroll.StopMovement();
        _infoScroll.verticalNormalizedPosition = 1;
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(_infoClose.gameObject);
    }

    private void BindInfo(MarketCardData2A item)
    {
        _infoTitle.SetText(item.Title);
        _infoPicture.sprite = item.Icon;
        _infoPicture.gameObject.SetActive(item.Icon != null);
        _infoText.SetText(item.Info);
        _infoText.gameObject.SetActive(!string.IsNullOrWhiteSpace(item.Info));
        bool showDurability = item.MaximumDurability > 0;
        _infoDurabilityTrack.gameObject.SetActive(showDurability);
        _infoDurability.SetText($"Прочность: {item.CurrentDurability} / {item.MaximumDurability}");
        var fill = _infoDurabilityFill.rectTransform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(showDurability ? Mathf.Clamp01((float)item.CurrentDurability / item.MaximumDurability) : 0, 1);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
    }

    private void CloseInfo()
    {
        if (_infoOverlay == null) return;
        _infoKey = null;
        _infoOverlay.gameObject.SetActive(false);
        var events = UnityEngine.EventSystems.EventSystem.current;
        if (events != null && events.currentSelectedGameObject != null &&
            events.currentSelectedGameObject.transform.IsChildOf(_infoOverlay.transform))
            events.SetSelectedGameObject(null);
    }

    private void OnDisable() => CloseInfo();
    private void OnDestroy()
    {
        if (_infoOverlay != null && _infoOverlay.transform.parent != _screen)
            Destroy(_infoOverlay.gameObject);
    }

    private void UpdateInfoTrade()
    {
        var action = _infoOwned ? _sell : _buy;
        _infoTradeLabel.SetText((_infoOwned ? _sellLabel : _buyLabel).text);
        SetButton(_infoTrade, action.interactable);
    }

    private void LayoutInfoWindow()
    {
        const float header = 64;
        const float inset = 16;
        const float barHeight = 46;
        const float buttonHeight = 64;
        var overlay = _infoOverlay.rectTransform.rect;
        float width = overlay.width * .94f;
        float maxHeight = overlay.height * .9f;
        if (width <= 0 || maxHeight <= 0) return;
        float barSpace = _infoDurabilityTrack.gameObject.activeSelf ? barHeight + 12 : 0;
        float footer = inset * 2 + buttonHeight + barSpace;
        var content = _infoScroll.content;
        var textHeight = _infoText.GetPreferredValues(_infoText.text, Mathf.Max(1, width - 2 * inset), 0).y;
        _infoText.GetComponent<LayoutElement>().preferredHeight = Mathf.Ceil(textHeight) + 8;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        float body = Mathf.Min(LayoutUtility.GetPreferredHeight(content), Mathf.Max(0, maxHeight - header - footer));
        float height = header + body + footer;
        ResearchPanelUi2A.Place(_infoWindow, (overlay.width - width) / 2, (overlay.height - height) / 2, width, height);
        ResearchPanelUi2A.Place(_infoHeading, 0, 0, width, header);
        ResearchPanelUi2A.Place(_infoTitle.rectTransform, 74, 0, width - 148, header);
        ResearchPanelUi2A.Place((RectTransform)_infoClose.transform, width - 68, 10, 56, 48);
        ResearchPanelUi2A.Place((RectTransform)_infoScroll.transform, 0, header, width, body);
        ResearchPanelUi2A.Place(_infoDurabilityTrack.rectTransform, inset, header + body, width - 2 * inset, barHeight);
        ResearchPanelUi2A.Place((RectTransform)_infoTrade.transform, inset, header + body + barSpace + inset,
            width - 2 * inset, buttonHeight);
    }

    public void SetActions(string buyInfo, string sellInfo, bool canBuy, bool canSell, bool canTransfer)
    {
        _buyInfo.SetText(buyInfo); _sellInfo.SetText(sellInfo);
        SetButton(_buy, canBuy); SetButton(_sell, canSell); SetButton(_transfer, canTransfer);
        if (_infoKey != null) UpdateInfoTrade();
    }
    private RectTransform CreateQuantityPicker(Transform parent, string name, Action<int> change, Action maximum, Action minimum,
        out Button minus, out Button plus, out Button max, out Button min, out TMP_Text label)
    {
        var root = ResearchPanelUi2A.Rect(parent, name);
        minus = ResearchPanelUi2A.Button(root, _template, "Minus", "−", out var minusLabel);
        plus = ResearchPanelUi2A.Button(root, _template, "Plus", "+", out var plusLabel);
        max = ResearchPanelUi2A.Button(root, _template, "Max", "Макс", out var maxLabel);
        min = ResearchPanelUi2A.Button(root, _template, "Min", "Мин", out var minLabel);
        foreach (var text in new[] { minusLabel, plusLabel, maxLabel, minLabel })
        { text.fontSizeMax = 22; text.fontSizeMin = 16; text.margin = Vector4.zero; }
        label = ResearchPanelUi2A.Text(root, _template, "Quantity", "0");
        label.alignment = TextAlignmentOptions.Center;
        label.fontSizeMax = 24; label.fontSizeMin = 16;
        minus.onClick.AddListener(() => change?.Invoke(-1));
        plus.onClick.AddListener(() => change?.Invoke(1));
        max.onClick.AddListener(() => maximum?.Invoke());
        min.onClick.AddListener(() => minimum?.Invoke());
        root.gameObject.SetActive(false); return root;
    }
    private static void SetQuantity(RectTransform picker, TMP_Text label, Button minus, Button plus, Button max, Button min,
        bool goods, int quantity, int maximum)
    {
        picker.gameObject.SetActive(goods); label.SetText(quantity.ToString());
        SetButton(minus, goods && quantity > 1);
        SetButton(plus, goods && quantity < maximum);
        SetButton(max, goods && maximum > 0 && quantity < maximum);
        SetButton(min, goods && maximum > 0 && quantity > 1);
    }
    public void SetPurchase(bool goods, int quantity, int maximum, long totalPrice)
    {
        SetQuantity(_quantityPicker, _quantityLabel, _minus, _plus, _maximum, _minimum, goods, quantity, maximum);
        _buyLabel.SetText("Купить за " + totalPrice.ToString("N0") + " кр"); Layout();
    }
    public void SetSale(bool goods, int quantity, int maximum, long totalPrice)
    {
        SetQuantity(_salePicker, _saleQuantityLabel, _saleMinus, _salePlus, _saleMaximum, _saleMinimum, goods, quantity, maximum);
        _sellLabel.SetText("Продать за " + totalPrice.ToString("N0") + " кр"); Layout();
    }
    private static void LayoutQuantity(RectTransform row, RectTransform picker, TMP_Text info, float width, float y)
    {
        bool visible = picker.gameObject.activeSelf;
        float rowWidth = Mathf.Min(288, Mathf.Max(0, width - 28));
        ResearchPanelUi2A.Place(row, (width - rowWidth) / 2, y, rowWidth, 40);
        float scale = Mathf.Min(1, rowWidth / 288);
        ResearchPanelUi2A.Place(picker, 0, 0, rowWidth, 40);
        ResearchPanelUi2A.Place((RectTransform)picker.Find("Min"), 0, 2, 64 * scale, 36);
        ResearchPanelUi2A.Place((RectTransform)picker.Find("Minus"), 72 * scale, 2, 36 * scale, 36);
        ResearchPanelUi2A.Place((RectTransform)picker.Find("Quantity"), 116 * scale, 0, 56 * scale, 40);
        ResearchPanelUi2A.Place((RectTransform)picker.Find("Plus"), 180 * scale, 2, 36 * scale, 36);
        ResearchPanelUi2A.Place((RectTransform)picker.Find("Max"), 224 * scale, 2, 64 * scale, 36);
        info.gameObject.SetActive(!visible);
        info.alignment = TextAlignmentOptions.Center;
        ResearchPanelUi2A.Place(info.rectTransform, 0, 0, rowWidth, 40);
    }
    private static void SetButton(Button button, bool enabled)
    { button.interactable = enabled; MissionPanelStyle2A.Action(button, enabled); }
    private void LateUpdate()
    { if (IsReady && (_lastSize != ((RectTransform)transform).rect.size || _lastHero != Header.sprite)) Layout(); }
    private void Layout()
    {
        if (!IsReady) return;
        _lastSize = ((RectTransform)transform).rect.size; _lastHero = Header.sprite;
        float width = _lastSize.x, height = _lastSize.y;
        if (width <= 0 || height <= 0) return;
        float hero = Header.sprite != null && Header.sprite.rect.width > 0 ? width * Header.sprite.rect.height / Header.sprite.rect.width : width * 4f / 9f;
        hero = Mathf.Max(300f, hero);
        ResearchPanelUi2A.Place(Header.rectTransform, 0, 194, width, hero);
        ResearchPanelUi2A.Place(_work, 0, 194 + hero, width, Mathf.Max(0, height - 194 - hero - 142));
        float workHeight = Mathf.Max(0, height - 194 - hero - 142);
        float contentWidth = Mathf.Max(0, width - 54), left = contentWidth * .677f, right = contentWidth - left;
        float panelHeight = Mathf.Max(0, workHeight - 36);
        ResearchPanelUi2A.Place(_left, 18, 18, left, panelHeight);
        ResearchPanelUi2A.Place(_right, 36 + left, 18, right, panelHeight);
        float buttonWidth = Mathf.Max(0, (left - 28 - 12 * (_kindButtons.Count - 1)) / _kindButtons.Count);
        for (int i = 0; i < _kindButtons.Count; i++) ResearchPanelUi2A.Place((RectTransform)_kindButtons[i].transform, 14 + i * (buttonWidth + 12), 14, buttonWidth, 64);
        ResearchPanelUi2A.Place(_groups, 10, 92, left - 20, 72);
        // Horizontal fitting controls width only; retain a real button-height content rectangle.
        _categoryContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 72f);
        ResearchPanelUi2A.Place((RectTransform)_marketScroll.transform, 10, 176, left - 20, Mathf.Max(0, panelHeight - 176 - 144));
        ResearchPanelUi2A.Place((RectTransform)_ownedScroll.transform, 10, 176, right - 20, Mathf.Max(0, panelHeight - 176 - 188));
        ResearchPanelUi2A.Place((RectTransform)_cargo.transform, 14, 14, right - 28, 64);
        ResearchPanelUi2A.Place((RectTransform)_storage.transform, 14, 94, right - 28, 64);
        LayoutQuantity(_purchaseRow, _quantityPicker, _buyInfo, left, panelHeight - 132);
        LayoutQuantity(_saleRow, _salePicker, _sellInfo, right, panelHeight - 184);
        ResearchPanelUi2A.Place((RectTransform)_buy.transform, 14, panelHeight - 82, left - 28, 68);
        ResearchPanelUi2A.Place((RectTransform)_transfer.transform, 14, panelHeight - 140, right - 28, 48);
        ResearchPanelUi2A.Place((RectTransform)_sell.transform, 14, panelHeight - 82, right - 28, 68);
        ResearchPanelUi2A.Place(_marketEmpty.rectTransform, 20, 188, left - 40, 80);
        ResearchPanelUi2A.Place(_ownedEmpty.rectTransform, 20, 188, right - 40, 80);
        if (_infoOverlay.gameObject.activeSelf) LayoutInfoWindow();
        _grid.constraintCount = left >= 820 ? 3 : 2;
        _grid.cellSize = new Vector2(Mathf.Max(80, (left - 28 - 14 * (_grid.constraintCount - 1)) / _grid.constraintCount), _equipmentVisible ? 300 : 236);
    }
}
