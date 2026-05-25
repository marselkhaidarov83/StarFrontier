using UnityEngine;

public sealed class SystemDestinationMarkerController2 : CustomMonoBehaviour
{
    [Header("Markers")]
    [SerializeField] private Transform planetDestinationMarker;
    [SerializeField] private Transform mapPointDestinationMarker;
    [SerializeField] private Transform selectedTargetFrame;

    [Header("Sprites")]
    [SerializeField] private SpriteRenderer selectedTargetFrameSprite;

    [Header("Marker GameObjects")]
    [SerializeField] private GameObject planetDestinationObject;
    [SerializeField] private GameObject mapPointDestinationObject;
    [SerializeField] private GameObject selectedTargetFrameObject;

    public void ShowPlanetDestination(Vector3 position, PlanetConfig planet)
    {
        LogCustom("position = " + position);

        HideAll();

        if (planetDestinationMarker != null)
            planetDestinationMarker.position = position;

        if (selectedTargetFrame != null)
        {
            selectedTargetFrame.position = position;
            SpriteRendererSizeUtility.SetWorldSize(
            selectedTargetFrameSprite,
            planet.PlanetOrbit.PlanetVisualSize);
        }

        if (planetDestinationObject != null)
            planetDestinationObject.SetActive(true);

        if (selectedTargetFrameObject != null)
            selectedTargetFrameObject.SetActive(true);
    }

    public void ShowMapPointDestination(Vector3 position)
    {
        HideAll();

        if (mapPointDestinationMarker != null)
            mapPointDestinationMarker.position = position;

        if (selectedTargetFrame != null)
            selectedTargetFrame.position = position;

        if (mapPointDestinationObject != null)
            mapPointDestinationObject.SetActive(true);

        if (selectedTargetFrameObject != null)
            selectedTargetFrameObject.SetActive(true);
    }

    public void ShowSystemExitDestination(StarSystemLink link)
    {
        HideAll();

        if (selectedTargetFrame != null)
        {
            selectedTargetFrame.position = link.ExitPoint;
            SpriteRendererSizeUtility.SetWorldSize(
            selectedTargetFrameSprite,
            link.Size);
        }

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