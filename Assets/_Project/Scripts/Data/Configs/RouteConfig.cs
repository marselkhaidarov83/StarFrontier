using UnityEngine;

// Конфиг RouteConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(
    fileName = "RouteConfig",
    menuName = "StarFrontier/Configs/Galaxy/Route"
)]
public class RouteConfig : BaseConfig
{
    public const int MaxDevelopmentLevel = 10;

    [Header("Системы")]

    [SerializeField]
    [Tooltip("Параметр FromSystem. Используется связанными игровыми системами этого конфига.")]
    public StarSystemConfig FromSystem;

    [SerializeField]
    [Tooltip("Параметр ToSystem. Используется связанными игровыми системами этого конфига.")]
    public StarSystemConfig ToSystem;

    [Header("Условия маршрута")]

    [SerializeField]
    [Tooltip("Переключатель IsLockedAtStart. Включает или выключает соответствующее правило или отображение.")]
    public bool IsLockedAtStart;

    [Header("Топливо по уровням развития (1–10)")]
    [SerializeField] private System.Collections.Generic.List<int> fuelByDevelopmentLevel = new();
    public System.Collections.Generic.IReadOnlyList<int> FuelByDevelopmentLevel => fuelByDevelopmentLevel;

    public bool IsUnlocked(GalaxyRuntimeState galaxy)
    {
        if (string.IsNullOrWhiteSpace(Id) || galaxy?.Routes == null)
            return false;
        foreach (RouteRuntimeState route in galaxy.Routes)
            if (route != null && route.RouteId == Id)
                return route.IsUnlocked;
        return false;
    }

    public int GetFuelCost(int level)
    {
        if (fuelByDevelopmentLevel == null || fuelByDevelopmentLevel.Count == 0)
            return 1;

        int index = level >= 1 && level <= MaxDevelopmentLevel && level <= fuelByDevelopmentLevel.Count
            ? level - 1
            : 0;
        return System.Math.Max(1, fuelByDevelopmentLevel[index]);
    }

    public int GetFuelCost(GalaxyRuntimeState galaxy)
    {
        if (galaxy?.Routes != null)
            foreach (RouteRuntimeState route in galaxy.Routes)
                if (route != null && route.RouteId == Id)
                    return GetFuelCost(route.DevelopmentLevel);

        return GetFuelCost(1);
    }

    [Header("Точки входа и выхода")]

    [SerializeField]
    private RouteEndpointConfig
        fromSystemRouteEndpointConfig;

    [SerializeField]
    private RouteEndpointConfig
        toSystemRouteEndpointConfig;

    [Header("Отображение на карте галактики")]

    [SerializeField]
    [Range(-0.5f, 0.5f)]
    [Tooltip("Параметр galaxyMapCurveStrength. Используется связанными игровыми системами этого конфига.")]
    private float galaxyMapCurveStrength =
        0f;

    public RouteEndpointConfig
        FromSystemRouteEndpointConfig =>
            fromSystemRouteEndpointConfig;

    public RouteEndpointConfig
        ToSystemRouteEndpointConfig =>
            toSystemRouteEndpointConfig;

    public float GalaxyMapCurveStrength =>
        Mathf.Clamp(
            galaxyMapCurveStrength,
            -0.5f,
            0.5f);

    public bool ContainsSystem(
        string systemId)
    {
        if (string.IsNullOrWhiteSpace(
                systemId))
        {
            return false;
        }

        return
            IsSystem(
                FromSystem,
                systemId) ||
            IsSystem(
                ToSystem,
                systemId);
    }

    public bool ConnectsSystems(
        string firstSystemId,
        string secondSystemId)
    {
        if (string.IsNullOrWhiteSpace(
                firstSystemId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(
                secondSystemId))
        {
            return false;
        }

        bool direct =
            IsSystem(
                FromSystem,
                firstSystemId) &&
            IsSystem(
                ToSystem,
                secondSystemId);

        bool reverse =
            IsSystem(
                FromSystem,
                secondSystemId) &&
            IsSystem(
                ToSystem,
                firstSystemId);

        return direct || reverse;
    }

    public StarSystemConfig GetOtherSystem(
        string systemId)
    {
        if (string.IsNullOrWhiteSpace(
                systemId))
        {
            return null;
        }

        if (IsSystem(
                FromSystem,
                systemId))
        {
            return ToSystem;
        }

        if (IsSystem(
                ToSystem,
                systemId))
        {
            return FromSystem;
        }

        return null;
    }

    public RouteEndpointConfig GetEndPointForSystem(
        string systemId)
    {
        if (string.IsNullOrWhiteSpace(
                systemId))
        {
            return null;
        }

        if (IsSystem(
                FromSystem,
                systemId))
        {
            return
                fromSystemRouteEndpointConfig;
        }

        if (IsSystem(
                ToSystem,
                systemId))
        {
            return
                toSystemRouteEndpointConfig;
        }

        return null;
    }

    public RouteEndpointConfig GetDepartureEndpoint(
        string fromSystemId)
    {
        return GetEndPointForSystem(
            fromSystemId);
    }

    public RouteEndpointConfig GetArrivalEndpoint(
        string toSystemId)
    {
        return GetEndPointForSystem(
            toSystemId);
    }

    public Vector3 GetExitPoint(
        string fromSystemId)
    {
        RouteEndpointConfig endpoint =
            GetDepartureEndpoint(
                fromSystemId);

        if (endpoint == null)
            return Vector3.zero;

        return endpoint.ExitPoint;
    }

    public Vector3 GetEntryPoint(
        string toSystemId)
    {
        RouteEndpointConfig endpoint =
            GetArrivalEndpoint(
                toSystemId);

        if (endpoint == null)
            return Vector3.zero;

        return endpoint.EntryPoint;
    }

    private bool IsSystem(
        StarSystemConfig systemConfig,
        string systemId)
    {
        return
            systemConfig != null &&
            systemConfig.Id == systemId;
    }
}
