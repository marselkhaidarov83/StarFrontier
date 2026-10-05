using System.Collections.Generic;
using UnityEngine;

public sealed class GalaxyNpcSimulationScheduleService :
    CustomService,
    IGalaxyNpcSimulationScheduleService
{
    private const int FallbackOffscreenSystemsPerTick = 3;

    private readonly IConfigService _configService;
    private readonly IGameSessionService _gameSessionService;
    private readonly IRouteService _routeService;
    private readonly SimpleEventBus _eventBus;

    private readonly Dictionary<int, List<StarSystemConfig>> _systemsByPriority =
        new();

    private readonly Dictionary<int, int> _priorityCursors =
        new();

    private readonly Dictionary<string, int> _distanceBySystemId =
        new();

    private readonly Queue<string> _distanceQueue =
        new();

    private readonly List<StarSystemConfig> _selectedOffscreenSystems =
        new();

    private readonly List<StarSystemConfig> _initialWarmupOffscreenSystems =
        new();

    private readonly List<int> _prioritySortBuffer =
        new();

    private int _scheduleTickIndex;
    private int _fallbackCursor;
    private int _lastSelectionQuantTick = int.MinValue;
    private string _prioritySourceSystemId = string.Empty;
    private bool _initialWarmupComplete;

    public string CurrentSystemId => GetCurrentSystemId();

    public string PrioritySourceSystemId => _prioritySourceSystemId;
    public bool IsInitialWarmupComplete => _initialWarmupComplete;

    public GalaxyNpcSimulationScheduleService()
    {
        _debugStop = true;

        _configService =
            Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();

        _gameSessionService =
            Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();

        _routeService =
            Bootstrapper.Instance.ServiceRegistry.Get<IRouteService>();

        _eventBus =
            Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        _eventBus?.Subscribe<StarSystemEnteredEvent>(OnStarSystemEntered);
    }

    public IReadOnlyList<StarSystemConfig> GetScheduledOffscreenSystems(
        int quantTick)
    {
        if (_configService == null)
            return _selectedOffscreenSystems;

        IReadOnlyList<StarSystemConfig> allSystems =
            _configService.GetAllStarSystems();

        if (allSystems == null || allSystems.Count == 0)
        {
            _selectedOffscreenSystems.Clear();
            return _selectedOffscreenSystems;
        }

        string currentSystemId = GetCurrentSystemId();

        EnsurePriorityIndex(currentSystemId, allSystems);

        if (_lastSelectionQuantTick == quantTick)
            return _selectedOffscreenSystems;

        _selectedOffscreenSystems.Clear();

        CollectScheduledOffscreenSystems(currentSystemId, allSystems);

        _lastSelectionQuantTick = quantTick;

        return _selectedOffscreenSystems;
    }

    public StarSystemConfig GetCurrentSystem()
    {
        if (_configService == null)
            return null;

        IReadOnlyList<StarSystemConfig> allSystems =
            _configService.GetAllStarSystems();

        return FindSystemById(
            allSystems,
            GetCurrentSystemId());
    }

    private void OnStarSystemEntered(StarSystemEnteredEvent eventData)
    {
        if (eventData == null ||
            string.IsNullOrWhiteSpace(eventData.SystemId))
        {
            return;
        }

        if (_prioritySourceSystemId == eventData.SystemId &&
            _systemsByPriority.Count > 0)
        {
            LogCustom(
                "[GalaxyNpcSimulationScheduleService] StarSystemEntered ignored. " +
                "Priority index already belongs to this system. " +
                "SystemId=" + eventData.SystemId +
                ", InitialWarmupComplete=" + _initialWarmupComplete);

            return;
        }

        IReadOnlyList<StarSystemConfig> allSystems =
            _configService != null
                ? _configService.GetAllStarSystems()
                : null;

        RebuildPriorityIndex(eventData.SystemId, allSystems);
    }

    private void EnsurePriorityIndex(
        string currentSystemId,
        IReadOnlyList<StarSystemConfig> allSystems)
    {
        if (string.IsNullOrWhiteSpace(currentSystemId))
            return;

        if (_prioritySourceSystemId == currentSystemId &&
            _systemsByPriority.Count > 0)
        {
            return;
        }

        RebuildPriorityIndex(currentSystemId, allSystems);
    }

    private void RebuildPriorityIndex(
        string currentSystemId,
        IReadOnlyList<StarSystemConfig> allSystems)
    {
        _systemsByPriority.Clear();
        _priorityCursors.Clear();
        _distanceBySystemId.Clear();
        _distanceQueue.Clear();
        _selectedOffscreenSystems.Clear();
        _initialWarmupOffscreenSystems.Clear();
        _prioritySortBuffer.Clear();

        _scheduleTickIndex = 0;
        _fallbackCursor = 0;
        _lastSelectionQuantTick = int.MinValue;
        _prioritySourceSystemId = currentSystemId ?? string.Empty;
        _initialWarmupComplete = false;

        if (string.IsNullOrWhiteSpace(currentSystemId) ||
            allSystems == null ||
            allSystems.Count == 0)
        {
            return;
        }

        BuildJumpDistances(currentSystemId);

        int unreachablePriority = GetUnreachableSystemPriority();

        for (int i = 0; i < allSystems.Count; i++)
        {
            StarSystemConfig system = allSystems[i];

            if (system == null ||
                string.IsNullOrWhiteSpace(system.Id) ||
                system.Id == currentSystemId)
            {
                continue;
            }

            int priority =
                _distanceBySystemId.TryGetValue(system.Id, out int distance)
                    ? Mathf.Max(1, distance)
                    : unreachablePriority;

            if (!_systemsByPriority.TryGetValue(
                    priority,
                    out List<StarSystemConfig> systems))
            {
                systems = new List<StarSystemConfig>();
                _systemsByPriority[priority] = systems;
            }

            systems.Add(system);
        }

        SortPriorityBuckets();

        LogCustom(
            "[GalaxyNpcSimulationScheduleService] Priority index rebuilt. " +
            "CurrentSystemId=" + currentSystemId +
            ", Priorities=" + _systemsByPriority.Count +
            ", InitialWarmupComplete=" + _initialWarmupComplete);
    }

    private void BuildJumpDistances(string currentSystemId)
    {
        _distanceBySystemId[currentSystemId] = 0;
        _distanceQueue.Enqueue(currentSystemId);

        while (_distanceQueue.Count > 0)
        {
            string systemId = _distanceQueue.Dequeue();

            if (!_distanceBySystemId.TryGetValue(
                    systemId,
                    out int currentDistance))
            {
                continue;
            }

            StarSystemConfig system =
                _configService.GetStarSystemConfigById(systemId);

            if (system == null || system.Routes == null)
                continue;

            for (int i = 0; i < system.Routes.Count; i++)
            {
                RouteConfig route = system.Routes[i];

                if (route == null)
                    continue;

                StarSystemConfig otherSystem =
                    route.GetOtherSystem(systemId);

                if (otherSystem == null ||
                    string.IsNullOrWhiteSpace(otherSystem.Id))
                {
                    continue;
                }

                if (_distanceBySystemId.ContainsKey(otherSystem.Id))
                    continue;

                if (_routeService != null &&
                    !_routeService.HasUnlockedRoute(systemId, otherSystem.Id))
                {
                    continue;
                }

                _distanceBySystemId[otherSystem.Id] = currentDistance + 1;
                _distanceQueue.Enqueue(otherSystem.Id);
            }
        }
    }

    private void SortPriorityBuckets()
    {
        foreach (KeyValuePair<int, List<StarSystemConfig>> pair
                 in _systemsByPriority)
        {
            pair.Value.Sort(CompareSystemsById);
        }
    }

    private void CollectScheduledOffscreenSystems(
        string currentSystemId,
        IReadOnlyList<StarSystemConfig> allSystems)
    {
        OffscreenNpcSimulationScheduleTick scheduleTick =
            GetNextScheduleTick();

        if (scheduleTick == null)
        {
            CollectFallbackOffscreenSystems(currentSystemId, allSystems);
            return;
        }

        if (!_systemsByPriority.TryGetValue(
                scheduleTick.Priority,
                out List<StarSystemConfig> prioritySystems))
        {
            return;
        }

        if (prioritySystems == null || prioritySystems.Count == 0)
            return;

        if (scheduleTick.ProcessAll)
        {
            for (int i = 0; i < prioritySystems.Count; i++)
            {
                _selectedOffscreenSystems.Add(prioritySystems[i]);
            }

            return;
        }

        int systemsToProcess =
            Mathf.Min(scheduleTick.MaxSystems, prioritySystems.Count);

        int cursor = GetPriorityCursor(scheduleTick.Priority);

        for (int i = 0; i < systemsToProcess; i++)
        {
            StarSystemConfig system = prioritySystems[cursor];

            _selectedOffscreenSystems.Add(system);

            cursor++;

            if (cursor >= prioritySystems.Count)
                cursor = 0;
        }

        _priorityCursors[scheduleTick.Priority] = cursor;
    }

    private OffscreenNpcSimulationScheduleTick GetNextScheduleTick()
    {
        OffscreenNpcSimulationScheduleConfig config =
            GetScheduleConfig();

        if (config == null ||
            config.Ticks == null ||
            config.Ticks.Length == 0)
        {
            return null;
        }

        int index = _scheduleTickIndex % config.Ticks.Length;
        _scheduleTickIndex++;

        return config.Ticks[index];
    }

    private void CollectFallbackOffscreenSystems(
        string currentSystemId,
        IReadOnlyList<StarSystemConfig> allSystems)
    {
        if (allSystems == null || allSystems.Count == 0)
            return;

        int safety = 0;

        while (_selectedOffscreenSystems.Count < FallbackOffscreenSystemsPerTick &&
               safety < allSystems.Count)
        {
            if (_fallbackCursor >= allSystems.Count)
                _fallbackCursor = 0;

            StarSystemConfig system = allSystems[_fallbackCursor];

            _fallbackCursor++;
            safety++;

            if (system == null ||
                string.IsNullOrWhiteSpace(system.Id) ||
                system.Id == currentSystemId)
            {
                continue;
            }

            _selectedOffscreenSystems.Add(system);
        }
    }

    private int GetPriorityCursor(int priority)
    {
        if (_priorityCursors.TryGetValue(priority, out int cursor))
            return Mathf.Max(0, cursor);

        return 0;
    }

    private int GetUnreachableSystemPriority()
    {
        OffscreenNpcSimulationScheduleConfig config =
            GetScheduleConfig();

        if (config == null)
            return 99;

        return config.UnreachableSystemPriority;
    }

    private OffscreenNpcSimulationScheduleConfig GetScheduleConfig()
    {
        if (Bootstrapper.Instance == null)
            return null;

        return Bootstrapper.Instance.OffscreenNpcSimulationScheduleConfig;
    }

    private StarSystemConfig FindSystemById(
        IReadOnlyList<StarSystemConfig> systems,
        string systemId)
    {
        if (systems == null || string.IsNullOrWhiteSpace(systemId))
            return null;

        for (int i = 0; i < systems.Count; i++)
        {
            StarSystemConfig system = systems[i];

            if (system != null && system.Id == systemId)
                return system;
        }

        return null;
    }

    private string GetCurrentSystemId()
    {
        if (_gameSessionService == null ||
            _gameSessionService.State == null ||
            _gameSessionService.State.Player == null)
        {
            return string.Empty;
        }

        return _gameSessionService.State.Player.CurrentSystemId ?? string.Empty;
    }

    private static int CompareSystemsById(
        StarSystemConfig left,
        StarSystemConfig right)
    {
        string leftId =
            left != null && left.Id != null
                ? left.Id
                : string.Empty;

        string rightId =
            right != null && right.Id != null
                ? right.Id
                : string.Empty;

        return string.CompareOrdinal(leftId, rightId);
    }

    public IReadOnlyList<StarSystemConfig> GetInitialWarmupOffscreenSystems()
    {
        _initialWarmupOffscreenSystems.Clear();

        if (_configService == null)
            return _initialWarmupOffscreenSystems;

        IReadOnlyList<StarSystemConfig> allSystems =
            _configService.GetAllStarSystems();

        if (allSystems == null || allSystems.Count == 0)
            return _initialWarmupOffscreenSystems;

        string currentSystemId = GetCurrentSystemId();

        EnsurePriorityIndex(currentSystemId, allSystems);

        CollectInitialWarmupOffscreenSystems();

        return _initialWarmupOffscreenSystems;
    }

    public void CompleteInitialWarmup()
    {
        _initialWarmupComplete = true;
    }

    private void CollectInitialWarmupOffscreenSystems()
    {
        _prioritySortBuffer.Clear();

        foreach (KeyValuePair<int, List<StarSystemConfig>> pair
                 in _systemsByPriority)
        {
            _prioritySortBuffer.Add(pair.Key);
        }

        _prioritySortBuffer.Sort();

        for (int priorityIndex = 0;
             priorityIndex < _prioritySortBuffer.Count;
             priorityIndex++)
        {
            int priority = _prioritySortBuffer[priorityIndex];

            if (!_systemsByPriority.TryGetValue(
                    priority,
                    out List<StarSystemConfig> systems))
            {
                continue;
            }

            if (systems == null || systems.Count == 0)
                continue;

            for (int i = 0; i < systems.Count; i++)
            {
                StarSystemConfig system = systems[i];

                if (system == null ||
                    string.IsNullOrWhiteSpace(system.Id))
                {
                    continue;
                }

                _initialWarmupOffscreenSystems.Add(system);
            }
        }
    }
}