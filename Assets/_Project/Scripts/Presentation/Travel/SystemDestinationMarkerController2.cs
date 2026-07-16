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

    [Header("System Exit")]
    [SerializeField] private float systemExitTargetFrameSize = 50f;

    private PlanetConfig _currentPlanet;
    private bool _isPlanetDestinationVisible;

    public void ShowPlanetDestination(Vector3 position, PlanetConfig planet)
    {
        LogCustom("position = " + position);

        HideAll();

        _currentPlanet = planet;
        _isPlanetDestinationVisible = true;

        if (planetDestinationObject != null)
            planetDestinationObject.SetActive(true);

        if (selectedTargetFrameObject != null)
            selectedTargetFrameObject.SetActive(true);

        UpdatePlanetDestinationPosition(position, planet);
    }

    public void UpdatePlanetDestinationPosition(Vector3 position, PlanetConfig planet)
    {
        if (!_isPlanetDestinationVisible)
            return;

        if (planetDestinationMarker != null)
            planetDestinationMarker.position = position;

        if (selectedTargetFrame != null)
            selectedTargetFrame.position = position;

        PlanetConfig effectivePlanet = planet != null ? planet : _currentPlanet;

        if (selectedTargetFrameSprite != null &&
            effectivePlanet != null &&
            effectivePlanet.PlanetOrbit != null)
        {
            SpriteRendererSizeUtility.SetWorldSize(
                selectedTargetFrameSprite,
                effectivePlanet.PlanetOrbit.PlanetVisualSize
            );
        }
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

    public void ShowSystemExitDestination(RouteExitMapChangedEvent evt)
    {
        if (evt == null)
        {
            Debug.LogError("[SystemDestinationMarkerController2] RouteExitMapChangedEvent is null");
            return;
        }

        HideAll();

        if (selectedTargetFrame != null)
            selectedTargetFrame.position = evt.ExitPoint;

        if (selectedTargetFrameSprite != null)
        {
            SpriteRendererSizeUtility.SetWorldSize(
                selectedTargetFrameSprite,
                systemExitTargetFrameSize
            );
        }

        if (selectedTargetFrameObject != null)
            selectedTargetFrameObject.SetActive(true);
    }

    public void HideAll()
    {
        _currentPlanet = null;
        _isPlanetDestinationVisible = false;

        if (planetDestinationObject != null)
            planetDestinationObject.SetActive(false);

        if (mapPointDestinationObject != null)
            mapPointDestinationObject.SetActive(false);

        if (selectedTargetFrameObject != null)
            selectedTargetFrameObject.SetActive(false);
    }
}