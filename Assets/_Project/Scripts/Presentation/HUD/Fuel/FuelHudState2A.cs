using UnityEngine;

/// <summary>
/// Чистая логика выбора HUD-состояния топлива.
/// Не читает Unity-сцену, не меняет UI и не хранит gameplay-state.
/// Нужен, чтобы thresholds можно было проверить EditMode-тестами.
/// </summary>
public enum FuelHudState2A
{
    Low = 0,
    Medium = 1,
    Normal = 2
}

public static class FuelHudStateUtility2A
{
    public const float LowUpperExclusive = 25f;
    public const float NormalLowerInclusive = 75f;

    public static FuelHudState2A GetState(
        int currentFuel,
        int fuelCapacity)
    {
        if (fuelCapacity <= 0)
            return FuelHudState2A.Low;

        int safeCurrentFuel = Mathf.Clamp(
            currentFuel,
            0,
            fuelCapacity);

        float percent = GetPercent(
            safeCurrentFuel,
            fuelCapacity);

        return GetStateByPercent(percent);
    }

    public static FuelHudState2A GetStateByPercent(
        float percent)
    {
        if (float.IsNaN(percent) ||
            float.IsInfinity(percent))
        {
            return FuelHudState2A.Low;
        }

        float safePercent = Mathf.Clamp(
            percent,
            0f,
            100f);

        if (safePercent >= NormalLowerInclusive)
            return FuelHudState2A.Normal;

        if (safePercent >= LowUpperExclusive)
            return FuelHudState2A.Medium;

        return FuelHudState2A.Low;
    }

    public static float GetPercent(
        int currentFuel,
        int fuelCapacity)
    {
        if (fuelCapacity <= 0)
            return 0f;

        int safeCurrentFuel = Mathf.Clamp(
            currentFuel,
            0,
            fuelCapacity);

        return safeCurrentFuel * 100f / fuelCapacity;
    }

    public static string BuildFuelLabel(
        int currentFuel,
        int fuelCapacity)
    {
        if (fuelCapacity <= 0)
            return "Топливо: 0 / 0";

        int safeCurrentFuel = Mathf.Clamp(
            currentFuel,
            0,
            fuelCapacity);

        return $"Топливо: {safeCurrentFuel} / {fuelCapacity}";
    }
}