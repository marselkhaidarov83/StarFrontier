using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class SystemAllyService : ISystemAllyService
{
    private readonly List<SystemAllyRuntimeState> _allies = new();

    private readonly SimpleEventBus _eventBus;
    private readonly ISystemEncounterService _encounterService;
    private readonly IDamageService2A _damageService;

    public IReadOnlyList<SystemAllyRuntimeState> Allies => _allies;

    public SystemAllyService()
    {
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _encounterService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEncounterService>();
        _damageService = ResolveDamageService();
    }

    public SystemAllyRuntimeState CreateAlly(
        AllyConfig allyConfig,
        string systemId,
        Vector3 position)
    {
        if (allyConfig == null)
            throw new ArgumentNullException(nameof(allyConfig));

        if (string.IsNullOrWhiteSpace(allyConfig.Id))
            throw new ArgumentException("AllyConfig.Id is empty.", nameof(allyConfig));

        if (string.IsNullOrWhiteSpace(systemId))
            throw new ArgumentException("SystemId is empty.", nameof(systemId));

        var ally = new SystemAllyRuntimeState
        {
            RuntimeAllyId = Guid.NewGuid().ToString("N"),

            AllyConfigId = allyConfig.Id,
            AllyConfig = allyConfig,

            SystemId = systemId,
            Position = position,

            CurrentHull = RandomInt(allyConfig.BaseHullMin, allyConfig.BaseHullMax),
            CurrentShield = RandomInt(allyConfig.BaseShieldMin, allyConfig.BaseShieldMax),
            CurrentEnergy = RandomInt(allyConfig.BaseEnergyMin, allyConfig.BaseEnergyMax),
            Speed = RandomInt(allyConfig.BaseSpeedMin, allyConfig.BaseSpeedMax),
            Acceleration = RandomFloat(allyConfig.BaseAccelerationMin, allyConfig.BaseAccelerationMax),
            TurnRate = RandomFloat(allyConfig.BaseTurnRateMin, allyConfig.BaseTurnRateMax),
            CargoCapacity = RandomInt(allyConfig.BaseCargoCapacityMin, allyConfig.BaseCargoCapacityMax),

            IsAlive = true
        };

        _allies.Add(ally);

        _eventBus.Publish(new SystemAllyCreatedEvent(
            ally.RuntimeAllyId,
            ally.AllyConfigId,
            ally.SystemId,
            ally.Position));

        return ally;
    }

    public bool TryGetAlly(string runtimeAllyId, out SystemAllyRuntimeState ally)
    {
        ally = _allies.FirstOrDefault(x => x.RuntimeAllyId == runtimeAllyId);
        return ally != null;
    }

    public IReadOnlyList<SystemAllyRuntimeState> GetAliveAlliesInSystem(string systemId)
    {
        return _allies
            .Where(x => x.SystemId == systemId && x.IsAlive)
            .ToList();
    }

    public void UpdateAllyPosition(string runtimeAllyId, Vector3 position)
    {
        if (!TryGetAlly(runtimeAllyId, out var ally))
            return;

        if (!ally.IsAlive)
            return;

        ally.Position = position;

        _eventBus.Publish(new SystemAllyPositionChangedEvent(
            ally.RuntimeAllyId,
            ally.Position));
    }

    public void ApplyDamage(string runtimeAllyId, int damage)
    {
        if (!TryGetAlly(runtimeAllyId, out var ally))
            return;

        if (!ally.IsAlive)
            return;

        CombatDamageResult2A result = _damageService.ApplyDamage(
            ally.CurrentShield,
            ally.CurrentHull,
            damage);

        if (result.AppliedDamage <= 0)
            return;

        ally.CurrentShield = result.CurrentShield;
        ally.CurrentHull = result.CurrentHull;

        _eventBus.Publish(new SystemAllyDamagedEvent(
            ally.RuntimeAllyId,
            result.AppliedDamage,
            ally.CurrentHull,
            ally.CurrentShield));

        if (result.IsDestroyed)
            DestroyAlly(ally);
    }

    public void ClearSystemAllies(string systemId)
    {
        _allies.RemoveAll(x => x.SystemId == systemId);
    }

    public void ClearAll()
    {
        _allies.Clear();
    }

    public void RestoreAlly(SystemAllyRuntimeState ally)
    {
        if (ally == null)
            return;

        _allies.RemoveAll(x => x.RuntimeAllyId == ally.RuntimeAllyId);
        _allies.Add(ally);

        _eventBus.Publish(new SystemAllyCreatedEvent(
            ally.RuntimeAllyId,
            ally.AllyConfigId,
            ally.SystemId,
            ally.Position));
    }

    public void RestoreAllies(IEnumerable<SystemAllyRuntimeState> allies)
    {
        if (allies == null)
            return;

        foreach (var ally in allies)
            RestoreAlly(ally);
    }

    private void DestroyAlly(SystemAllyRuntimeState ally)
    {
        if (!ally.IsAlive)
            return;

        ally.IsAlive = false;
        ally.CurrentHull = 0;

        _encounterService.RegisterAllyDestroyed();

        _eventBus.Publish(new SystemAllyDestroyedEvent(
            ally.RuntimeAllyId,
            ally.AllyConfigId,
            ally.SystemId));
    }

    private static IDamageService2A ResolveDamageService()
    {
        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null &&
            Bootstrapper.Instance.ServiceRegistry.TryGet(out IDamageService2A damageService))
        {
            return damageService;
        }

        return new DamageService2A();
    }

    private static int RandomInt(int min, int max)
    {
        int safeMin = Mathf.Min(min, max);
        int safeMax = Mathf.Max(min, max);
        return UnityEngine.Random.Range(safeMin, safeMax + 1);
    }

    private static float RandomFloat(float min, float max)
    {
        float safeMin = Mathf.Min(min, max);
        float safeMax = Mathf.Max(min, max);
        return UnityEngine.Random.Range(safeMin, safeMax);
    }
}
