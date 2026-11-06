using UnityEngine;

// Конфиг StationConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(fileName = "StationConfig", menuName = "StarFrontier/Configs/System/Station")]
public class StationConfig : BaseConfig
{
    [Header("Type")]
    [Tooltip("Параметр stationTypeConfig. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private StationTypeConfig stationTypeConfig;

    [Header("Level Effects and Repair")]
    [SerializeField] private StationLevelEffectsConfig levelEffects;
    public StationLevelEffectsConfig LevelEffects => levelEffects;

    [Header("Station Lifecycle")]
    [Tooltip("Уровень конкретной станции в НОВОЙ игре. 0 — сначала построить первый уровень. Сохранения не перезаписываются.")]
    [SerializeField, Range(0, 10)] private int initialLevel;
    [Tooltip("Окно строительства/восстановления открывается при расстоянии МЕНЬШЕ этого значения.")]
    [SerializeField, Min(0.01f)] private float operationInteractionDistance = 300f;
    [Tooltip("Варианты оплаты первого уровня. У каждого варианта своё время, включая мгновенный.")]
    [SerializeField] private StationPaymentOption[] constructionPaymentOptions = System.Array.Empty<StationPaymentOption>();
    [SerializeField] private StationUpgradeSettings[] upgradeSettings = System.Array.Empty<StationUpgradeSettings>();
    [Tooltip("Восстановление оплачивается отдельно. Те же способы оплаты, но собственные цены и время.")]
    [SerializeField, HideInInspector] private StationPaymentOption[] recoveryPaymentOptions = System.Array.Empty<StationPaymentOption>();

    public int InitialLevel => Mathf.Clamp(initialLevel, 0, 10);
    public float OperationInteractionDistance => float.IsNaN(operationInteractionDistance) ||
        float.IsInfinity(operationInteractionDistance) || operationInteractionDistance <= 0f
        ? 300f : operationInteractionDistance;

    public StationPaymentOption[] GetPaymentOptions(StationOperationKind kind, int level)
    {
        if (kind == StationOperationKind.Construction) return constructionPaymentOptions ?? System.Array.Empty<StationPaymentOption>();
        if (kind == StationOperationKind.Upgrade)
        {
            if (upgradeSettings != null)
                foreach (var entry in upgradeSettings)
                    if (entry != null && entry.TargetLevel == level + 1) return entry.PaymentOptions ?? System.Array.Empty<StationPaymentOption>();
            return System.Array.Empty<StationPaymentOption>();
        }
        var settings = levelEffects?.GetLevel(Mathf.Max(1, level));
        StationInstantPaymentOption[] instant = kind == StationOperationKind.Repair
            ? settings?.RepairPaymentOptions : settings?.RecoveryPaymentOptions;
        if (instant == null) return kind == StationOperationKind.Recovery
            ? recoveryPaymentOptions ?? System.Array.Empty<StationPaymentOption>() : System.Array.Empty<StationPaymentOption>();
        var result = new StationPaymentOption[instant.Length];
        for (int i = 0; i < result.Length; i++)
            if (instant[i] != null) result[i] = new StationPaymentOption { Id = instant[i].Id, Costs = instant[i].Costs, DurationSeconds = 0 };
        return result;
    }

    public bool TryCreateOperation(StationOperationKind kind, int currentLevel, string optionId,
        out StationOperationRuntimeState snapshot)
    {
        snapshot = null;
        if (currentLevel < 0 || currentLevel > 10 || string.IsNullOrWhiteSpace(optionId)) return false;
        int target = kind == StationOperationKind.Recovery ? currentLevel : currentLevel + 1;
        StationPaymentOption[] options = null;
        if (kind == StationOperationKind.Construction && currentLevel == 0) options = constructionPaymentOptions;
        else if (kind == StationOperationKind.Recovery)
        {
            var instant = levelEffects?.GetLevel(Mathf.Max(1, currentLevel))?.RecoveryPaymentOptions;
            if (instant != null)
            {
                StationInstantPaymentOption choice = null;
                var names = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
                foreach (var entry in instant)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || !names.Add(entry.Id)) return false;
                    if (entry.Id == optionId) choice = entry;
                }
                return choice != null && choice.TryCreateSnapshot(kind, currentLevel, out snapshot);
            }
            options = recoveryPaymentOptions; // Only compatibility with older content.
        }
        else if (kind == StationOperationKind.Upgrade && currentLevel > 0 && currentLevel < 10)
        {
            bool targetFound = false;
            if (upgradeSettings != null)
                foreach (var settings in upgradeSettings)
                    if (settings != null && settings.TargetLevel == target)
                    {
                        if (targetFound) return false; // Ambiguous target-level configuration, even with null options.
                        targetFound = true;
                        options = settings.PaymentOptions;
                    }
        }
        if (options == null) return false;
        StationPaymentOption selected = null;
        var ids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        foreach (var option in options)
        {
            if (option == null || string.IsNullOrWhiteSpace(option.Id) || !ids.Add(option.Id)) return false;
            if (option.Id == optionId) selected = option;
        }
        return selected != null && selected.TryCreateSnapshot(kind, target, out snapshot);
    }

