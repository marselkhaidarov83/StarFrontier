using UnityEngine;
using UnityEngine.Serialization;

// Конфиг SectorConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(fileName = "SectorConfig", menuName = "StarFrontier/Configs/Galaxy/Sector")]
public class SectorConfig : BaseConfig
{
    [Header("World Structure")]
    [Tooltip("Параметр systems. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private StarSystemConfig[] systems;
    [Tooltip("Параметр order. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private int order;
    [Tooltip("Переключатель IsUnlocked. Включает или выключает соответствующее правило или отображение.")]
    [SerializeField] public bool IsUnlocked = false;

    [Header("Enemy Start State")]
    [FormerlySerializedAs("isEnemyOwnedAtStart")]
    [Tooltip("Если включено, все системы этого сектора при новой игре стартуют как вражеские системы.")]
    [SerializeField] private bool isEnemyAtStart;

    [Tooltip("Фракция, которой принадлежат стартовые вражеские системы этого сектора.")]
    [SerializeField] private EnemyFaction2A enemyFactionAtStart = EnemyFaction2A.AI;

    [Header("Position")]
    [Tooltip("Позиция объекта на карте галактики или сектора.")]
    [SerializeField] private Vector2 mapPosition;

    [Header("Size")]
    [Tooltip("Параметр mapSize. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private Vector2 mapSize = new Vector2(10.8f, 19.2f);

    [Header("Visual")]
    [Tooltip("Параметр SectorPreviewImage. Используется связанными игровыми системами этого конфига.")]
    public Sprite SectorPreviewImage;
    [Tooltip("Цвет для поля SectorTitleColor. Используется при визуальном отображении объекта или интерфейса.")]
    public Color SectorTitleColor = Color.white;

    public StarSystemConfig[] Systems => systems;
    public int Order => order;
    public bool IsEnemyAtStart => isEnemyAtStart;
    public EnemyFaction2A EnemyFactionAtStart => enemyFactionAtStart;
    public Vector2 MapPosition => mapPosition;
    public Vector2 MapSize => mapSize;
}