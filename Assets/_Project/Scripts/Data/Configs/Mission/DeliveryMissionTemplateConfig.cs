using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class DeliveryCargoOption
{
    public ItemConfig Item;
    [Min(0)] public float Weight;
}

[CreateAssetMenu(fileName = "DeliveryMissionTemplateConfig", menuName = "StarFrontier/Configs/Mission/Delivery Template")]
public sealed class DeliveryMissionTemplateConfig : MissionTemplateConfig
{
    public override MissionType MissionType => global::MissionType.Delivery;
    [Header("Награда за межсистемную доставку")]
    [Tooltip("Кредиты = топливо по кратчайшему доступному маршруту × коэффициент. Позже коэффициент сможет менять навык научной станции.")]
    [SerializeField] [Min(0)] private float fuelRewardDeflator;
    public float FuelRewardDeflator => fuelRewardDeflator;

    [Header("Случайный груз доставки")]
    [Tooltip("Допустимые товары и относительный вес выбора. Все игровые значения задаются в этом ассете.")]
    [SerializeField] private List<DeliveryCargoOption> cargoOptions;
    [SerializeField] [Min(1)] private int minimumQuantity;
    [SerializeField] [Min(1)] private int maximumQuantity;
    public IReadOnlyList<DeliveryCargoOption> CargoOptions => cargoOptions;
    public int MinimumQuantity => minimumQuantity;
    public int MaximumQuantity => maximumQuantity;
}
