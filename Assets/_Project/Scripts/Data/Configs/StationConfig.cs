using UnityEngine;

// Конфиг StationConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(fileName = "StationConfig", menuName = "StarFrontier/Configs/System/Station")]
public class StationConfig : BaseConfig
{
    [Header("Type")]
    [Tooltip("Параметр stationTypeConfig. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private StationTypeConfig stationTypeConfig;

    [Header("Station Lifecycle")]
    [Tooltip("Уровень конкретной станции в НОВОЙ игре. 0 — сначала построить первый уровень. Сохранения не перезаписываются.")]
    [SerializeField, Range(0, 10)] private int initialLevel;

    public int InitialLevel => Mathf.Clamp(initialLevel, 0, 10);
    public float OperationInteractionDistance => stationTypeConfig != null
        ? stationTypeConfig.OperationInteractionDistance : StationTypeConfig.DefaultOperationInteractionDistance;

    public StationPaymentOption[] GetPaymentOptions(StationOperationKind kind, int level)
    {
        if (kind == StationOperationKind.Construction || kind == StationOperationKind.Upgrade)
            return GetDevelopmentPaymentOptions(kind, level) ?? System.Array.Empty<StationPaymentOption>();
        return (kind == StationOperationKind.Recovery ? stationTypeConfig?.GetLevel(level)?.RecoveryPayment?.Options : null)
            ?? System.Array.Empty<StationPaymentOption>();
    }

    public bool TryCreateOperation(StationOperationKind kind, int currentLevel, string optionId,
        out StationOperationRuntimeState snapshot)
    {
        snapshot = null;
        if (currentLevel < 0 || currentLevel > 10 || string.IsNullOrWhiteSpace(optionId)) return false;
        int target = kind == StationOperationKind.Recovery ? currentLevel : currentLevel + 1;
        var options = GetPaymentOptions(kind, currentLevel);
        StationPaymentOption selected = null;
        var ids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        foreach (var option in options)
        {
            if (option == null || string.IsNullOrWhiteSpace(option.Id) || !ids.Add(option.Id)) return false;
            if (option.Id == optionId) selected = option;
        }
        if (selected == null || !selected.TryCreateSnapshot(kind, target, out snapshot)) return false;
        if (kind == StationOperationKind.Recovery) snapshot.DurationSeconds = 0;
        return true;
    }

    private StationPaymentOption[] GetDevelopmentPaymentOptions(StationOperationKind kind, int currentLevel)
    {
        if (kind == StationOperationKind.Construction ? currentLevel != 0 : currentLevel <= 0 || currentLevel >= 10)
            return null;
        return stationTypeConfig?.GetUpgradePayment(currentLevel + 1)?.Options;
    }

    [Header("Map Placement")]
    [Tooltip("Параметр stationOrbit. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private PlanetOrbitConfig stationOrbit;
    [Tooltip("Параметр orbitAngleDegrees. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float orbitAngleDegrees;

    public StationTypeConfig StationTypeConfig => stationTypeConfig;

    public StationType StationType => stationTypeConfig != null
        ? stationTypeConfig.StationType
        : default;

    public bool HasMarket => stationTypeConfig != null && stationTypeConfig.HasMarket;
    public bool HasMissions => stationTypeConfig != null && stationTypeConfig.HasMissions;
    public bool HasRepair => stationTypeConfig != null && stationTypeConfig.HasRepair;
    public bool HasMedical => stationTypeConfig != null && stationTypeConfig.HasMedical;
    public bool HasResearch => stationTypeConfig != null && stationTypeConfig.HasResearch;
    public bool HasRangerBoard => stationTypeConfig != null && stationTypeConfig.HasRangerBoard;
    public bool HasMilitaryContracts => stationTypeConfig != null && stationTypeConfig.HasMilitaryContracts;

    public MarketProfileConfig MarketProfile => stationTypeConfig != null ? stationTypeConfig.DefaultMarketProfile : null;
    public PlanetMissionConfig MissionPool => stationTypeConfig != null ? stationTypeConfig.DefaultMissionPool : null;

    public Sprite MissionIllustration => stationTypeConfig != null ? stationTypeConfig.MissionIllustration : null;
    public Sprite MarketIllustration => stationTypeConfig != null ? stationTypeConfig.MarketIllustration : null;

    public PlanetOrbitConfig StationOrbit => stationOrbit;
    public float OrbitAngleDegrees => orbitAngleDegrees;

    public Vector2 LocalOffset => stationOrbit != null
        ? CalculateOrbitLocalOffset(stationOrbit, orbitAngleDegrees)
        : stationTypeConfig != null
            ? stationTypeConfig.LocalOffset
            : Vector2.zero;

    public Sprite EffectiveIcon => stationTypeConfig != null
        ? stationTypeConfig.Icon
        : Icon;

    private static Vector2 CalculateOrbitLocalOffset(PlanetOrbitConfig orbitConfig, float angleDegrees)
    {
        if (orbitConfig == null)
        {
            return Vector2.zero;
        }

        float radians = angleDegrees * Mathf.Deg2Rad;
        float radius = Mathf.Max(0f, orbitConfig.OrbitRadius);

        return new Vector2(
            Mathf.Cos(radians) * radius,
            Mathf.Sin(radians) * radius);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (stationTypeConfig == null)
        {
            AppLog.Warning(DebugLogChannel.Config, $"StationConfig '{name}' has no StationTypeConfig assigned.");
        }
    }
#endif
}
