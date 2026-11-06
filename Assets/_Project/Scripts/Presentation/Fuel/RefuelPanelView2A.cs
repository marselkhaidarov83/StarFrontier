using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One shared refuelling layout for both landed planets and stations.</summary>
public sealed class RefuelPanelView2A : MonoBehaviour
{
    private const float TopHud = 194f;
    private const float BottomHud = 142f;
    private RectTransform _screen, _work, _left, _right, _shipArea, _sliderArea, _handleArea;
    private TMP_Text _levelLabel, _maximum, _zero;
    private Image _buttonImage;
    private TMP_Text _buttonLabel;
    private Vector2 _lastSize;
    private Sprite _lastHeader;
    private bool _warningVisible, _disposed;
    public bool IsReady => !_disposed && _screen != null && _work != null && _left != null && _right != null &&
        _shipArea != null && _sliderArea != null && _handleArea != null && Header != null && Ship != null &&
        FuelOverlay != null && Slider != null && Refuel != null && _buttonImage != null && _buttonLabel != null &&
        Level != null && Amount != null && Cost != null && Warning != null && _levelLabel != null &&
        _maximum != null && _zero != null;
    public Image Header { get; private set; }
    public Image Ship { get; private set; }
    public Image FuelOverlay { get; private set; }
    public TMP_Text Level { get; private set; }
    public TMP_Text Amount { get; private set; }
    public TMP_Text Cost { get; private set; }
    public TMP_Text Warning { get; private set; }
    public Slider Slider { get; private set; }
    public Button Refuel { get; private set; }
    public RectTransform TankColumn => _right;

    public void Build(TMP_Text template, Sprite sliderKnob = null)
    {
        if (_disposed || _screen != null) return;
        // Retire the old fuel controls while retaining the existing scene entry point.
        foreach (Transform child in transform) child.gameObject.SetActive(false);
        var host = transform as RectTransform;
        if (host != null && host.parent is RectTransform) Stretch(host);
        transform.SetAsFirstSibling(); // Keep the existing location action buttons above this screen.
        _screen = Rect(transform, "SharedRefuelLayout"); Stretch(_screen);
        Header = Picture(_screen, "LocationIllustration", Color.white);
        Header.preserveAspect = true;
        _work = Picture(_screen, "RefuelWorkspace", MissionPanelStyle2A.Workspace).rectTransform;
        _work.GetComponent<Image>().raycastTarget = true;
        var shipCard = Picture(_work, "ShipRefuelling", MissionPanelStyle2A.CardBackground);
        _left = shipCard.rectTransform; MissionPanelStyle2A.Frame(shipCard, MissionPanelStyle2A.CardAccent);
        CardTrim(_left, "CardTopTrim", true); CardTrim(_left, "CardLeftTrim", false);
        var tankCard = Picture(_work, "TankColumn", MissionPanelStyle2A.CardBackground);
        _right = tankCard.rectTransform; MissionPanelStyle2A.Frame(tankCard, MissionPanelStyle2A.CardAccent);
        // Intentionally empty. Tank equipment is scheduled for Sprint 9.
        _levelLabel = Text(_left, template, "FuelLevelLabel", "Уровень топлива");
        Level = Text(_left, template, "FuelLevel", string.Empty); Level.alignment = TextAlignmentOptions.Right;
        _shipArea = Rect(_left, "ShipPreview");
        Ship = Picture(_shipArea, "ShipImage", Color.white); Stretch(Ship.rectTransform); Ship.preserveAspect = true;
        FuelOverlay = Picture(_shipArea, "FuelPreview", new Color(.18f, 1f, .42f, .32f));
        Stretch(FuelOverlay.rectTransform); FuelOverlay.preserveAspect = true;
        var fuelMaterial = Resources.Load<Material>("UIFuelSilhouette2A");
        FuelOverlay.material = fuelMaterial;
        if (fuelMaterial == null)
            AppLog.Warning(DebugLogChannel.UI, typeof(RefuelPanelView2A), "Fuel silhouette material is missing");
        FuelOverlay.type = Image.Type.Filled; FuelOverlay.fillMethod = Image.FillMethod.Vertical;
        FuelOverlay.fillOrigin = (int)Image.OriginVertical.Bottom;
        _maximum = Text(_left, template, "MaximumFuel", string.Empty); _maximum.alignment = TextAlignmentOptions.Center;
        _zero = Text(_left, template, "ZeroFuel", "0"); _zero.alignment = TextAlignmentOptions.Center;
        _sliderArea = Rect(_left, "FuelSlider"); Slider = _sliderArea.gameObject.AddComponent<Slider>();
        var track = Picture(_sliderArea, "Track", new Color32(43, 59, 76, 255));
        track.rectTransform.anchorMin = new Vector2(.4f, 0); track.rectTransform.anchorMax = new Vector2(.6f, 1);
        track.rectTransform.offsetMin = track.rectTransform.offsetMax = Vector2.zero;
        var fillArea = Rect(_sliderArea, "FillArea");
        fillArea.anchorMin = new Vector2(.4f, 0); fillArea.anchorMax = new Vector2(.6f, 1);
        fillArea.offsetMin = fillArea.offsetMax = Vector2.zero;
        var fill = Picture(fillArea, "Fill", new Color32(65, 185, 112, 255));
        // A default RectTransform has a 100x100 sizeDelta. Clear it before
        // Slider drives the anchors, otherwise zero fuel still draws a large rectangle.
        Stretch(fill.rectTransform);
        Slider.fillRect = fill.rectTransform;
        _handleArea = Rect(_sliderArea, "HandleArea"); Stretch(_handleArea);
        var handle = Picture(_handleArea, "Handle", MissionPanelStyle2A.ActionBackground);
        handle.sprite = sliderKnob; handle.preserveAspect = true;
        handle.rectTransform.sizeDelta = new Vector2(36, 36);
        MissionPanelStyle2A.Frame(handle, MissionPanelStyle2A.ActionAccent);
        Slider.handleRect = handle.rectTransform; Slider.targetGraphic = handle;
        Slider.direction = UnityEngine.UI.Slider.Direction.BottomToTop;
        Slider.wholeNumbers = true; Slider.minValue = 0;
        var hit = _sliderArea.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
        Amount = Text(_left, template, "FuelAmount", string.Empty);
        Cost = Text(_left, template, "RefuelCost", string.Empty); Cost.alignment = TextAlignmentOptions.Right;
        _buttonImage = Picture(_left, "RefuelButton", MissionPanelStyle2A.ActionBackground);
        _buttonImage.raycastTarget = true;
        Refuel = _buttonImage.gameObject.AddComponent<Button>(); Refuel.targetGraphic = _buttonImage;
        MissionPanelStyle2A.Action(Refuel, false);
        _buttonLabel = Text(_buttonImage.transform, template, "Label", "Заправить");
        Stretch(_buttonLabel.rectTransform); _buttonLabel.alignment = TextAlignmentOptions.Center;
        _buttonLabel.enableAutoSizing = false; _buttonLabel.fontSize = 24;
        Warning = Text(_left, template, "RefuelWarning", string.Empty); Warning.color = new Color32(239, 183, 126, 255);
        RefreshLayout();
    }

