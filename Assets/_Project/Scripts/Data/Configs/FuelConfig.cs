using UnityEngine;

[CreateAssetMenu(
    fileName = "fuelConfig_01",
    menuName = "STAR FRONTIER/Fuel/Fuel Config")]
public sealed class FuelConfig : ScriptableObject
{
    [Header("Fallback")]

    [Tooltip(
        "Используется только тогда, когда ShipFinalStats " +
        "не предоставляет корректную Fuel Capacity.")]
    [SerializeField]
    [Min(0.01f)]
    private float fallbackCapacity = 100f;

    [Header("HUD")]

    [Tooltip(
        "Доля запаса, ниже которой HUD показывает Low Fuel. " +
        "Временное значение до отдельной балансной фиксации.")]
    [SerializeField]
    [Range(0.01f, 0.99f)]
    private float lowFuelThresholdNormalized = 0.20f;

    public float FallbackCapacity =>
        Mathf.Max(0.01f, fallbackCapacity);

    public float LowFuelThresholdNormalized =>
        Mathf.Clamp01(lowFuelThresholdNormalized);
}