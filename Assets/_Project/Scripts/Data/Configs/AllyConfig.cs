using UnityEngine;

    [CreateAssetMenu(fileName = "AllyConfig", menuName = "StarFrontier/Configs/Ally")]
    public sealed class AllyConfig : BaseConfig
    {
        [Header("Base Stats")]
        [SerializeField] private int baseHull = 50;
        [SerializeField] private int baseShield = 20;
        [SerializeField] private int baseEnergy = 50;
        [SerializeField] private float baseSpeed = 2f;

        [Header("Combat")]
        [SerializeField] private WeaponConfig weaponConfig;

        [Header("Visuals")]
        [SerializeField] private Sprite mapSprite;

        public int BaseHull => baseHull;
        public int BaseShield => baseShield;
        public int BaseEnergy => baseEnergy;
        public float BaseSpeed => baseSpeed;
        public WeaponConfig WeaponConfig => weaponConfig;
        public Sprite MapSprite => mapSprite;
    }