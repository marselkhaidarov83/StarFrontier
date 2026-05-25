using System;
using UnityEngine;

[Serializable]
public struct StatModifierData
{
    [SerializeField] private ShipStatType statType;
    [SerializeField] private StatModifierType modifierType;
    [SerializeField] private float value;

    public ShipStatType StatType => statType;
    public StatModifierType ModifierType => modifierType;
    public float Value => value;
}

[CreateAssetMenu(fileName = "ModuleConfig", menuName = "StarFrontier/Configs/Module")]
public class ModuleConfig : BaseConfig
{
    [Header("Module Info")]
    [SerializeField] private ModuleType moduleType;
    [SerializeField] private ModuleActivationType activationType;
    [SerializeField] private ModuleSlotType slotType;
    [SerializeField] private ModuleActiveEffectType activeEffectType;
    [SerializeField] private ModuleStatType statType;
    [SerializeField] private int flatBonus;
    [SerializeField] private float percentBonus;

    [Header("Active Module")]
    [SerializeField] private float cooldown;
    [SerializeField] private int energyCost;

    [Header("Stat Modifiers")]
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