using UnityEngine;

public sealed class ShipMarkerView : MonoBehaviour
{
    [SerializeField] private RectTransform shipRectTransform;
    [SerializeField] private RectTransform currentPositionRectTransform;

    private void Reset()
    {
        shipRectTransform = GetComponent<RectTransform>();
    }

    public void SetPosition(Vector2 mapPosition)
    {
        if (shipRectTransform == null)
            shipRectTransform = GetComponent<RectTransform>();

        shipRectTransform.anchoredPosition = mapPosition;

        if (currentPositionRectTransform != null)
            currentPositionRectTransform.anchoredPosition = mapPosition;
    }

    public void SetCurrentPositionMarker(RectTransform marker)
    {
        currentPositionRectTransform = marker;
    }
}