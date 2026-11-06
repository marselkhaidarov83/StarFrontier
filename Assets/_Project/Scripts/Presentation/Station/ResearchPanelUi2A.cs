using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Mission typography and layout primitives shared by the research screen and card prefab.</summary>
public static class ResearchPanelUi2A
{
    public static RectTransform Rect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = parent.gameObject.layer;
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect;
    }
    public static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    public static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = Vector2.up; rect.pivot = Vector2.up;
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(Mathf.Max(0, width), Mathf.Max(0, height));
    }
    public static Image Picture(Transform parent, string name, Color color)
    {
        var image = Rect(parent, name).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
    }
    public static TMP_Text Text(Transform parent, TMP_Text template, string name, string value)
    {
        var text = Rect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
        StationPanelStyle2A.CopyTypography(template, text);
        text.fontSize = 30; text.enableAutoSizing = true; text.fontSizeMin = 20; text.fontSizeMax = 30;
        text.fontStyle = FontStyles.Normal; text.color = Color.white; text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Left; text.SetText(value); return text;
    }
    public static Button Button(Transform parent, TMP_Text template, string name, string label, out TMP_Text text)
    {
        var image = Picture(parent, name, MissionPanelStyle2A.ActionBackground); image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        text = Text(image.transform, template, "Label", label); Stretch(text.rectTransform);
        text.margin = new Vector4(10, 3, 10, 3); text.alignment = TextAlignmentOptions.Center;
        MissionPanelStyle2A.Action(button, true); return button;
    }
    public static ScrollRect Scroll(Transform parent, string name, bool horizontal, out RectTransform content)
    {
        var host = Rect(parent, name); var hit = host.gameObject.AddComponent<Image>(); hit.color = Color.clear;
        var scroll = host.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = horizontal; scroll.vertical = !horizontal;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        var viewport = Rect(host, "Viewport"); Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>();
        var maskHit = viewport.gameObject.AddComponent<Image>(); maskHit.color = Color.clear;
        content = Rect(viewport, "Content");
        content.anchorMin = horizontal ? Vector2.zero : Vector2.up;
        content.anchorMax = Vector2.one; content.pivot = Vector2.up;
        content.offsetMin = content.offsetMax = Vector2.zero;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        if (horizontal)
        {
            content.anchorMin = content.anchorMax = Vector2.up;
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 12;
            // Outline vertices extend beyond button bounds: keep all four edges inside RectMask2D.
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childForceExpandHeight = true; layout.childForceExpandWidth = false;
            layout.childControlHeight = true; layout.childControlWidth = true;
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        else
        {
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 14;
            layout.padding = new RectOffset(4, 4, 4, 12); layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false; layout.childControlWidth = true; layout.childControlHeight = true;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        scroll.viewport = viewport; scroll.content = content; return scroll;
    }
    public static string Time(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return "—";
        seconds = System.Math.Max(0, System.Math.Ceiling(seconds));
        return ((long)seconds / 60).ToString("00") + ":" + ((long)seconds % 60).ToString("00");
    }
}
