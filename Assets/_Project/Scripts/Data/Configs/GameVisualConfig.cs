using UnityEngine;

// Визуальные настройки карты системы и карты галактики.
[CreateAssetMenu(
    fileName = "GameVisualConfig",
    menuName = "StarFrontier/Configs/Game/Game Visual")]
public sealed class GameVisualConfig : BaseConfig
{
    public const float DefaultSystemExitVisualSize = 50f;
    public const float DefaultStationVisualSize = 50f;
    public const float DefaultGalaxyMapRoutePointSpacing = 0.25f;

    [Header("System Object Size Grid")]
    [SerializeField]
    [Min(0.01f)]
    [Tooltip("Параметр sizeUnitPixels. Используется связанными игровыми системами этого конфига.")]
    private float sizeUnitPixels = 1f;

    [Header("World Object Visuals")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("Общий визуальный размер точки выхода из звёздной системы.")]
    private float systemExitVisualSize = DefaultSystemExitVisualSize;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Общий визуальный размер станции на карте системы.")]
    private float stationVisualSize = DefaultStationVisualSize;

    [Header("Карта галактики")]
    [SerializeField, Min(0.01f)]
    [Tooltip("Расстояние между соседними точками при отрисовке линии маршрута на карте галактики.")]
    private float galaxyMapRoutePointSpacing = DefaultGalaxyMapRoutePointSpacing;

    public float GalaxyMapRoutePointSpacing => galaxyMapRoutePointSpacing > 0f
        ? galaxyMapRoutePointSpacing : DefaultGalaxyMapRoutePointSpacing;
    public float SizeUnitPixels => Mathf.Max(0.01f, sizeUnitPixels);
    public float SystemExitVisualSize => Mathf.Max(0f, systemExitVisualSize);
    public float StationVisualSize => Mathf.Max(0f, stationVisualSize);

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
            ? StationVisualSize
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
            ? SystemExitVisualSize
            : 0f;

        return UnitsToWorldSize(units);
    }
}