    public void SetSprites(Sprite header, Sprite ship)
    {
        if (!IsReady) return;
        Header.sprite = header; Header.enabled = header != null;
        Ship.sprite = ship; Ship.enabled = ship != null;
        FuelOverlay.sprite = ship; FuelOverlay.enabled = ship != null;
        RefreshLayout();
    }
    public void SetPreview(int current, int selected, int capacity, long cost, bool canBuy, string warning)
    {
        if (!IsReady) return;
        Slider.minValue = 0; Slider.maxValue = Mathf.Max(0, capacity);
        Slider.SetValueWithoutNotify(selected);
        Slider.interactable = capacity > current;
        FuelOverlay.fillAmount = capacity > 0 ? Mathf.Clamp01(selected / (float)capacity) : 0;
        Level.SetText(selected + " / " + capacity);
        Amount.SetText("Добавить: " + Mathf.Max(0, selected - current) + " ед.");
        Cost.SetText(cost.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")) + " кр.");
        _maximum.SetText(capacity.ToString());
        Refuel.interactable = canBuy;
        MissionPanelStyle2A.Action(Refuel, canBuy);
        SetWarning(warning);
    }
    public void SetWarning(string message)
    {
        if (!IsReady) return;
        Warning.SetText(message ?? string.Empty);
        bool visible = !string.IsNullOrWhiteSpace(message);
        Warning.gameObject.SetActive(visible);
        if (_warningVisible == visible) return;
        _warningVisible = visible;
        RefreshLayout();
    }
    private void OnDestroy() { _disposed = true; }
    private void LateUpdate()
    {
        if (IsReady && (_lastSize != _screen.rect.size || _lastHeader != Header.sprite)) RefreshLayout();
    }
    public void RefreshLayout()
    {
        if (!IsReady) return;
        float width = _screen.rect.width, height = _screen.rect.height;
        if (width <= 0 || height <= 0) return;
        _lastSize = _screen.rect.size; _lastHeader = Header.sprite;
        float heroHeight = Header.sprite != null ? width * Header.sprite.rect.height / Header.sprite.rect.width : 0;
        Place(Header.rectTransform, 0, TopHud, width, heroHeight);
        _work.anchorMin = Vector2.zero; _work.anchorMax = Vector2.one;
        _work.offsetMin = new Vector2(0, BottomHud); _work.offsetMax = new Vector2(0, -TopHud - heroHeight);
        float workHeight = Mathf.Max(0, height - TopHud - heroHeight - BottomHud);
        const float margin = 18f, gap = 18f;
        float innerWidth = Mathf.Max(0, width - 2 * margin - gap);
        float leftWidth = innerWidth * .677f, rightWidth = innerWidth - leftWidth;
        float cardHeight = Mathf.Max(0, workHeight - 2 * margin);
        Place(_left, margin, margin, leftWidth, cardHeight);
        Place(_right, margin + leftWidth + gap, margin, rightWidth, cardHeight);
        float padding = Mathf.Min(34, leftWidth * .06f), line = 48;
        float valueWidth = Mathf.Min(230, leftWidth * .4f);
        Place(_levelLabel.rectTransform, padding, padding, Mathf.Max(0, leftWidth - 2 * padding - valueWidth), line);
        Place(Level.rectTransform, leftWidth - padding - valueWidth, padding, valueWidth, line);
        const float bottomPadding = 18f, buttonHeight = 76f, controlGap = 16f;
        float warningReserve = _warningVisible ? 60f : 0f;
        float buttonTop = Mathf.Max(0, cardHeight - bottomPadding - warningReserve - buttonHeight);
        float footerTop = Mathf.Max(0, buttonTop - controlGap - line);
        float previewTop = padding + line + 28;
        float previewHeight = Mathf.Max(0, footerTop - 22f - previewTop);
        float sliderWidth = Mathf.Min(64, leftWidth * .16f);
        float sliderLeft = leftWidth - padding - sliderWidth;
        Place(_shipArea, padding, previewTop, Mathf.Max(0, sliderLeft - padding - 18), previewHeight);
        Place(_maximum.rectTransform, sliderLeft - 12, previewTop, sliderWidth + 24, 38);
        Place(_sliderArea, sliderLeft, previewTop + 54, sliderWidth, Mathf.Max(0, previewHeight - 108));
        Place(_zero.rectTransform, sliderLeft, previewTop + Mathf.Max(0, previewHeight - 38), sliderWidth, 38);
        float contentWidth = Mathf.Max(0, leftWidth - 2 * padding);
        Place(Amount.rectTransform, padding, footerTop, contentWidth * .6f, line);
        Place(Cost.rectTransform, padding + contentWidth * .6f, footerTop, contentWidth * .4f, line);
        Place(Refuel.GetComponent<RectTransform>(), padding, buttonTop, contentWidth, buttonHeight);
        Place(Warning.rectTransform, padding, buttonTop + buttonHeight + 12f, contentWidth, 48);
    }
    private static void CardTrim(RectTransform card, string name, bool top)
    {
        var image = Picture(card, name, MissionPanelStyle2A.CardAccent); var rect = image.rectTransform;
        rect.anchorMin = top ? Vector2.up : Vector2.zero;
        rect.anchorMax = Vector2.one;
        if (top)
        {
            rect.pivot = Vector2.up; rect.sizeDelta = new Vector2(-4, 4);
            rect.anchoredPosition = new Vector2(2, -2);
        }
        else
        {
            rect.anchorMax = Vector2.up; rect.pivot = Vector2.up;
            rect.sizeDelta = new Vector2(5, -4); rect.anchoredPosition = new Vector2(2, -2);
        }
    }

    private static TMP_Text Text(Transform parent, TMP_Text template, string name, string value)
    {
        var rect = Rect(parent, name); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        StationPanelStyle2A.CopyTypography(template, text);
        text.fontSize = 28; text.enableAutoSizing = true; text.fontSizeMin = 22; text.fontSizeMax = 28;
        text.fontStyle = FontStyles.Normal;
        text.color = Color.white; text.raycastTarget = false; text.alignment = TextAlignmentOptions.Left;
        text.SetText(value); return text;
    }
    private static Image Picture(Transform parent, string name, Color color)
    {
        var rect = Rect(parent, name); var image = rect.gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false; return image;
    }
    private static RectTransform Rect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = parent.gameObject.layer;
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect;
    }
    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void Place(RectTransform rect, float x, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = Vector2.up; rect.pivot = Vector2.up;
        rect.anchoredPosition = new Vector2(x, -top); rect.sizeDelta = new Vector2(width, height);
    }
}
