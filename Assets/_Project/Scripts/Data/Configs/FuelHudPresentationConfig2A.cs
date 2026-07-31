using UnityEngine;

/// <summary>
/// Presentation-настройки индикатора топлива.
///
/// Содержит только визуальные параметры.
/// Не управляет расходом и пополнением топлива.
/// </summary>
[CreateAssetMenu(
    fileName = "FuelHudPresentationConfig_01",
    menuName =
        "StarFrontier/Configs/Fuel HUD Presentation Config 2A")]
public sealed class FuelHudPresentationConfig2A :
    ScriptableObject
{
    [Header("Threshold")]

    [Tooltip(
        "Доля топлива, при которой включается Low state. " +
        "0.25 означает 25 процентов.")]
    [SerializeField]
    [Range(0.01f, 0.99f)]
    private float lowFuelThresholdNormalized =
        0.25f;

    [Header("Production Icons")]

    [SerializeField]
    private Sprite normalIcon;

    [SerializeField]
    private Sprite lowIcon;

    [Header("Colors")]

    [SerializeField]
    private Color normalColor =
        Color.white;

    [SerializeField]
    private Color lowColor =
        Color.white;

    [SerializeField]
    private Color emptyColor =
        new Color(
            1f,
            0.2f,
            0.2f,
            1f);

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