using System;
using UnityEngine;

    [Serializable]
    public sealed class SystemAllyRuntimeState
    {
        public string RuntimeAllyId;

        public string AllyConfigId;
        public AllyConfig AllyConfig;

        public string SystemId;

        public Vector3 Position;

        public int CurrentHull;
        public int CurrentShield;
        public int CurrentEnergy;

        public bool IsAlive;
    }