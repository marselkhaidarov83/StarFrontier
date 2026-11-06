using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class StationInfrastructureService : IStationInfrastructureService
{
    private readonly IGameSessionService _session;
    private readonly IConfigService _config;
    private readonly ISystemNpcRuntimeService _npcs;
    private readonly ISystemNpcPopulationService _population;
    private readonly IStationService _stations;
    private readonly SimpleEventBus _events;
    public StationInfrastructureService() : this(
        Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcRuntimeService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcPopulationService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<IStationService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>()) { }

    public StationInfrastructureService(IGameSessionService session, IConfigService config,
        ISystemNpcRuntimeService npcs, ISystemNpcPopulationService population,
        IStationService stations, SimpleEventBus events)
    {
        _session = session; _config = config; _npcs = npcs;
        _population = population; _stations = stations; _events = events;
    }
    public void InitializeSession()
    {
        var systems = _session.State?.Galaxy?.Systems;
        if (systems == null) return;
        foreach (var system in systems)
        {
            if (system == null) continue;
            var capturedSystem = system;
            system.HasLivingMilitary = () => _npcs.Npcs.Any(n => n != null && n.IsAlive && n.IsAlly &&
                n.AllyRole == AllyRole2A.Military && n.CurrentSystemId == capturedSystem.SystemId);
            // Legacy saves can contain an intact station in Captured; ownership implies its destruction.
            if (system.SystemStatus == StarSystemStatus.Captured && system.Station != null)
                StationLifecycleRules.Destroy(system.Station);
        }
        foreach (var sector in _config.GetAllSectors())
        {
            var types = new HashSet<StationType>();
            foreach (var config in sector.Systems ?? Array.Empty<StarSystemConfig>())
                if (config?.Station != null && !types.Add(config.Station.StationType))
                    throw new InvalidOperationException("Duplicate station type in sector: " + sector.Id);
        }
    }
    private StationLevelEffectsSettings Settings(StarSystemRuntimeState system) =>
        system?.Station == null ? null : _config.GetStarSystemConfigById(system.SystemId)?.Station?.LevelEffects?.GetLevel(system.Station.Level);
    public float GetRangerAttackProbability(string systemId, out bool configured)
    {
        configured = false;
        var sector = StationMilitaryRules.Sector(_config.GetAllSectors(), systemId);
        if (sector == null) return 0;
        foreach (var config in sector.Systems ?? Array.Empty<StarSystemConfig>())
        {
            if (config?.Station == null || config.Station.StationType != StationType.RangerBase) continue;
            var system = _session.State?.Galaxy?.Systems?.Find(x => x != null && x.SystemId == config.Id);
            var settings = Settings(system) ?? config.Station.LevelEffects?.GetLevel(1);
            if (settings == null || system?.Station == null || !system.Station.IsOperational ||
                system.Station.ConfigMissing || system.SystemStatus == StarSystemStatus.Captured) return 0;
            configured = true;
            return Mathf.Clamp01(settings.RangerAttackProbability * StationInfrastructureRules.Efficiency(system, settings));
        }
        return 0;
    }
    public double GetScienceRate()
    {
        double rate = 0;
        foreach (var system in _session.State?.Galaxy?.Systems ?? new List<StarSystemRuntimeState>())
        {
            if (system?.Station?.StationType != StationType.Science) continue;
            var settings = Settings(system);
            if (settings == null || float.IsNaN(settings.ScienceRate) || float.IsInfinity(settings.ScienceRate)) continue;
            rate += Math.Max(0, settings.ScienceRate) * StationInfrastructureRules.Efficiency(system, settings);
        }
        return rate;
    }
    public bool CanTravel(SystemNpcRuntimeState npc, string targetSystemId) =>
        StationMilitaryRules.CanTravel(npc, targetSystemId, _config.GetAllSectors(), _session.State?.Galaxy);
    public double NextRepairBoundary(double maximumSeconds)
    {
        double tickSeconds = GameTimeState.SecondsPerDay;
        double phase = _session.State?.Galaxy?.StationOfflineRepairSeconds ?? 0;
        return Math.Min(maximumSeconds, Math.Max(0.000001, tickSeconds - phase));
    }
    public void CompleteTick(int tick)
    {
        var galaxy = _session.State?.Galaxy;
        if (galaxy == null || tick <= galaxy.StationLastRepairTick) return;
        // Claim before notifying observers: re-entry cannot heal twice, and reload retains this marker.
        galaxy.StationLastRepairTick = tick;
        RepairCompletedTick("Tick=" + tick);
    }
    public void AdvanceOfflineRepair(double seconds)
    {
        var galaxy = _session.State?.Galaxy;
        double tickSeconds = GameTimeState.SecondsPerDay;
        if (galaxy == null || seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds) ||
            tickSeconds <= 0 || double.IsNaN(tickSeconds) || double.IsInfinity(tickSeconds)) return;
        galaxy.StationOfflineRepairSeconds += seconds;
        while (galaxy.StationOfflineRepairSeconds >= tickSeconds)
        {
            galaxy.StationOfflineRepairSeconds -= tickSeconds;
            RepairCompletedTick("Offline");
        }
    }
    private void RepairCompletedTick(string context)
    {
        foreach (var system in _session.State?.Galaxy?.Systems ?? new List<StarSystemRuntimeState>())
        {
            if (system?.Station == null) continue;
            var settings = Settings(system);
            bool enemies = _npcs.GetAliveNpcsInSystem(system.SystemId)
                .Any(n => n != null && n.IsAlive && (n.IsEnemy || n.IsHostileToPlayer));
            int previous = system.Station.Health;
            if (!StationInfrastructureRules.AutoRepair(system, settings, enemies)) continue;
            _stations?.ReconcileSystem(system.SystemId);
            AppLog.Info(DebugLogChannel.StationEffects, "[StationEffects] AUTO_REPAIR | " + context +
                " | System=" + system.SystemId + " | Health=" + previous + "->" + system.Station.Health +
                "/" + system.Station.MaxHealth + " | HealthPerTick=" + settings.HealthPerTick);
        }
    }
    public void AdvanceTime(double seconds)
    {
        if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
        foreach (var system in _session.State?.Galaxy?.Systems ?? new List<StarSystemRuntimeState>())
        {
            if (system?.Station == null) continue;
            var settings = Settings(system);
            if (system.Station.StationType == StationType.Military && settings != null)
                Populate(system, settings, seconds);
        }
    }
    private void Populate(StarSystemRuntimeState system, StationLevelEffectsSettings settings, double seconds)
    {
        float efficiency = StationInfrastructureRules.Efficiency(system, settings);
        if (efficiency <= 0 || settings.Population?.AllySpawnRules == null ||
            Bootstrapper.Instance.StopAutomaticAllySpawns) return;
        int galaxyLevel = Mathf.Clamp(_session.State.Galaxy.Sectors.Count(x => x != null && x.IsUnlocked), 1, 10);
        var home = _config.GetStarSystemConfigById(system.SystemId);
        foreach (var rule in settings.Population.AllySpawnRules)
        {
            if (rule == null || rule.GetSpawnIntervalSeconds(galaxyLevel) <= 0) continue;
            string source = StationMilitaryRules.RuleId(system.SystemId, home.Station.Id, rule.Id);
            foreach (var entry in rule.GetAlliesForGalaxyLevel(galaxyLevel))
            {
                if (entry?.AllyConfig == null || !entry.IsValid() || entry.AllyConfig.Role != AllyRole2A.Military) continue;
                string timerId = source + ":" + entry.AllyConfig.Id;
                bool initialized = _population.RuntimeState.Timers.Any(t => t != null && t.SystemId == system.SystemId && t.RuleId == timerId);
                var timer = _population.RuntimeState.GetOrCreateTimer(system.SystemId, timerId);
                timer.TimerSeconds += (float)(seconds * efficiency);
                float interval = rule.GetSpawnIntervalSeconds(galaxyLevel);
                int count = _npcs.Npcs.Count(n => n != null && n.IsAlive && n.ConfigId == entry.AllyConfig.Id &&
                    StationMilitaryRules.SourceSystem(n) == system.SystemId);
                int cycles = (int)Math.Min(entry.MaxCount, Math.Floor(timer.TimerSeconds / interval));
                timer.TimerSeconds %= interval;
                if (!initialized) cycles = Math.Max(cycles, entry.MinCount - count);
                int spawn = Math.Max(0, Math.Min(cycles, entry.MaxCount - count));
                for (int i = 0; i < spawn; i++)
                {
                    var npc = SystemNpcRuntimeFactory.CreateAlly(entry.AllyConfig, system.SystemId, system.SystemId,
                        string.Empty, home.Station.LocalOffset, source);
                    npc.IsOnPlanet = false; npc.CurrentPlanetId = null; npc.TravelState = SystemNpcTravelState.Idle;
                    npc.HasActiveBehavior = false; npc.CurrentBehavior = SystemNpcBehaviorType.None;
                    npc.CanChangeLocationOnRestore = true;
                    _npcs.AddNpc(npc);
                    Log("MILITARY_SPAWN", system.SystemId);
                }
            }
        }
    }
    public void RelocateStationMilitary()
    {
        if (_session.State?.Galaxy?.Systems == null)
        {
            AppLog.Info(DebugLogChannel.Save,
                "[StationMilitaryRestore] Deferred | Reason=SessionNotBound");
            return;
        }
        foreach (var npc in _npcs.Npcs)
        {
            if (!StationMilitaryRules.IsStationMilitary(npc) || !npc.IsAlive) continue;
            var sector = StationMilitaryRules.Sector(_config.GetAllSectors(), StationMilitaryRules.SourceSystem(npc));
            var candidates = sector?.Systems?.Where(s => s != null && CanTravel(npc, s.Id)).ToArray();
            if (candidates == null || candidates.Length == 0)
                throw new InvalidOperationException("No valid system for station military: " + npc.RuntimeNpcId);
            var destination = candidates[UnityEngine.Random.Range(0, candidates.Length)];
            npc.CurrentSystemId = destination.Id; npc.OriginSystemId = StationMilitaryRules.SourceSystem(npc);
            npc.CurrentPlanetId = null; npc.TargetPlanetId = null; npc.IsOnPlanet = false;
            npc.TargetSystemId = null; npc.TravelState = SystemNpcTravelState.Idle;
            Vector2 center = destination.Sun != null ? destination.Sun.LocalOffset : Vector2.zero;
            npc.CurrentPosition = destination.Station != null ? (Vector3)destination.Station.LocalOffset :
                (Vector3)(center + UnityEngine.Random.insideUnitCircle.normalized * 120f);
            npc.CurrentMovementTargetPosition = npc.CurrentPosition; npc.TickMovementTargetPosition = npc.CurrentPosition;
            npc.TargetSystemExitPoint = Vector3.zero; npc.TargetSystemEntryPoint = Vector3.zero;
            npc.TravelProgress01 = 0; npc.IsWaitingForInitialRouteBuild = false;
            npc.ReleaseFromPlanetAfterInitialRouteBuild = false; npc.InitialRouteBuildPlanetId = null;
            npc.StartPosition = npc.CurrentPosition; npc.TargetPosition = npc.CurrentPosition;
            npc.HasActiveBehavior = false; npc.CurrentBehavior = SystemNpcBehaviorType.None;
            npc.CombatState = SystemNpcCombatState.None; npc.CurrentTargetRuntimeNpcId = null;
            npc.IsFighting = false; npc.TickMovementDirectionTick = -1;
            SystemNpcMovementTime2A.Reset(npc, SystemNpcMovementTime2A.Now);
            Log("MILITARY_RELOCATE", destination.Id);
        }
    }
    private static void Log(string action, string systemId) => AppLog.Info(DebugLogChannel.StationEffects,
        "[StationEffects] " + action + " | System=" + systemId);
}
