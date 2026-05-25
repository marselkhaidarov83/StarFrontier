using UnityEngine;
using UnityEngine.UI;

public sealed class TravelLineView2 : CustomMonoBehaviour
{
    [SerializeField] private SpriteRenderer lineImage;

    private void Reset()
    {
        lineImage = GetComponent<SpriteRenderer>();
    }

    public void Show(Vector3 from, Vector3 to)
    {
        LogCustom("from = " + from + ", to = " + to);

        gameObject.SetActive(true);
        UpdateLine(from, to);
    }

    public void UpdateLine(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        float distance = direction.magnitude;

        if (distance <= 0.1f)
        {
            Hide();
            return;
        }

        // lineRectTransform.anchoredPosition = from;
        transform.position = from;
        SpriteRendererSizeUtility.SetWorldSize(
            lineImage,
            distance
        );
        // lineRectTransform.sizeDelta = new Vector3(distance, lineRectTransform.sizeDelta.y);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetAlpha(float alpha)
    {
        if (lineImage == null)
            return;

        Color color = lineImage.color;
        color.a = alpha;
        lineImage.color = color;
    }
}