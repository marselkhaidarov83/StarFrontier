using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Copies the live planet-list theme rather than maintaining a parallel palette/font.</summary>
public static class StationPanelStyle2A
{
    public const string AwaitingConstructionTextColor = "#AAB7C4";
    public static bool IsAwaitingConstruction(StationRuntimeState station) => station != null &&
        station.Level == 0 && !station.IsDestroyed && station.Development == null;
    public static readonly Color PrimaryButtonColor = new Color32(30, 112, 210, 255);
    public static GameObject Clone(Transform template)
    {
        if (template == null) return null;
        var panel = Object.Instantiate(template.gameObject, template.parent, false);
        panel.name = "SystemStationPanel";
        panel.SetActive(false);
        foreach (var relay in panel.GetComponentsInChildren<SystemHudPointerClickRelay2A>(true)) relay.SetReceiver(null);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.up; rect.anchorMax = Vector2.up; rect.pivot = Vector2.up;
        panel.transform.SetAsLastSibling();
        return panel;
    }
    public static Image HeaderIcon(RectTransform panel)
    {
        var existing = panel.Find("HeaderIcon")?.GetComponent<Image>();
        if (existing != null) return existing;
        var go = new GameObject("HeaderIcon", typeof(RectTransform), typeof(Image)); go.transform.SetParent(panel, false);
        var icon = go.GetComponent<Image>(); icon.preserveAspect = true; icon.raycastTarget = false;
        var rect = icon.rectTransform; rect.anchorMin = rect.anchorMax = Vector2.up; rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(40, 40); rect.anchoredPosition = new Vector2(28, -24);
        return icon;
    }
    public static void LayoutStationHeader(RectTransform panel, TMP_Text title, Image icon, Sprite sprite, float sidePadding = 8f)
    {
        icon.sprite = sprite; icon.enabled = sprite != null; icon.color = Color.white;
        title.alignment = TextAlignmentOptions.Center;
        float iconWidth = icon.enabled ? icon.rectTransform.rect.width : 0;
        float gap = icon.enabled ? 8 : 0;
        float offset = (iconWidth + gap) * .5f;
        float textWidth = Mathf.Min(title.preferredWidth, Mathf.Max(0, panel.rect.width - 2 * sidePadding - iconWidth - gap));
        var rect = title.rectTransform; rect.sizeDelta = new Vector2(textWidth - panel.rect.width, rect.sizeDelta.y);
        rect.anchoredPosition = new Vector2(offset, rect.anchoredPosition.y);
        var position = icon.rectTransform.anchoredPosition;
        position.x = panel.rect.width * .5f + offset - textWidth * .5f - gap - iconWidth * .5f;
        icon.rectTransform.anchoredPosition = position;
    }
    public static void FitHeightToCanvas(RectTransform panel, float desiredHeight, float bottomMargin = 100f)
    {
        if (panel == null) return;
        float height = desiredHeight;
        var canvas = panel.GetComponentInParent<Canvas>();
        var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        if (canvasRect != null)
        {
            // The immediate parent is the 200-unit top HUD, not the screen bounds.
            var topInCanvas = canvasRect.InverseTransformPoint(panel.TransformPoint(new Vector3(0f, panel.rect.yMax, 0f)));
            var bottomWorld = canvasRect.TransformPoint(new Vector3(topInCanvas.x, canvasRect.rect.yMin + bottomMargin, 0f));
            float available = panel.rect.yMax - panel.InverseTransformPoint(bottomWorld).y;
            height = Mathf.Min(desiredHeight, Mathf.Max(1f, available));
        }
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }
    public static TMP_Text Text(Transform parent, TMP_Text template, string name, string value)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        CopyTypography(template, text); text.raycastTarget = false;
        text.SetText(value);
        var size = go.GetComponent<LayoutElement>(); size.minHeight = template != null ? template.fontSize + 10 : 36;
        return text;
    }
    public static void CopyTypography(TMP_Text source, TMP_Text target)
    {
        if (source == null || target == null) return;
        target.font = source.font; target.fontSharedMaterial = source.fontSharedMaterial;
        target.fontSize = source.fontSize; target.fontStyle = source.fontStyle;
        target.color = source.color; target.richText = true;
        target.textWrappingMode = TextWrappingModes.Normal;
        target.alignment = TextAlignmentOptions.TopLeft;
    }
    public static Button Button(Transform parent, TMP_Text template, Color panelColor, string label, UnityEngine.Events.UnityAction action, out TMP_Text text)
    {
        var go = new GameObject("StationAction", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.color = panelColor; image.raycastTarget = true;
        var button = go.GetComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = Color.white;
        button.colors = colors;
        var size = go.GetComponent<LayoutElement>(); size.minHeight = 58; size.preferredHeight = 84;
        text = Text(go.transform, template, "StationActionText", label);
        var rect = text.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10, 6); rect.offsetMax = new Vector2(-10, -6);
        var element = text.GetComponent<LayoutElement>(); element.ignoreLayout = true;
        button.onClick.AddListener(action); return button;
    }
    // Keep the primary action outside the masked/scrollable description.
    public static Button ConstructionFooter(RectTransform panel, TMP_Text template, Color panelColor, UnityEngine.Events.UnityAction action)
    {
        var button = Button(panel, template, PrimaryButtonColor, "Построить", action, out var label);
        button.name = "StationConstructionFooter";
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.right;
        rect.pivot = new Vector2(.5f, 0f);
        rect.offsetMin = new Vector2(12, 12); rect.offsetMax = new Vector2(-12, 76);
        button.gameObject.SetActive(false);
        return button;
    }
    public static bool IsConstructionRunning(StationRuntimeState station) => station != null && station.Level == 0 &&
        !station.IsDestroyed && station.Development?.Kind == StationOperationKind.Construction;

    public static bool IsUpgradeRunning(StationRuntimeState station) => station != null && station.Level >= 1 && station.Level < 10 &&
        !station.IsDestroyed && station.Development?.Kind == StationOperationKind.Upgrade;
    public static bool IsDevelopmentRunning(StationRuntimeState station) => IsConstructionRunning(station) || IsUpgradeRunning(station);

    public static void UpdateConstructionFooter(Button button, StationRuntimeState station, bool underAttack = false)
    {
        bool running = IsDevelopmentRunning(station);
        button.interactable = !running; button.transition = Selectable.Transition.None;
        button.GetComponent<Image>().color = running ? new Color32(65, 73, 90, 255) : PrimaryButtonColor;
        var label = button.GetComponentInChildren<TMP_Text>(true); label.color = Color.white;
        var fill = button.transform.Find("ConstructionProgressFill");
        if (!running)
        {
            if (fill != null) fill.gameObject.SetActive(false);
            label.SetText("Построить"); return;
        }
        if (fill == null)
        {
            var go = new GameObject("ConstructionProgressFill", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(button.transform, false); go.transform.SetAsFirstSibling();
            var image = go.GetComponent<Image>(); image.color = new Color32(38, 154, 79, 255); image.raycastTarget = false;
            fill = go.transform;
        }
        fill.gameObject.SetActive(true); label.transform.SetAsLastSibling();
        var operation = station.Development;
        fill.GetComponent<Image>().color = underAttack || operation.PauseReason == StationOperationPauseReason.UnderAttack
            ? new Color32(180, 70, 70, 255) : new Color32(38, 154, 79, 255);
        double duration = operation.DurationSeconds;
        if (double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0) duration = 0;
        double elapsed = operation.ElapsedSeconds;
        if (double.IsNaN(elapsed) || double.IsInfinity(elapsed)) elapsed = 0;
        elapsed = System.Math.Max(0, System.Math.Min(duration, elapsed));
        float progress = duration <= 0 ? 1f : (float)(elapsed / duration);
        var rect = fill.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(progress, 1);
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        label.SetText(DevelopmentRemainingTime(station));
    }
    public static string DevelopmentRemainingTime(StationRuntimeState station)
    {
        if (!IsDevelopmentRunning(station)) return string.Empty;
        double duration = station.Development.DurationSeconds;
        if (double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0) duration = 0;
        double elapsed = station.Development.ElapsedSeconds;
        if (double.IsNaN(elapsed) || double.IsInfinity(elapsed)) elapsed = 0;
        elapsed = System.Math.Max(0, System.Math.Min(duration, elapsed));
        double seconds = System.Math.Ceiling(System.Math.Max(0, duration - elapsed));
        double minutes = System.Math.Floor(seconds / 60);
        return seconds >= 3600 ? System.Math.Floor(seconds / 3600).ToString("00") + ":" + (minutes % 60).ToString("00") + ":" + (seconds % 60).ToString("00") :
            minutes.ToString("00") + ":" + (seconds % 60).ToString("00");
    }
    public static RectTransform ScrollContent(RectTransform panel, Color panelColor)
    {
        var go = new GameObject("StationScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        go.transform.SetParent(panel, false); var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10, 10); rect.offsetMax = new Vector2(-10, -56);
        go.GetComponent<Image>().color = new Color(panelColor.r, panelColor.g, panelColor.b, 0.01f);
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(go.transform, false); var v = viewport.GetComponent<RectTransform>();
        v.anchorMin = Vector2.zero; v.anchorMax = Vector2.one; v.offsetMin = Vector2.zero; v.offsetMax = Vector2.zero;
        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(v, false); var c = content.GetComponent<RectTransform>();
        c.anchorMin = new Vector2(0, 1); c.anchorMax = Vector2.one; c.pivot = new Vector2(.5f, 1);
        c.sizeDelta = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = go.GetComponent<ScrollRect>(); scroll.viewport = v; scroll.content = c; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        return c;
    }
}
