using UnityEngine;

[CreateAssetMenu(fileName = "SectorConfig", menuName = "StarFrontier/Configs/Galaxy/Sector")]
public class SectorConfig : BaseConfig
{
    [Header("World Structure")]
    [SerializeField] private StarSystemConfig[] systems;
    [SerializeField] private int order;
    [SerializeField] public bool IsUnlocked = false;

    [Header("Position")]
    [SerializeField] private Vector2 mapPosition;

    [Header("Size")]
    [SerializeField] private Vector2 mapSize = new Vector2(10.8f, 19.2f);

    [Header("Visual")]
    public Sprite SectorPreviewImage;
    public Color SectorTitleColor = Color.white;

    public StarSystemConfig[] Systems => systems;
    public int Order => order;
    public Vector2 MapPosition => mapPosition;
    public Vector2 MapSize => mapSize;
}