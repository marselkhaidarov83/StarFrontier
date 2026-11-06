using System;
using UnityEngine;

public interface IStationCombatService2A
{
    GalaxyCombatTarget FindTarget(SystemNpcRuntimeState attacker, SystemNpcBehaviorType? behavior = null);
    bool TryGetPosition(string systemId, string stationId, out Vector3 position);
    int ApplyDamage(string systemId, string stationId, int damage, string shooterId, string weaponId, string kind, string attackId);
}

/// <summary>Station target adapter for the ordinary NPC weapon pipeline. Never selects unbuilt/destroyed infrastructure.</summary>
public sealed class StationCombatService2A : IStationCombatService2A
{
    private readonly IGameSessionService _session;
    private readonly Func<string, StarSystemConfig> _resolve;
    private readonly IStationService _stations;
    private readonly IDamageService2A _damage;
    private readonly Func<string, int, bool> _applyInfrastructureDamage;
    private readonly SimpleEventBus _events;
    public StationCombatService2A() : this(Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>(),
        Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>().GetStarSystemConfigById,
        Bootstrapper.Instance.ServiceRegistry.Get<IStationService>(), Bootstrapper.Instance.ServiceRegistry.Get<IDamageService2A>(),
        (id, amount) => Bootstrapper.Instance.ServiceRegistry.Get<ISystemSecurityService>().ApplyInfrastructureDamageFromWar(id, amount, false),
        Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>()) { }
    public StationCombatService2A(IGameSessionService session, Func<string, StarSystemConfig> resolve,
        IStationService stations, IDamageService2A damage, Func<string, int, bool> applyDamage, SimpleEventBus events)
    { _session = session; _resolve = resolve; _stations = stations; _damage = damage; _applyInfrastructureDamage = applyDamage; _events = events; }

    public GalaxyCombatTarget FindTarget(SystemNpcRuntimeState attacker, SystemNpcBehaviorType? behavior = null)
    {
        if (attacker == null || !attacker.IsAlive || !attacker.IsEnemy || string.IsNullOrEmpty(attacker.CurrentSystemId) ||
            !StationAttackRules2A.TryGetStationType(behavior ?? attacker.CurrentBehavior, out var type)) return GalaxyCombatTarget.None();
        string id = _resolve(attacker.CurrentSystemId)?.Station?.Id;
        if (!TryResolve(attacker.CurrentSystemId, id, out var station, out var position) || station.StationType != type)
            return GalaxyCombatTarget.None();
        position.z = attacker.CurrentPosition.z;
        return GalaxyCombatTarget.Station(station.ConfigId, position);
    }
    private bool TryResolve(string systemId, string stationId, out StationRuntimeState station, out Vector3 position)
    {
        station = null; position = Vector3.zero;
        if (string.IsNullOrEmpty(systemId) || string.IsNullOrEmpty(stationId)) return false;
        var system = _session.State?.Galaxy?.Systems?.Find(x => x != null && x.SystemId == systemId);
        var config = _resolve(systemId)?.Station;
        if (system == null || system.SystemStatus == StarSystemStatus.Captured || config == null || config.Id != stationId ||
            !_stations.TryGetStation(systemId, out station) || station == null || station.ConfigId != stationId || !StationAttackRules2A.CanTarget(station)) return false;
        position = config.LocalOffset; position.z = 0; return true;
    }
    public bool TryGetPosition(string systemId, string stationId, out Vector3 position) => TryResolve(systemId, stationId, out _, out position);
    public int ApplyDamage(string systemId, string stationId, int damage, string shooterId, string weaponId, string kind, string attackId)
    {
        if (damage <= 0 || !TryResolve(systemId, stationId, out var before, out _)) return 0;
        var result = _damage.ApplyDamage(0, before.Health, damage);
        if (result.AppliedDamage <= 0 || !_applyInfrastructureDamage(systemId, result.AppliedDamage)) return 0;
        var after = _session.State.Galaxy.Systems.Find(x => x != null && x.SystemId == systemId)?.Station;
        int applied = Math.Max(0, before.Health - (after?.Health ?? before.Health));
        if (applied <= 0) return 0;
        _stations.ReconcileSystem(systemId);
        AppLog.Info(DebugLogChannel.Damage, "[STATION_DAMAGE_RESULT] System=" + systemId + " | Station=" + stationId +
            " | Shooter=" + shooterId + " | Weapon=" + weaponId + " | Kind=" + kind + " | AttackId=" + attackId +
            " | Requested=" + damage + " | Applied=" + applied + " | HealthBefore=" + before.Health +
            " | HealthAfter=" + after.Health + " | Destroyed=" + after.IsDestroyed);
        if (after.IsDestroyed) AppLog.Info(DebugLogChannel.StationEffects, "[STATION_DESTROYED_BY_NPC] System=" + systemId +
            " | Station=" + stationId + " | Shooter=" + shooterId + " | AttackId=" + attackId);
        _events?.PublishSafely(new SaveNeedEvent(after.IsDestroyed ? "station_destroyed_by_npc" : "station_hit_by_npc"),
            e => AppLog.Warning(DebugLogChannel.Damage, "[StationCombat] Save observer failed | " + e.Message));
        return applied;
    }
}
