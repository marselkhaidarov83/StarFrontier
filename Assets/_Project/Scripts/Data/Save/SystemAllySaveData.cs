using System;
using UnityEngine;

    [Serializable]
    public sealed class SystemAllySaveData
    {
        public string RuntimeAllyId;
        public string AllyConfigId;
        public string SystemId;

        public Vector3 Position;

        public int CurrentHull;
        public int CurrentShield;
        public int CurrentEnergy;

        public int Speed;
        public float Acceleration;
        public float TurnRate;
        public int CargoCapacity;

        public bool IsAlive;
    }
