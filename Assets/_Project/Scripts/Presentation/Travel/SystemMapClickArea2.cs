using System;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SystemMapClickArea2 : CustomMonoBehaviour, IPointerClickHandler
{
    // [SerializeField] private Transform mapRectTransform;

    public event Action<Vector3> EmptyMapClicked;

    // private void Reset()
    // {
    //     mapRectTransform = GetComponent<RectTransform>();
    // }

    public void OnPointerClick(PointerEventData eventData)
    {
        // if (mapRectTransform == null)
        //     mapRectTransform = GetComponent<RectTransform>();

        if (eventData.pointerEnter != gameObject)
            return;

        if (IsDebug())
        {
            Debug.Log("[SystemMapClickArea2] OnPointerClick.eventData.pointerCurrentRaycast.worldNormal = " + eventData.pointerCurrentRaycast.worldNormal);
            Debug.Log("[SystemMapClickArea2] OnPointerClick.eventData.pointerCurrentRaycast.worldPosition = " + eventData.pointerCurrentRaycast.worldPosition);            
        }

        // bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(
        //     mapRectTransform,
        //     eventData.position,
        //     eventData.pressEventCamera,
        //     out Vector2 localPoint
        // );

        // if (!converted)
        //     return;

        // EmptyMapClicked?.Invoke(localPoint);
        EmptyMapClicked?.Invoke(new Vector3(eventData.pointerCurrentRaycast.worldPosition.x, eventData.pointerCurrentRaycast.worldPosition.y, -2));
    }
}