using UnityEngine;
using UnityEngine.UI;

public class GalaxyRouteView2A : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Image lineImage;

    public void Setup(Vector2 fromPosition, Vector2 toPosition, bool isUnlocked)
    {
        var center = (fromPosition + toPosition) * 0.5f;
        var direction = toPosition - fromPosition;
        var length = direction.magnitude;

        rectTransform.anchoredPosition = center;
        rectTransform.sizeDelta = new Vector2(length, 6f);

        var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);

        if (lineImage != null)
        {
            lineImage.color = isUnlocked
                ? new Color(0.2f, 0.8f, 1f, 0.8f)
                : new Color(0.35f, 0.35f, 0.35f, 0.35f);
        }
    }
}