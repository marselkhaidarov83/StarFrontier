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
        _marketEmpty.gameObject.SetActive(market.Count == 0); _ownedEmpty.gameObject.SetActive(owned.Count == 0);
        Layout();
    }
    private void Fill(List<MarketCardView2A> pool, RectTransform parent, IReadOnlyList<MarketCardData2A> items, string selected, Action<string> select)
    {
        while (pool.Count < items.Count)
        {
            var rect = ResearchPanelUi2A.Rect(parent, "MarketCard_" + pool.Count);
            var view = rect.gameObject.AddComponent<MarketCardView2A>(); view.Build(_template, select); pool.Add(view);
        }
        for (int i = 0; i < pool.Count; i++)
        { pool[i].gameObject.SetActive(i < items.Count); if (i < items.Count) pool[i].Bind(items[i], items[i].Key == selected); }
    }
    public void SetActions(string buyInfo, string sellInfo, bool canBuy, bool canSell, bool canTransfer)
    {
        _buyInfo.SetText(buyInfo); _sellInfo.SetText(sellInfo);
        SetButton(_buy, canBuy); SetButton(_sell, canSell); SetButton(_transfer, canTransfer);
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
        _grid.constraintCount = left >= 820 ? 3 : 2;
        _grid.cellSize = new Vector2(Mathf.Max(80, (left - 28 - 14 * (_grid.constraintCount - 1)) / _grid.constraintCount), _equipmentVisible ? 300 : 236);
    }
}
