using UnityEngine;

public sealed class SystemDestinationMarkerController : MonoBehaviour
{
    [Header("Markers")]
    [SerializeField] private RectTransform planetDestinationMarker;
    [SerializeField] private RectTransform mapPointDestinationMarker;
    [SerializeField] private RectTransform selectedTargetFrame;

    [Header("Marker GameObjects")]
    [SerializeField] private GameObject planetDestinationObject;
    [SerializeField] private GameObject mapPointDestinationObject;
    [SerializeField] private GameObject selectedTargetFrameObject;

    public void ShowPlanetDestination(Vector2 position)
    {
        HideAll();

        if (planetDestinationMarker != null)
            planetDestinationMarker.anchoredPosition = position;

        if (selectedTargetFrame != null)
            selectedTargetFrame.anchoredPosition = position;

        if (planetDestinationObject != null)
            planetDestinationObject.SetActive(true);

        if (selectedTargetFrameObject != null)
            selectedTargetFrameObject.SetActive(true);
    }

    public void ShowMapPointDestination(Vector2 position)
    {
        HideAll();

        if (mapPointDestinationMarker != null)
            mapPointDestinationMarker.anchoredPosition = position;

        if (selectedTargetFrame != null)
            selectedTargetFrame.anchoredPosition = position;

        if (mapPointDestinationObject != null)
            mapPointDestinationObject.SetActive(true);

        if (selectedTargetFrameObject != null)
            selectedTargetFrameObject.SetActive(true);
    }

    public void ShowSystemExitDestination(Vector2 position)
    {
        HideAll();

        if (selectedTargetFrame != null)
            selectedTargetFrame.anchoredPosition = position;

        if (selectedTargetFrameObject != null)
            selectedTargetFrameObject.SetActive(true);
    }

    public void HideAll()
    {
        if (planetDestinationObject != null)
            planetDestinationObject.SetActive(false);

        if (mapPointDestinationObject != null)
            mapPointDestinationObject.SetActive(false);

        if (selectedTargetFrameObject != null)
            selectedTargetFrameObject.SetActive(false);
    }
}