using UnityEngine;
using UnityEngine.UI;

public sealed class TravelLineView : MonoBehaviour
{
    [SerializeField] private RectTransform lineRectTransform;
    [SerializeField] private Image lineImage;

    private void Reset()
    {
        lineRectTransform = GetComponent<RectTransform>();
        lineImage = GetComponent<Image>();
    }

    public void Show(Vector2 from, Vector2 to)
    {
        gameObject.SetActive(true);
        UpdateLine(from, to);
    }

    public void UpdateLine(Vector2 from, Vector2 to)
    {
        if (lineRectTransform == null)
            lineRectTransform = GetComponent<RectTransform>();

        Vector2 direction = to - from;
        float distance = direction.magnitude;

        if (distance <= 0.1f)
        {
            Hide();
            return;
        }

        lineRectTransform.anchoredPosition = from;
        lineRectTransform.sizeDelta = new Vector2(distance, lineRectTransform.sizeDelta.y);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        lineRectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetAlpha(float alpha)
    {
        if (lineImage == null)
            lineImage = GetComponent<Image>();

        if (lineImage == null)
            return;

        Color color = lineImage.color;
        color.a = alpha;
        lineImage.color = color;
    }
}