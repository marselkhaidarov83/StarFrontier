using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared visual language of mission cards and location service panels.</summary>
public static class MissionPanelStyle2A
{
    public static readonly Color Workspace = new Color(.07f, .15f, .23f, 1f);
    public static readonly Color CardBackground = new Color(.09f, .19f, .29f, 1f);
    public static readonly Color CardAccent = new Color(.13f, .65f, .92f, 1f);
    public static readonly Color ActionBackground = new Color(.05f, .35f, .58f, 1f);
    public static readonly Color ActionAccent = new Color(.25f, .79f, 1f, 1f);
    public static readonly Color DisabledBackground = new Color(.23f, .29f, .35f, 1f);
    public static readonly Color DisabledAccent = new Color(.49f, .56f, .63f, 1f);

    public static Outline Frame(Image image, Color accent)
    {
        var outline = image.GetComponent<Outline>() ?? image.gameObject.AddComponent<Outline>();
        outline.effectColor = accent; outline.effectDistance = new Vector2(2f, 2f);
        outline.useGraphicAlpha = false; return outline;
    }
    public static void Action(Button button, bool available)
    {
        var image = button.GetComponent<Image>();
        image.color = available ? ActionBackground : DisabledBackground;
        Frame(image, available ? ActionAccent : DisabledAccent);
        var colors = button.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
        colors.pressedColor = new Color(.7f, .85f, .95f, 1f); colors.disabledColor = Color.white;
        button.colors = colors;
    }
}
