using UnityEngine;
using UnityEngine.UI;

public class GalaxyRouteLineView : MonoBehaviour
{
    [SerializeField] private Image routeImage;
    [SerializeField] private Sprite normalRouteSprite;
    [SerializeField] private Sprite lockedRouteSprite;
    [SerializeField] private Sprite selectedRouteSprite;

    private string _routeId;

    public string RouteId => _routeId;

    public void Initialize(
        string routeId,
        Vector2 fromPosition,
        Vector2 toPosition,
        bool isUnlocked)
    {
        _routeId = routeId;

        RectTransform rectTransform = transform as RectTransform;

        Vector2 direction = toPosition - fromPosition;
        Vector2 middle = fromPosition + direction * 0.5f;

        float distance = direction.magnitude;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        rectTransform.anchoredPosition = middle;
        rectTransform.sizeDelta = new Vector2(distance, 12f);
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);

        if (routeImage != null)
            routeImage.sprite = isUnlocked ? normalRouteSprite : lockedRouteSprite;
    }

    public void SetSelected(bool selected)
    {
        if (routeImage == null)
            return;

        if (selected && selectedRouteSprite != null)
            routeImage.sprite = selectedRouteSprite;
    }
}
