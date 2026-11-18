using UnityEngine;

/// <summary>
/// Настройки интерфейса звёздной системы и индикатора топлива.
/// </summary>
[CreateAssetMenu(
    fileName = "systemHudConfig_01",
    menuName = "StarFrontier/Configs/Game/System HUD")]
public sealed class SystemHudConfig :
    BaseConfig
{
    [Header("Порог топлива")]

    [Tooltip(
        "Ниже этой доли топлива индикатор показывает малый запас. " +
        "Например, 0.25 означает 25 процентов. Используется при выборе цвета и иконки индикатора топлива.")]
    [SerializeField]
    [Range(0.01f, 0.99f)]
    private float lowFuelThresholdNormalized =
        0.25f;

    [SerializeField, Range(0.01f, 1f)]
    [Tooltip("Минимальная доля топлива для индикатора нормального запаса.")]
    private float normalFuelThresholdNormalized = 0.75f;

    [Header("Иконки")]

    [Tooltip(
        "Иконка нормального запаса топлива. " +
        "Используется индикатором топлива, когда топлива больше порога малого запаса.")]
    [SerializeField]
    private Sprite normalIcon;

    [SerializeField]
    [Tooltip("Иконка среднего запаса топлива.")]
    private Sprite mediumIcon;

    [Tooltip(
        "Иконка малого или пустого запаса топлива. " +
        "Используется индикатором топлива, когда топлива мало или оно закончилось.")]
    [SerializeField]
    private Sprite lowIcon;

    [Header("Цвета")]

    [Tooltip(
        "Цвет индикатора при нормальном запасе топлива. " +
        "Применяется в верхнем индикаторе топлива.")]
    [SerializeField]
    private Color normalColor =
        Color.white;

    [SerializeField]
    [Tooltip("Цвет иконки при среднем запасе топлива.")]
    private Color mediumColor = Color.white;

    [Tooltip(
        "Цвет индикатора при малом запасе топлива. " +
        "Применяется в верхнем индикаторе топлива, когда запас ниже заданного порога.")]
    [SerializeField]
    private Color lowColor =
        Color.white;

    [Tooltip(
        "Цвет индикатора, когда топливо закончилось. " +
        "Применяется в верхнем индикаторе топлива при нулевом запасе.")]
    [SerializeField]
    private Color emptyColor =
        new Color(
            1f,
            0.2f,
            0.2f,
            1f);

    [Tooltip(
        "Цвет индикатора, когда состояние топлива нельзя определить или оно недоступно. " +
        "Используется как запасной вариант для неизвестного состояния.")]
    [SerializeField]
    private Color unavailableColor =
        new Color(
            0.65f,
            0.65f,
            0.65f,
            1f);

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
    private Vector2 offscreenEnemyIndicatorSize = new Vector2(30f, 35f);

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

    public float LowHullNormalizedThreshold => lowHullNormalizedThreshold;

    public FuelHudVisualState2A ResolveState(
        float currentFuel,
        float fuelCapacity)
    {
        return FuelHudVisualRules2A.Resolve(
            currentFuel,
            fuelCapacity,
            lowFuelThresholdNormalized,
            normalFuelThresholdNormalized);
    }

    public Sprite GetIcon(
        FuelHudVisualState2A state)
    {
        switch (state)
        {
            case FuelHudVisualState2A.Normal:
                return normalIcon;

            case FuelHudVisualState2A.Medium:
                return mediumIcon;

            case FuelHudVisualState2A.Low:
            case FuelHudVisualState2A.Empty:
            case FuelHudVisualState2A.Unavailable:
                return lowIcon;

            default:
                return null;
        }
    }

    public Color GetColor(
        FuelHudVisualState2A state)
    {
        switch (state)
        {
            case FuelHudVisualState2A.Normal:
                return normalColor;

            case FuelHudVisualState2A.Medium:
                return mediumColor;

            case FuelHudVisualState2A.Low:
                return lowColor;

            case FuelHudVisualState2A.Empty:
                return emptyColor;

            default:
                return unavailableColor;
        }
    }

    private void OnValidate()
    {
        lowFuelThresholdNormalized =
            Mathf.Clamp(
                lowFuelThresholdNormalized,
                0.01f,
                0.99f);
        normalFuelThresholdNormalized =
            Mathf.Clamp(normalFuelThresholdNormalized, lowFuelThresholdNormalized, 1f);
    }
}
