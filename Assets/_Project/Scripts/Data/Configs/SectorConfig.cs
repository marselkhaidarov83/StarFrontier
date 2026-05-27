using UnityEngine;

[CreateAssetMenu(fileName = "SectorConfig", menuName = "StarFrontier/Configs/Sector")]
public class SectorConfig : BaseConfig
{
    public int Order;
    public bool UnlockedAtStart;

    [Header("World Structure")]
    [SerializeField] private StarSystemConfig[] systems;
    [SerializeField] private StarSystemConfig startingSystem;
    [SerializeField] private PlanetConfig startingPlanet;

    public StarSystemConfig[] Systems => systems;
    public StarSystemConfig StartingSystem => startingSystem;
    public PlanetConfig StartingPlanet => startingPlanet;
}