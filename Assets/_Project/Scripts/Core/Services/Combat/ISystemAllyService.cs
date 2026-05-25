using System.Collections.Generic;
using UnityEngine;

    public interface ISystemAllyService
    {
        IReadOnlyList<SystemAllyRuntimeState> Allies { get; }

        SystemAllyRuntimeState CreateAlly(
            AllyConfig allyConfig,
            string systemId,
            Vector3 position);

        void RestoreAlly(SystemAllyRuntimeState ally);
        void RestoreAllies(IEnumerable<SystemAllyRuntimeState> allies);

        bool TryGetAlly(string runtimeAllyId, out SystemAllyRuntimeState ally);

        IReadOnlyList<SystemAllyRuntimeState> GetAliveAlliesInSystem(string systemId);

        void UpdateAllyPosition(string runtimeAllyId, Vector3 position);

        void ApplyDamage(string runtimeAllyId, int damage);

        void ClearSystemAllies(string systemId);
        void ClearAll();
    }
