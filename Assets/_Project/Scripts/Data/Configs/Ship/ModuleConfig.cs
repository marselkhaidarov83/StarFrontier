using System;
using UnityEngine;

// Конфиг ModuleConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[Serializable]
public struct StatModifierData
{
    [Tooltip("Параметр statType. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private ShipStatType statType;
    [Tooltip("Параметр modifierType. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private StatModifierType modifierType;
    [Tooltip("Параметр value. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float value;

    public ShipStatType StatType => statType;
    public StatModifierType ModifierType => modifierType;
    public float Value => value;
}

[CreateAssetMenu(fileName = "ModuleConfig", menuName = "StarFrontier/Configs/Ship/Module")]
public class ModuleConfig : BaseConfig
{
    [Header("Module Info")]
    [Tooltip("Параметр moduleType. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private ModuleType moduleType;
    [Tooltip("Параметр activationType. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private ModuleActivationType activationType;
    [Tooltip("Параметр slotType. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private ModuleSlotType slotType;
    [Tooltip("Параметр activeEffectType. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private ModuleActiveEffectType activeEffectType;
    [Tooltip("Параметр statType. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private ModuleStatType statType;
    [Tooltip("Параметр flatBonus. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private int flatBonus;
    [Tooltip("Параметр percentBonus. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float percentBonus;

    [Header("Active Module")]
    [Tooltip("Параметр cooldown. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float cooldown;
    [Tooltip("Параметр energyCost. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private int energyCost;

    [Header("Stat Modifiers")]
    [Tooltip("Параметр statModifiers. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private StatModifierData[] statModifiers;

    public ModuleType ModuleType => moduleType;
    public ModuleActivationType ActivationType => activationType;
    public ModuleSlotType SlotType => slotType;
    public ModuleActiveEffectType ActiveEffectType => activeEffectType;
    public ModuleStatType StatType => statType;
    public int FlatBonus => flatBonus;
    public float PercentBonus => percentBonus;
    public float Cooldown => cooldown;
    public int EnergyCost => energyCost;
    public StatModifierData[] StatModifiers => statModifiers;
}
