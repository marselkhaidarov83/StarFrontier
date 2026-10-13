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
