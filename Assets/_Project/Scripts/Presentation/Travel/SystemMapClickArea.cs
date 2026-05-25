using System;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SystemMapClickArea : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private RectTransform mapRectTransform;

    public event Action<Vector2> EmptyMapClicked;

    private void Reset()
    {
        mapRectTransform = GetComponent<RectTransform>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (mapRectTransform == null)
            mapRectTransform = GetComponent<RectTransform>();

        if (eventData.pointerEnter != gameObject)
            return;

        bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mapRectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );

        if (!converted)
            return;

        EmptyMapClicked?.Invoke(localPoint);
    }
}