using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

    public sealed class SystemAllyService : ISystemAllyService
    {
        private readonly List<SystemAllyRuntimeState> _allies = new();

        private readonly SimpleEventBus _eventBus;
        private readonly ISystemEncounterService _encounterService;

        public IReadOnlyList<SystemAllyRuntimeState> Allies => _allies;

        public SystemAllyService()
        {
            _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
            _encounterService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEncounterService>();
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

                CurrentHull = allyConfig.BaseHull,
                CurrentShield = allyConfig.BaseShield,
                CurrentEnergy = allyConfig.BaseEnergy,

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
            if (damage <= 0)
                return;

            if (!TryGetAlly(runtimeAllyId, out var ally))
                return;

            if (!ally.IsAlive)
                return;

            int remainingDamage = damage;

            if (ally.CurrentShield > 0)
            {
                int shieldDamage = Mathf.Min(ally.CurrentShield, remainingDamage);
                ally.CurrentShield -= shieldDamage;
                remainingDamage -= shieldDamage;
            }

            if (remainingDamage > 0)
                ally.CurrentHull -= remainingDamage;

            _eventBus.Publish(new SystemAllyDamagedEvent(
                ally.RuntimeAllyId,
                damage,
                ally.CurrentHull,
                ally.CurrentShield));

            Debug.Log(
                $"[SystemAllyService] Damage: {damage}, Ally: {ally.AllyConfigId}, Hull: {ally.CurrentHull}, Shield: {ally.CurrentShield}"
            );

            if (ally.CurrentHull <= 0)
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
}