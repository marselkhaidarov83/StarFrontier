using UnityEngine;

[CreateAssetMenu(
    fileName = "SystemHudConfig",
    menuName = "StarFrontier/Configs/Game/System HUD")]
public sealed class SystemHudConfig : ScriptableObject
{
    [Header("Visibility")]
    [SerializeField]
    private bool showFuel = true;

    [SerializeField]
    private bool showHull = true;

    [SerializeField]
    private bool showShield = true;

    [SerializeField]
    private bool showCurrentTarget = true;

    [SerializeField]
    private bool showSystemName = true;

    [Header("Safe Area")]
    [SerializeField]
    private bool useSafeArea = true;

    [SerializeField]
    private Vector2 hudPadding = new Vector2(32f, 32f);

    [Header("Bars")]
    [SerializeField]
    [Min(0f)]
    private float barWidth = 320f;

    [SerializeField]
    [Min(0f)]
    private float barHeight = 28f;

    [SerializeField]
    [Min(0f)]
    private float barSpacing = 12f;

    [Header("Warnings")]
    [SerializeField]
    [Range(0f, 1f)]
    private float lowFuelNormalizedThreshold = 0.2f;

    [SerializeField]
    [Range(0f, 1f)]
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