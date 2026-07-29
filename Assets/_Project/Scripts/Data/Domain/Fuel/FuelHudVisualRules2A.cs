using UnityEngine;

/// <summary>
/// Чистые правила выбора визуального состояния топлива.
/// Не изменяет CurrentFuel и FuelCapacity.
/// </summary>
public static class FuelHudVisualRules2A
{
    public static FuelHudVisualState2A Resolve(
        float currentFuel,
        float fuelCapacity,
        float lowFuelThresholdNormalized)
    {
        if (!IsFinite(currentFuel) ||
            !IsFinite(fuelCapacity) ||
            fuelCapacity <= 0f)
        {
            return FuelHudVisualState2A.Unavailable;
        }

        float safeCurrentFuel =
            Mathf.Clamp(
                currentFuel,
                0f,
                fuelCapacity);

        if (safeCurrentFuel <= 0f)
        {
            return FuelHudVisualState2A.Empty;
        }

        float safeThreshold =
            Mathf.Clamp(
                lowFuelThresholdNormalized,
                0.01f,
                0.99f);

        float normalizedFuel =
            safeCurrentFuel /
            fuelCapacity;

        if (normalizedFuel <= safeThreshold)
        {
            return FuelHudVisualState2A.Low;
        }

        return FuelHudVisualState2A.Normal;
    }

    private static bool IsFinite(float value)
    {
        return
            !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }
}