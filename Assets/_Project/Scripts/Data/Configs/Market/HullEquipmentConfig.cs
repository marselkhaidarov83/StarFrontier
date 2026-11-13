using UnityEngine;

[CreateAssetMenu(menuName = "StarFrontier/Configs/Market/Hull Equipment")]
public sealed class HullEquipmentConfig : BaseConfig
{
    public AllyConfig Hull;
    [Min(1)] public int MaximumHull = 100;
}
