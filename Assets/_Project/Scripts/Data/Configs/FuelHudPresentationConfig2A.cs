using UnityEngine;

/// <summary>
/// Настройки внешнего вида индикатора топлива.
///
/// Содержит только визуальные параметры.
/// Не управляет расходом и пополнением топлива.
/// </summary>
[CreateAssetMenu(
    fileName = "FuelHudPresentationConfig_01",
    menuName =
        "StarFrontier/Configs/Fuel HUD Presentation Config 2A")]
public sealed class FuelHudPresentationConfig2A :
    BaseConfig
{
    [Header("Порог топлива")]

    [Tooltip(
        "Доля топлива, при которой индикатор переходит в состояние малого запаса. " +
        "Например, 0.25 означает 25 процентов. Используется при выборе цвета и иконки индикатора топлива.")]
    [SerializeField]
    [Range(0.01f, 0.99f)]
    private float lowFuelThresholdNormalized =
        0.25f;

    [Header("Иконки")]

    [Tooltip(
        "Иконка нормального запаса топлива. " +
        "Используется индикатором топлива, когда топлива больше порога малого запаса.")]
    [SerializeField]
    private Sprite normalIcon;

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

    public float LowFuelThresholdNormalized =>
        lowFuelThresholdNormalized;

    public FuelHudVisualState2A ResolveState(
        float currentFuel,
        float fuelCapacity)
    {
        return FuelHudVisualRules2A.Resolve(
            currentFuel,
            fuelCapacity,
            lowFuelThresholdNormalized);
    }

    public Sprite GetIcon(
        FuelHudVisualState2A state)
    {
        switch (state)
        {
            case FuelHudVisualState2A.Normal:
                return normalIcon;

            case FuelHudVisualState2A.Low:
            case FuelHudVisualState2A.Empty:
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
    }
}