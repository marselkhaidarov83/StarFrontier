using UnityEngine;

// Конфиг SystemVisualConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(
    fileName = "SystemVisualConfig",
    menuName = "StarFrontier/Configs/Sprint 3/System Visual")]
public sealed class SystemVisualConfig : BaseConfig
{
    [Header("System Object Size Grid")]
    [SerializeField]
    [Min(0.01f)]
    [Tooltip("Параметр sizeUnitPixels. Используется связанными игровыми системами этого конфига.")]
    private float sizeUnitPixels = 1f;

    [Header("Player Ship Visuals")]
    [SerializeField]
    [Tooltip("Спрайт для поля playerShipPlaceholderSprite. Используется визуальной частью игры при отображении объекта.")]
    private Sprite playerShipPlaceholderSprite;

    [SerializeField]
    [Tooltip("Спрайт для поля shipShadowSprite. Используется визуальной частью игры при отображении объекта.")]
    private Sprite shipShadowSprite;

    [SerializeField]
    [Tooltip("Спрайт для поля engineGlowSprite. Используется визуальной частью игры при отображении объекта.")]
    private Sprite engineGlowSprite;

    [Header("World Object Visuals")]
    [SerializeField]
    [Tooltip("Спрайт для поля sunPlaceholderSprite. Используется визуальной частью игры при отображении объекта.")]
    private Sprite sunPlaceholderSprite;

    [SerializeField]
    [Tooltip("Спрайт для поля planetPlaceholderSprite. Используется визуальной частью игры при отображении объекта.")]
    private Sprite planetPlaceholderSprite;

    [SerializeField]
    [Tooltip("Спрайт для поля stationPlaceholderSprite. Используется визуальной частью игры при отображении объекта.")]
    private Sprite stationPlaceholderSprite;

    [Header("Markers")]
    [SerializeField]
    [Tooltip("Спрайт для поля targetMarkerSprite. Используется визуальной частью игры при отображении объекта.")]
    private Sprite targetMarkerSprite;

    [SerializeField]
    [Tooltip("Спрайт для поля interactionMarkerSprite. Используется визуальной частью игры при отображении объекта.")]
    private Sprite interactionMarkerSprite;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Параметр targetMarkerScale. Используется связанными игровыми системами этого конфига.")]
    private float targetMarkerScale = 1f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Параметр interactionMarkerScale. Используется связанными игровыми системами этого конфига.")]
    private float interactionMarkerScale = 1f;

    [Header("Background Layers")]
    [SerializeField]
    [Tooltip("Спрайт для поля backgroundFarSprite. Используется визуальной частью игры при отображении объекта.")]
    private Sprite backgroundFarSprite;

    [SerializeField]
    [Tooltip("Спрайт для поля backgroundMidSprite. Используется визуальной частью игры при отображении объекта.")]
    private Sprite backgroundMidSprite;

    [SerializeField]
    [Tooltip("Спрайт для поля backgroundNearSprite. Используется визуальной частью игры при отображении объекта.")]
    private Sprite backgroundNearSprite;

    [Header("VFX")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("Минимальное значение параметра engineGlowMinScale. Используется как нижняя граница диапазона.")]
    private float engineGlowMinScale = 0.6f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Максимальное значение параметра engineGlowMaxScale. Используется как верхняя граница диапазона.")]
    private float engineGlowMaxScale = 1.15f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Параметр idleEngineGlowAlpha. Используется связанными игровыми системами этого конфига.")]
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