    [Header("State")]
    [Tooltip("Переключатель isMainStation. Включает или выключает соответствующее правило или отображение.")]
    [SerializeField] private bool isMainStation = true;
    [Tooltip("Переключатель isActive. Включает или выключает соответствующее правило или отображение.")]
    [SerializeField] private bool isActive = true;
    [Tooltip("Переключатель isDestroyed. Включает или выключает соответствующее правило или отображение.")]
    [SerializeField] private bool isDestroyed;
    [Tooltip("Параметр visibleAtStart. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private bool visibleAtStart;

    [Header("Optional Content Overrides")]
    [Tooltip("Параметр marketProfileOverride. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private MarketProfileConfig marketProfileOverride;
    [Tooltip("Параметр missionPoolOverride. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private ScriptableObject missionPoolOverride;
    [Tooltip("Количество для параметра encounterProfileOverride. Используется соответствующей системой при генерации или расчёте.")]
    [SerializeField] private ScriptableObject encounterProfileOverride;

    [Header("Optional Visual Overrides")]
    [Tooltip("Спрайт для поля stationSpriteOverride. Используется визуальной частью игры при отображении объекта.")]
    [SerializeField] private Sprite stationSpriteOverride;
    [Tooltip("Спрайт для поля destroyedSpriteOverride. Используется визуальной частью игры при отображении объекта.")]
    [SerializeField] private Sprite destroyedSpriteOverride;
    [Tooltip("Спрайт для поля shadowSpriteOverride. Используется визуальной частью игры при отображении объекта.")]
    [SerializeField] private Sprite shadowSpriteOverride;
    [Tooltip("Параметр overrideVisualSize. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private bool overrideVisualSize;
    [Tooltip("Параметр visualSizeOverride. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float visualSizeOverride = 180f;

    [Header("Map Placement")]
    [Tooltip("Параметр stationOrbit. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private PlanetOrbitConfig stationOrbit;
    [Tooltip("Параметр orbitAngleDegrees. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private float orbitAngleDegrees;
    [Tooltip("Параметр overrideLocalOffset. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private bool overrideLocalOffset;
    [Tooltip("Параметр localOffsetOverride. Используется связанными игровыми системами этого конфига.")]
    [SerializeField] private Vector2 localOffsetOverride = Vector2.zero;

    public StationTypeConfig StationTypeConfig => stationTypeConfig;

    public StationType StationType => stationTypeConfig != null
        ? stationTypeConfig.StationType
        : default;

    public bool IsMainStation => isMainStation;
    public bool IsActive => isActive;
    public bool IsDestroyed => isDestroyed;
    public bool VisibleAtStart => visibleAtStart;

    public bool HasMarket => stationTypeConfig != null && stationTypeConfig.HasMarket;
    public bool HasMissions => stationTypeConfig != null && stationTypeConfig.HasMissions;
    public bool HasRepair => stationTypeConfig != null && stationTypeConfig.HasRepair;
    public bool HasMedical => stationTypeConfig != null && stationTypeConfig.HasMedical;
    public bool HasResearch => stationTypeConfig != null && stationTypeConfig.HasResearch;
    public bool HasRangerBoard => stationTypeConfig != null && stationTypeConfig.HasRangerBoard;
    public bool HasMilitaryContracts => stationTypeConfig != null && stationTypeConfig.HasMilitaryContracts;

    public MarketProfileConfig MarketProfile => marketProfileOverride != null
        ? marketProfileOverride
        : stationTypeConfig != null
            ? stationTypeConfig.DefaultMarketProfile
            : null;

    public ScriptableObject MissionPool => missionPoolOverride != null
        ? missionPoolOverride
        : stationTypeConfig != null
            ? stationTypeConfig.DefaultMissionPool
            : null;

    public ScriptableObject EncounterProfile => encounterProfileOverride != null
        ? encounterProfileOverride
        : stationTypeConfig != null
            ? stationTypeConfig.DefaultEncounterProfile
            : null;

    public Sprite StationSprite => stationSpriteOverride != null
        ? stationSpriteOverride
        : stationTypeConfig != null
            ? stationTypeConfig.StationSprite
            : null;

    public Sprite DestroyedSprite => destroyedSpriteOverride != null
        ? destroyedSpriteOverride
        : stationTypeConfig != null
            ? stationTypeConfig.DestroyedSprite
            : null;

    public Sprite ShadowSprite => shadowSpriteOverride != null
        ? shadowSpriteOverride
        : stationTypeConfig != null
            ? stationTypeConfig.ShadowSprite
            : null;

    public float VisualSize => overrideVisualSize
        ? visualSizeOverride
        : stationTypeConfig != null
            ? stationTypeConfig.VisualSize
            : 180f;

    public PlanetOrbitConfig StationOrbit => stationOrbit;
    public float OrbitAngleDegrees => orbitAngleDegrees;

    public Vector2 LocalOffset => stationOrbit != null
        ? CalculateOrbitLocalOffset(stationOrbit, orbitAngleDegrees)
        : overrideLocalOffset
        ? localOffsetOverride
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
