using UnityEngine;

// Конфиг SystemHudConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(
    fileName = "SystemHudConfig",
    menuName = "StarFrontier/Configs/Game/System HUD")]
public sealed class SystemHudConfig : BaseConfig
{
    [Header("Visibility")]
    [SerializeField]
    [Tooltip("Переключатель showFuel. Включает или выключает соответствующее правило или отображение.")]
    private bool showFuel = true;

    [SerializeField]
    [Tooltip("Переключатель showHull. Включает или выключает соответствующее правило или отображение.")]
    private bool showHull = true;

    [SerializeField]
    [Tooltip("Переключатель showShield. Включает или выключает соответствующее правило или отображение.")]
    private bool showShield = true;

    [SerializeField]
    [Tooltip("Переключатель showCurrentTarget. Включает или выключает соответствующее правило или отображение.")]
    private bool showCurrentTarget = true;

    [SerializeField]
    [Tooltip("Переключатель showSystemName. Включает или выключает соответствующее правило или отображение.")]
    private bool showSystemName = true;

    [Header("Offscreen Indicators")]
    [SerializeField, InspectorName("Показывать треугольники врагов")]
    private bool showOffscreenEnemyIndicators = true;

    [SerializeField, InspectorName("Показывать треугольники мирных НПС")]
    private bool showOffscreenNpcIndicators = true;

    [SerializeField, InspectorName("Показывать треугольник игрока")]
    private bool showOffscreenPlayerIndicator = true;

    public bool ShowOffscreenEnemyIndicators => showOffscreenEnemyIndicators;
    public bool ShowOffscreenNpcIndicators => showOffscreenNpcIndicators;
    public bool ShowOffscreenPlayerIndicator => showOffscreenPlayerIndicator;

    [SerializeField, Min(0f)]
    [Tooltip("Показывать врагов за экраном, если расстояние до игрока меньше дальности их самого дальнобойного оружия, умноженной на это значение.")]
    private float offscreenEnemyRangeMultiplier = 3f;

    [SerializeField, InspectorName("Цвет индикаторов врагов")]
    private Color offscreenEnemyIndicatorColor = new Color(1f, 0.33333334f, 0.33333334f, 1f);

    [SerializeField, InspectorName("Цвет индикатора игрока")]
    private Color offscreenPlayerIndicatorColor = Color.white;

    [SerializeField, InspectorName("Цвет индикаторов мирных НПС")]
    private Color offscreenNpcIndicatorColor = new Color(0.5f, 0.8f, 1f, 1f);

    public Color OffscreenPlayerIndicatorColor => offscreenPlayerIndicatorColor;
    public Color OffscreenNpcIndicatorColor => offscreenNpcIndicatorColor;

    [SerializeField]
    [Tooltip("Ширина и длина треугольника в единицах Canvas.")]
    private Vector2 offscreenEnemyIndicatorSize = new Vector2(24f, 28f);

    [SerializeField, Min(0f)]
    [Tooltip("Отступ треугольников от безопасного края экрана в единицах Canvas.")]
    private float offscreenEnemyIndicatorEdgePadding = 16f;

    public float OffscreenEnemyRangeMultiplier => Mathf.Max(0f, offscreenEnemyRangeMultiplier);
    public Color OffscreenEnemyIndicatorColor => offscreenEnemyIndicatorColor;
    public Vector2 OffscreenEnemyIndicatorSize => new Vector2(
        Mathf.Max(1f, offscreenEnemyIndicatorSize.x), Mathf.Max(1f, offscreenEnemyIndicatorSize.y));
    public float OffscreenEnemyIndicatorEdgePadding => Mathf.Max(0f, offscreenEnemyIndicatorEdgePadding);

    [Header("Invasion Warning")]
    [SerializeField, InspectorName("Показывать предупреждение о вторжении")]
    private bool showInvasionWarning = true;

    [SerializeField, Min(0.1f), InspectorName("Длительность предупреждения о вторжении")]
    private float invasionWarningDurationSeconds = 4.5f;

    [SerializeField, Min(1), InspectorName("Количество импульсов предупреждения")]
    private int invasionWarningPulseCount = 3;

    [SerializeField, InspectorName("Цвет предупреждения о вторжении")]
    private Color invasionWarningColor = new Color(1f, 0.16f, 0.2f, 1f);

