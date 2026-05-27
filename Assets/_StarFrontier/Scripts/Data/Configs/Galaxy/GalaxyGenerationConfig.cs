using UnityEngine;

[CreateAssetMenu(
    fileName = "GalaxyGenerationConfig",
    menuName = "Star Frontier/Galaxy/Galaxy Generation Config")]
public class GalaxyGenerationConfig : ScriptableObject
{
    [Header("Galaxy Structure")]
    public int Seed = 12345;
    public int SectorCount = 2;
    public int SystemsPerSector = 6;

    [Header("Map Layout")]
    public float SectorDistance = 900f;
    public float SystemSpread = 280f;
    public float MinSystemDistance = 120f;

    [Header("Routes")]
    public int MinRoutesPerSystem = 1;
    public int MaxRoutesPerSystem = 3;

    [Header("Starting Point")]
    public string StartingSectorId = "sector_001";
    public string StartingSystemId = "system_001_001";
}