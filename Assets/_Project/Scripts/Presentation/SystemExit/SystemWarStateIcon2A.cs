using UnityEngine;

/// <summary>Shared war-state icon rules for galaxy nodes and system exits.</summary>
internal static class SystemWarStateIcon2A
{
    public static void Apply(
        SpriteRenderer icon,
        StarSystemRuntimeState runtimeState,
        Sprite threatSprite,
        Sprite invasionSprite,
        Color threatColor,
        Color invasionColor)
    {
        if (icon == null)
            return;

        Sprite sprite = null;
        Color color = Color.white;
        if (runtimeState != null)
        {
            switch (runtimeState.SystemStatus)
            {
                case StarSystemStatus.Threat:
                    sprite = threatSprite;
                    color = threatColor;
                    break;
                case StarSystemStatus.Invasion:
                    sprite = invasionSprite;
                    color = invasionColor;
                    break;
            }
        }

        if (sprite == null)
        {
            icon.gameObject.SetActive(false);
            return;
        }

        icon.sprite = sprite;
        icon.color = color;
        icon.gameObject.SetActive(true);
    }
}
