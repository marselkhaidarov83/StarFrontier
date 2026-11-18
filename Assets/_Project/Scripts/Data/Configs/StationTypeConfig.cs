using UnityEngine;

// Конфиг StationTypeConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(fileName = "StationTypeConfig", menuName = "StarFrontier/Configs/System/Station Type")]
public class StationTypeConfig : LocationServicesConfig
{
    public const float DefaultOperationInteractionDistance = 300f;

    [Header("Type")]
    [Tooltip("Параметр stationType. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private StationType stationType;

    [Tooltip("Максимальная дистанция для входа на станцию, строительства и восстановления.")]
    [SerializeField, Min(0.01f)] private float operationInteractionDistance = DefaultOperationInteractionDistance;

    [Header("Эффекты уровней")]
    public StationLevelEffectsSettings[] Levels = System.Array.Empty<StationLevelEffectsSettings>();

    [Tooltip("Популяция по уровням станции.")]
    [SerializeField] private PopulationConfig population;

    [Header("Оплата строительства и улучшений")]
    [Tooltip("Оплата по целевому уровню. Уровень 1 — постройка станции.")]
    [SerializeField] private StationUpgradeSettings[] upgradeSettings = System.Array.Empty<StationUpgradeSettings>();

    [Header("Default Content Links")]
    [Tooltip("Параметр defaultMarketProfile. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private MarketProfileConfig defaultMarketProfile;
    [Tooltip("Параметр defaultMissionPool. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private MissionPoolConfig defaultMissionPool;

    [Header("Default Visuals")]
    [Tooltip("Параметр localOffset. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private Vector2 localOffset = Vector2.zero;

    public StationType StationType => stationType;
    public PopulationConfig Population => population;
    public float OperationInteractionDistance => operationInteractionDistance > 0f && !float.IsInfinity(operationInteractionDistance)
        ? operationInteractionDistance : DefaultOperationInteractionDistance;
    public StationPaymentOptionsConfig GetUpgradePayment(int targetLevel)
    {
        if (upgradeSettings == null) return null;

        StationUpgradeSettings found = null;
        foreach (var entry in upgradeSettings)
        {
            if (entry == null || entry.TargetLevel != targetLevel) continue;
            if (found != null) return null;
            found = entry;
        }
        return found?.Payment;
    }

    public MarketProfileConfig DefaultMarketProfile => defaultMarketProfile;
    public MissionPoolConfig DefaultMissionPool => defaultMissionPool;

    public Vector2 LocalOffset => localOffset;

    public StationLevelEffectsSettings GetLevel(int level)
    {
        StationLevelEffectsSettings found = null;
        if (Levels == null) return null;
        foreach (var entry in Levels)
        {
            if (entry == null || entry.Level != level) continue;
            if (found != null) return null;
            found = entry;
        }
        return found;
    }
}
