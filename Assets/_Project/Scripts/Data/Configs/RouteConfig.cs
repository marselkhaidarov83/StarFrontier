using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "RouteConfig", menuName = "StarFrontier/Configs/Route")]
public class RouteConfig : BaseConfig
{
    [SerializeField] public StarSystemConfig FromSystem;
    [SerializeField] public StarSystemConfig ToSystem;

    [SerializeField] public bool IsLockedAtStart;
    [SerializeField] public int RequiredScanLevel;
    [SerializeField] public int ParsecDistance;

    [SerializeField] private RouteEndpointConfig fromSystemRouteEndpointConfig;
    [SerializeField] private RouteEndpointConfig toSystemRouteEndpointConfig;

    public RouteEndpointConfig FromSystemRouteEndpointConfig => fromSystemRouteEndpointConfig;
    public RouteEndpointConfig ToSystemRouteEndpointConfig => toSystemRouteEndpointConfig;

    public bool ContainsSystem(string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId))
            return false;

        return IsSystem(FromSystem, systemId) ||
               IsSystem(ToSystem, systemId);
    }

    public bool ConnectsSystems(string firstSystemId, string secondSystemId)
    {
        if (string.IsNullOrWhiteSpace(firstSystemId))
            return false;

        if (string.IsNullOrWhiteSpace(secondSystemId))
            return false;

        bool direct =
            IsSystem(FromSystem, firstSystemId) &&
            IsSystem(ToSystem, secondSystemId);

        bool reverse =
            IsSystem(FromSystem, secondSystemId) &&
            IsSystem(ToSystem, firstSystemId);

        return direct || reverse;
    }

    public StarSystemConfig GetOtherSystem(string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId))
            return null;

        if (IsSystem(FromSystem, systemId))
            return ToSystem;

        if (IsSystem(ToSystem, systemId))
            return FromSystem;

        return null;
    }

    public RouteEndpointConfig GetEndPointForSystem(string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId))
            return null;

        if (IsSystem(FromSystem, systemId))
            return fromSystemRouteEndpointConfig;

        if (IsSystem(ToSystem, systemId))
            return toSystemRouteEndpointConfig;

        return null;
    }

    public RouteEndpointConfig GetDepartureEndpoint(string fromSystemId)
    {
        return GetEndPointForSystem(fromSystemId);
    }

    public RouteEndpointConfig GetArrivalEndpoint(string toSystemId)
    {
        return GetEndPointForSystem(toSystemId);
    }

    public Vector3 GetExitPoint(string fromSystemId)
    {
        RouteEndpointConfig endpoint = GetDepartureEndpoint(fromSystemId);

        if (endpoint == null)
            return Vector3.zero;

        return endpoint.ExitPoint;
    }

    public Vector3 GetEntryPoint(string toSystemId)
    {
        RouteEndpointConfig endpoint = GetArrivalEndpoint(toSystemId);

        if (endpoint == null)
            return Vector3.zero;

        return endpoint.EntryPoint;
    }

    private bool IsSystem(StarSystemConfig systemConfig, string systemId)
    {
        return systemConfig != null &&
               systemConfig.Id == systemId;
    }
}