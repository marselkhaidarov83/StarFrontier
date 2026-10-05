using UnityEngine;

[CreateAssetMenu(
    fileName = "SystemVisualConfig",
    menuName = "StarFrontier/Configs/Sprint 3/System Visual")]
public sealed class SystemVisualConfig : ScriptableObject
{
    [Header("System Object Size Grid")]
    [SerializeField]
    [Min(0.01f)]
    private float sizeUnitPixels = 1f;

    [Header("Player Ship Visuals")]
    [SerializeField]
    private Sprite playerShipPlaceholderSprite;

    [SerializeField]
    private Sprite shipShadowSprite;

    [SerializeField]
    private Sprite engineGlowSprite;

    [Header("World Object Visuals")]
    [SerializeField]
    private Sprite sunPlaceholderSprite;

    [SerializeField]
    private Sprite planetPlaceholderSprite;

    [SerializeField]
    private Sprite stationPlaceholderSprite;

    [Header("Markers")]
    [SerializeField]
    private Sprite targetMarkerSprite;

    [SerializeField]
    private Sprite interactionMarkerSprite;

    [SerializeField]
    [Min(0f)]
    private float targetMarkerScale = 1f;

    [SerializeField]
    [Min(0f)]
    private float interactionMarkerScale = 1f;

    [Header("Background Layers")]
    [SerializeField]
    private Sprite backgroundFarSprite;

    [SerializeField]
    private Sprite backgroundMidSprite;

    [SerializeField]
    private Sprite backgroundNearSprite;

    [Header("VFX")]
    [SerializeField]
    [Min(0f)]
    private float engineGlowMinScale = 0.6f;

    [SerializeField]
    [Min(0f)]
    private float engineGlowMaxScale = 1.15f;

    [SerializeField]
    [Range(0f, 1f)]
    private float idleEngineGlowAlpha = 0.35f;

    public Sprite PlayerShipPlaceholderSprite => playerShipPlaceholderSprite;
    public Sprite ShipShadowSprite => shipShadowSprite;
    public Sprite EngineGlowSprite => engineGlowSprite;
    public float SizeUnitPixels => Mathf.Max(0.01f, sizeUnitPixels);

    public Sprite SunPlaceholderSprite => sunPlaceholderSprite;
    public Sprite PlanetPlaceholderSprite => planetPlaceholderSprite;
    public Sprite StationPlaceholderSprite => stationPlaceholderSprite;

    public Sprite TargetMarkerSprite => targetMarkerSprite;
    public Sprite InteractionMarkerSprite => interactionMarkerSprite;
    public float TargetMarkerScale => targetMarkerScale;
    public float InteractionMarkerScale => interactionMarkerScale;

    public Sprite BackgroundFarSprite => backgroundFarSprite;
    public Sprite BackgroundMidSprite => backgroundMidSprite;
    public Sprite BackgroundNearSprite => backgroundNearSprite;

    public float EngineGlowMinScale => engineGlowMinScale;
    public float EngineGlowMaxScale => engineGlowMaxScale;
    public float IdleEngineGlowAlpha => idleEngineGlowAlpha;

    public float UnitsToWorldSize(float sizeUnits)
    {
        return Mathf.Max(0f, sizeUnits) * SizeUnitPixels;
    }

    public float GetSunWorldSize(SunConfig sun)
    {
        float units = sun != null
            ? sun.VisualSize
            : 0f;

        return UnitsToWorldSize(units);
    }

    public float GetPlanetWorldSize(PlanetConfig planet)
    {
        float units = planet != null
            ? planet.VisualSize
            : 0f;

        return UnitsToWorldSize(units);
    }

    public float GetStationWorldSize(StationConfig station)
    {
        float units = station != null
            ? station.VisualSize
            : 0f;

        return UnitsToWorldSize(units);
    }

    public float GetAllyWorldSize(AllyConfig ally)
    {
        float units = ally != null
            ? ally.VisualSize
            : 0f;

        return UnitsToWorldSize(units);
    }

    public float GetEnemyWorldSize(EnemyConfig enemy)
    {
        float units = enemy != null
            ? enemy.VisualSize
            : 0f;

        return UnitsToWorldSize(units);
    }

    public float GetPirateWorldSize(PirateConfig pirate)
    {
        float units = pirate != null
            ? pirate.VisualSize
            : 0f;

        return UnitsToWorldSize(units);
    }

    public float GetSystemExitWorldSize(RouteEndpointConfig endpoint)
    {
        float units = endpoint != null
            ? endpoint.VisualSize
            : 0f;

        return UnitsToWorldSize(units);
    }
}