    [SerializeField, Range(0f, 1f), InspectorName("Непрозрачность красных краёв")]
    private float invasionWarningEdgeOpacity = 0.28f;

    [SerializeField, Range(0.01f, 0.4f), InspectorName("Ширина красных краёв (доля экрана)")]
    private float invasionWarningEdgeWidth = 0.14f;

    [SerializeField, Min(20f), InspectorName("Размер знака вторжения")]
    private float invasionWarningSignSize = 150f;

    public bool ShowInvasionWarning => showInvasionWarning;
    public float InvasionWarningDurationSeconds => Mathf.Max(0.1f, invasionWarningDurationSeconds);
    public int InvasionWarningPulseCount => Mathf.Max(1, invasionWarningPulseCount);
    public Color InvasionWarningColor => invasionWarningColor;
    public float InvasionWarningEdgeOpacity => Mathf.Clamp01(invasionWarningEdgeOpacity);
    public float InvasionWarningEdgeWidth => Mathf.Clamp(invasionWarningEdgeWidth, 0.01f, 0.4f);
    public float InvasionWarningSignSize => Mathf.Max(20f, invasionWarningSignSize);

    [Header("Player Damage Pulse")]
    [SerializeField, InspectorName("Показывать пульсацию при попадании")]
    private bool showPlayerDamagePulse = true;

    [SerializeField, Min(0.1f), InspectorName("Длительность пульсации при попадании")]
    private float playerDamagePulseDurationSeconds = 0.7f;

    [SerializeField, InspectorName("Цвет пульсации при попадании")]
    private Color playerDamagePulseColor = new Color(1f, 0.6f, 0.12f, 1f);

    [SerializeField, Range(0f, 1f), InspectorName("Непрозрачность пульсации при попадании")]
    private float playerDamagePulseOpacity = 0.3f;

    [SerializeField, Range(0.01f, 0.4f), InspectorName("Ширина пульсации при попадании (доля экрана)")]
    private float playerDamagePulseWidth = 0.07f;

    public bool ShowPlayerDamagePulse => showPlayerDamagePulse;
    public float PlayerDamagePulseDurationSeconds => Mathf.Max(0.1f, playerDamagePulseDurationSeconds);
    public Color PlayerDamagePulseColor => playerDamagePulseColor;
    public float PlayerDamagePulseOpacity => Mathf.Clamp01(playerDamagePulseOpacity);
    public float PlayerDamagePulseWidth => Mathf.Clamp(playerDamagePulseWidth, 0.01f, 0.4f);

    [Header("Safe Area")]
    [SerializeField]
    [Tooltip("Переключатель useSafeArea. Включает или выключает соответствующее правило или отображение.")]
    private bool useSafeArea = true;

    [SerializeField]
    private Vector2 hudPadding = new Vector2(32f, 32f);

    [Header("Bars")]
    [SerializeField]
    [Min(0f)]
    [Tooltip("Параметр barWidth. Используется связанными игровыми системами этого конфига.")]
    private float barWidth = 320f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Параметр barHeight. Используется связанными игровыми системами этого конфига.")]
    private float barHeight = 28f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Параметр barSpacing. Используется связанными игровыми системами этого конфига.")]
    private float barSpacing = 12f;

    [Header("Warnings")]
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Параметр lowFuelNormalizedThreshold. Используется связанными игровыми системами этого конфига.")]
    private float lowFuelNormalizedThreshold = 0.2f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Параметр lowHullNormalizedThreshold. Используется связанными игровыми системами этого конфига.")]
    private float lowHullNormalizedThreshold = 0.25f;

    public bool ShowFuel => showFuel;
    public bool ShowHull => showHull;
    public bool ShowShield => showShield;
    public bool ShowCurrentTarget => showCurrentTarget;
    public bool ShowSystemName => showSystemName;

    public bool UseSafeArea => useSafeArea;
    public Vector2 HudPadding => hudPadding;

    public float BarWidth => barWidth;
    public float BarHeight => barHeight;
    public float BarSpacing => barSpacing;

    public float LowFuelNormalizedThreshold => lowFuelNormalizedThreshold;
    public float LowHullNormalizedThreshold => lowHullNormalizedThreshold;
}
