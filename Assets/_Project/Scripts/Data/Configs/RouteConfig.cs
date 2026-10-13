using UnityEngine;

// Конфиг RouteConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(
    fileName = "RouteConfig",
    menuName = "StarFrontier/Configs/Galaxy/Route"
)]
public class RouteConfig : BaseConfig
{
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

    [SerializeField]
    [Tooltip("Параметр RequiredScanLevel. Используется связанными игровыми системами этого конфига.")]
    public int RequiredScanLevel;

    [SerializeField]
    [Tooltip("Параметр ParsecDistance. Используется связанными игровыми системами этого конфига.")]
    public int ParsecDistance;

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
