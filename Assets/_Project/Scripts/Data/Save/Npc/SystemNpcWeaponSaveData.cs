using System;

[Serializable]
    public sealed class SystemNpcWeaponSaveData
    {
        public string WeaponConfigId;
        public int LastShotTick;
        public float CooldownRemainingSeconds;
    }