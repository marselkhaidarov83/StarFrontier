using UnityEngine;
using UnityEngine.UI;

public sealed class ShipEngineGlowView : MonoBehaviour
{
    [SerializeField] private GameObject glowObject;
    [SerializeField] private Image glowImage;

    private void Reset()
    {
        glowObject = gameObject;
        glowImage = GetComponent<Image>();
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
            glowImage = GetComponent<Image>();

        if (glowImage == null)
            return;

        Color color = glowImage.color;
        color.a = alpha;
        glowImage.color = color;
    }
}