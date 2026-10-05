using System.Linq;

public class RouteService : CustomService, IRouteService
{
    private GalaxyRuntimeState _galaxyRuntimeState;
    private readonly SimpleEventBus _eventBus;
    private readonly IConfigService _configService;
    private const double PerfLogThresholdMs = 2.0;

    public RouteService()
    {
        GalaxyRuntimeStateUpdate();
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
    }

    private void GalaxyRuntimeStateUpdate()
    {
        if (_galaxyRuntimeState == null && Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>().State != null)
            _galaxyRuntimeState = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>().State.Galaxy;
    }

    public bool IsRouteUnlocked(string routeId)
    {
        if (string.IsNullOrWhiteSpace(routeId))
            return false;

        RouteConfig routeConfig = FindRouteConfigById(routeId);

        if (routeConfig == null)
            return false;

        return IsRouteAvailable(routeConfig);
    }

    public bool HasUnlockedRoute(string fromSystemId, string toSystemId)
    {
        if (string.IsNullOrWhiteSpace(fromSystemId))
            return false;

        if (string.IsNullOrWhiteSpace(toSystemId))
            return false;

        RouteConfig routeConfig = FindRouteConfig(fromSystemId, toSystemId);

        if (routeConfig == null)
            return false;

        return IsRouteAvailable(routeConfig);
    }

    private bool IsRouteAvailable(RouteConfig routeConfig)
    {
        if (routeConfig == null)
            return false;

        if (!IsRouteStateUnlocked(routeConfig))
            return false;

        if (routeConfig.FromSystem == null || routeConfig.ToSystem == null)
            return false;

        if (!IsSystemSectorUnlocked(routeConfig.FromSystem.Id))
            return false;

        if (!IsSystemSectorUnlocked(routeConfig.ToSystem.Id))
            return false;

        return true;
    }

    private bool IsRouteStateUnlocked(RouteConfig routeConfig)
    {
        GalaxyRuntimeStateUpdate();

        RouteRuntimeState routeState = FindRouteState(routeConfig.Id);

        if (routeState != null)
            return routeState.IsUnlocked;

        return routeConfig.IsLockedAtStart == false;
    }

    private bool IsSystemSectorUnlocked(string systemId)
    {
        GalaxyRuntimeStateUpdate();

        if (string.IsNullOrWhiteSpace(systemId))
            return false;

        foreach (SectorConfig sectorConfig in _configService.GetAllSectors())
        {
            if (sectorConfig == null || sectorConfig.Systems == null)
                continue;

            bool containsSystem = sectorConfig.Systems.Any(
                systemConfig => systemConfig != null && systemConfig.Id == systemId
            );

            if (!containsSystem)
                continue;

            SectorRuntimeState sectorState = _galaxyRuntimeState?.Sectors?
                .FirstOrDefault(state => state != null && state.SectorId == sectorConfig.Id);

            if (sectorState != null)
                return sectorState.IsUnlocked;

            return sectorConfig.IsUnlocked;
        }

        return false;
    }

    private RouteConfig FindRouteConfig(string fromSystemId, string toSystemId)
    {
        long startedAt = BeginPerfMeasure();

        int sectorCount = 0;
        int systemCount = 0;
        int routeCount = 0;

        foreach (SectorConfig sectorConfig in _configService.GetAllSectors())
        {
            sectorCount++;

            if (sectorConfig == null || sectorConfig.Systems == null)
                continue;

            foreach (StarSystemConfig systemConfig in sectorConfig.Systems)
            {
                systemCount++;

                if (systemConfig == null || systemConfig.Routes == null)
                    continue;

                foreach (RouteConfig routeConfig in systemConfig.Routes)
                {
                    routeCount++;

                    if (routeConfig == null)
                        continue;

                    if (routeConfig.ConnectsSystems(fromSystemId, toSystemId))
                    {
                        LogRoutePerf(
                            EndPerfMeasureMs(startedAt),
                            "FindRouteConfig OK" +
                            " | From=" + fromSystemId +
                            " | To=" + toSystemId +
                            " | Sectors=" + sectorCount +
                            " | Systems=" + systemCount +
                            " | Routes=" + routeCount);

                        return routeConfig;
                    }
                }
            }
        }

        LogRoutePerf(
            EndPerfMeasureMs(startedAt),
            "FindRouteConfig FAILED" +
            " | From=" + fromSystemId +
            " | To=" + toSystemId +
            " | Sectors=" + sectorCount +
            " | Systems=" + systemCount +
            " | Routes=" + routeCount);

        return null;
    }

