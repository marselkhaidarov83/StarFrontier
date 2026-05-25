using UnityEngine;
using UnityEngine.UI;

public sealed class ShipEngineGlowView2 : CustomMonoBehaviour
{
    [SerializeField] private GameObject glowObject;
    [SerializeField] private SpriteRenderer glowImage;

    private void Reset()
    {
        glowObject = gameObject;
        glowImage = GetComponent<SpriteRenderer>();
    }

    public void Show()
    {
        if (glowObject != null)
            glowObject.SetActive(true);
    }

    public void Hide()
    {
        if (glowObject != null)
            glowObject.SetActive(false);
    }

    public void SetAlpha(float alpha)
    {
        if (glowImage == null)
            glowImage = GetComponent<SpriteRenderer>();

        if (glowImage == null)
            return;

        Color color = glowImage.color;
        color.a = alpha;
        glowImage.color = color;
    }
}