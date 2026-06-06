using UnityEngine;

[CreateAssetMenu(fileName = "SectorConfig", menuName = "StarFrontier/Configs/Sector")]
public class SectorConfig : BaseConfig
{
    [Header("World Structure")]
    [SerializeField] private StarSystemConfig[] systems;
    [SerializeField] private int order;

    public StarSystemConfig[] Systems => systems;
    public int Order => order;
}