    private RouteConfig FindRouteConfigById(string routeId)
    {
        foreach (SectorConfig sectorConfig in _configService.GetAllSectors())
        {
            if (sectorConfig == null || sectorConfig.Systems == null)
                continue;

            foreach (StarSystemConfig systemConfig in sectorConfig.Systems)
            {
                if (systemConfig == null || systemConfig.Routes == null)
                    continue;

                foreach (RouteConfig routeConfig in systemConfig.Routes)
                {
                    if (routeConfig == null)
                        continue;

                    if (routeConfig.Id == routeId)
                        return routeConfig;
                }
            }
        }

        return null;
    }

    public string FindRouteId(string fromSystemId, string toSystemId)
    {
        long startedAt = BeginPerfMeasure();

        if (_configService.GalaxyConfig == null ||
            _configService.GetAllSectors() == null)
        {
            return string.Empty;
        }

        int sectorCount = 0;
        int systemCount = 0;
        int routeCount = 0;

        foreach (SectorConfig sectorConfig in _configService.GetAllSectors())
        {
            sectorCount++;

            if (sectorConfig == null || sectorConfig.Systems == null)
                continue;

            foreach (StarSystemConfig systemConfig in sectorConfig.Systems)
            {
                systemCount++;

                if (systemConfig == null || systemConfig.Routes == null)
                    continue;

                foreach (RouteConfig routeConfig in systemConfig.Routes)
                {
                    routeCount++;

                    if (routeConfig == null)
                        continue;

                    bool directRoute =
                        routeConfig.FromSystem.Id == fromSystemId &&
                        routeConfig.ToSystem.Id == toSystemId;

                    bool reverseRoute =
                        routeConfig.FromSystem.Id == toSystemId &&
                        routeConfig.ToSystem.Id == fromSystemId;

                    if (directRoute || reverseRoute)
                    {
                        LogRoutePerf(
                            EndPerfMeasureMs(startedAt),
                            "FindRouteId OK" +
                            " | From=" + fromSystemId +
                            " | To=" + toSystemId +
                            " | RouteId=" + routeConfig.Id +
                            " | Sectors=" + sectorCount +
                            " | Systems=" + systemCount +
                            " | Routes=" + routeCount);

                        return routeConfig.Id;
                    }
                }
            }
        }

        LogRoutePerf(
            EndPerfMeasureMs(startedAt),
            "FindRouteId FAILED" +
            " | From=" + fromSystemId +
            " | To=" + toSystemId +
            " | Sectors=" + sectorCount +
            " | Systems=" + systemCount +
            " | Routes=" + routeCount);

        return string.Empty;
    }

    public void UnlockRoute(string routeId)
    {
        RouteRuntimeState routeState = FindRouteState(routeId);

        if (routeState == null)
        {
            LogCustom("[RouteService] Route not found: " + routeId);
            return;
        }

        if (routeState.IsUnlocked)
            return;

        routeState.IsUnlocked = true;

        _eventBus.Publish(new RouteUnlockedEvent(routeId));

        LogCustom("[RouteService] Route unlocked: " + routeId);
    }

    private RouteRuntimeState FindRouteState(string routeId)
    {
        GalaxyRuntimeStateUpdate();

        if (_galaxyRuntimeState == null || _galaxyRuntimeState.Routes == null)
            return null;

        return _galaxyRuntimeState.Routes.FirstOrDefault(
            route => route.RouteId == routeId
        );
    }

    private static long BeginPerfMeasure()
    {
        return System.Diagnostics.Stopwatch.GetTimestamp();
    }

    private static double EndPerfMeasureMs(long startedAt)
    {
        long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startedAt;
        return elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }

    private void LogRoutePerf(double elapsedMs, string message)
    {
        if (elapsedMs < PerfLogThresholdMs)
            return;

        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(DebugLogPerformanceArea.GalaxyRoute))
        {
            return;
        }

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.GalaxyRoute,
            "[RouteService] " +
            message +
            " | Ms=" +
            elapsedMs.ToString("F2"));
    }
}