using System;

    [Serializable]
    public sealed class PlayerCombatRuntimeState
    {
        public int MaxHull;
        public int CurrentHull;

        public int MaxShield;
        public int CurrentShield;

        public int MaxEnergy;
        public int CurrentEnergy;

        public bool IsAlive;

        public void Init(int hull, int shield, int energy)
        {
            MaxHull = hull;
            CurrentHull = hull;

            MaxShield = shield;
            CurrentShield = shield;

            MaxEnergy = energy;
            CurrentEnergy = energy;

            IsAlive = true;
        }
    }