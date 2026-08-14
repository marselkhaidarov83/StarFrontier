using UnityEngine;

public sealed class SystemDestinationMarkerController2 :
    CustomMonoBehaviour
{
    [Header("Markers")]

    [SerializeField]
    private Transform planetDestinationMarker;

    [SerializeField]
    private Transform mapPointDestinationMarker;

    [SerializeField]
    private Transform selectedTargetFrame;

    [Header("Sprites")]

    [SerializeField]
    private SpriteRenderer selectedTargetFrameSprite;

    [Header("Marker GameObjects")]

    [SerializeField]
    private GameObject planetDestinationObject;

    [SerializeField]
    private GameObject mapPointDestinationObject;

    [SerializeField]
    private GameObject selectedTargetFrameObject;

    [Header("System Exit")]

    [SerializeField]
    private float systemExitTargetFrameSize =
        50f;

    [Header("Compatibility")]

    [Tooltip(
        "Старая отдельная рамка SystemScene. " +
        "Отключена, потому что отображением выбранной " +
        "цели занимается TargetMarkerView2A.")]
    [SerializeField]
    private bool showLegacySelectedTargetFrame =
        false;

    private PlanetConfig _currentPlanet;

    private bool _isPlanetDestinationVisible;
    private SystemVisualConfig _systemVisualConfig;

    private void Awake()
    {
        ResolveVisualConfig();
        SetLegacyFrameVisible(false);
    }

    public void ShowPlanetDestination(
        Vector3 position,
        PlanetConfig planet)
    {
        LogCustom(
            "position = " +
            position);

        HideAll();

        _currentPlanet =
            planet;

        _isPlanetDestinationVisible =
            true;

        if (planetDestinationObject != null)
        {
            planetDestinationObject.SetActive(
                true);
        }

        SetLegacyFrameVisible(true);

        UpdatePlanetDestinationPosition(
            position,
            planet);
    }

    public void UpdatePlanetDestinationPosition(
        Vector3 position,
        PlanetConfig planet)
    {
        if (!_isPlanetDestinationVisible)
            return;

        PlanetConfig effectivePlanet =
            planet != null
                ? planet
                : _currentPlanet;

        ResolveVisualConfig();

        if (planetDestinationMarker != null)
        {
            planetDestinationMarker.position =
                position;
        }

        SetLegacyFramePosition(
            position);

        if (effectivePlanet != null &&
            effectivePlanet.PlanetOrbit != null)
        {
            SetLegacyFrameSize(
                _systemVisualConfig != null
                    ? _systemVisualConfig.GetPlanetWorldSize(
                        effectivePlanet)
                    : effectivePlanet
                        .VisualSize);
        }
    }

    public void ShowMapPointDestination(
        Vector3 position)
    {
        HideAll();

        ResolveVisualConfig();

        if (mapPointDestinationMarker != null)
        {
            mapPointDestinationMarker.position =
                position;
        }

        if (mapPointDestinationObject != null)
        {
            mapPointDestinationObject.SetActive(
                true);
        }

        SetLegacyFramePosition(
            position);

        SetLegacyFrameVisible(
            true);
    }

    public void ShowStationDestination(
        Vector3 position,
        StationConfig station)
    {
        HideAll();
        ResolveVisualConfig();

        if (mapPointDestinationMarker != null)
        {
            mapPointDestinationMarker.position =
                position;
        }

        if (mapPointDestinationObject != null)
        {
            mapPointDestinationObject.SetActive(
                true);
        }

        SetLegacyFramePosition(
            position);

        if (station != null)
        {
            SetLegacyFrameSize(
                _systemVisualConfig != null
                    ? _systemVisualConfig.GetStationWorldSize(
                        station)
                    : station.VisualSize);
        }

        SetLegacyFrameVisible(
            true);
    }

    public void ShowSystemExitDestination(
        RouteExitMapChangedEvent evt)
    {
        if (evt == null)
        {
            Debug.LogError(
                "[SystemDestinationMarkerController2] " +
                "RouteExitMapChangedEvent is null.");

            return;
        }

        HideAll();

        SetLegacyFramePosition(
            evt.ExitPoint);

        SetLegacyFrameSize(
            _systemVisualConfig != null &&
            evt.VisualSize > 0f
                ? _systemVisualConfig.UnitsToWorldSize(
                    evt.VisualSize)
                : systemExitTargetFrameSize);

        SetLegacyFrameVisible(
            true);
    }

    public void HideAll()
    {
        _currentPlanet =
            null;

        _isPlanetDestinationVisible =
            false;

        if (planetDestinationObject != null)
        {
            planetDestinationObject.SetActive(
                false);
        }

        if (mapPointDestinationObject != null)
        {
            mapPointDestinationObject.SetActive(
                false);
        }

        SetLegacyFrameVisible(
            false);
    }

    private void SetLegacyFrameVisible(
        bool visible)
    {
        if (selectedTargetFrameObject == null)
            return;

        selectedTargetFrameObject.SetActive(
            showLegacySelectedTargetFrame &&
            visible);
    }

    private void SetLegacyFramePosition(
        Vector3 position)
    {
        if (!showLegacySelectedTargetFrame)
            return;

        if (selectedTargetFrame != null)
        {
            selectedTargetFrame.position =
                position;
        }
    }

    private void SetLegacyFrameSize(
        float worldSize)
    {
        if (!showLegacySelectedTargetFrame)
            return;

        if (selectedTargetFrameSprite == null)
            return;

        SpriteRendererSizeUtility.SetWorldSize(
            selectedTargetFrameSprite,
            worldSize);
    }

    private void ResolveVisualConfig()
    {
        if (_systemVisualConfig != null)
            return;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        try
        {
            IConfigService configService =
                Bootstrapper.Instance
                    .ServiceRegistry
                    .Get<IConfigService>();

            _systemVisualConfig = configService != null
                ? configService.SystemVisualConfig
                : null;
        }
        catch
        {
            _systemVisualConfig = null;
        }
    }
}
