using System;
using UnityEngine;

    [Serializable]
    public sealed class SystemEnemyRuntimeState
    {
        public string RuntimeEnemyId;

        public string EnemyConfigId;
        public EnemyConfig EnemyConfig;

        public string SystemId;

        public Vector3 Position;

        public int CurrentHull;
        public int CurrentShield;
        public int CurrentEnergy;

        public bool IsAlive;
        public bool WasKilledByPlayer;
    }