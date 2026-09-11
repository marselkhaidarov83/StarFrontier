using UnityEngine;

[CreateAssetMenu(
    fileName = "DamagePopupVisualConfig2A",
    menuName = "Star Frontier/Combat/Damage Popup Visual Config 2A")]
public sealed class CombatDamagePopupVisualConfig2A : BaseConfig
{
    [SerializeField] private bool enabled = true;

    [Header("Motion")]
    [SerializeField, Min(0.01f)] private float lifetimeSeconds = 0.85f;
    [SerializeField, Min(0f)] private float startDistanceFromShip = 0.55f;
    [SerializeField, Min(0f)] private float travelDistance = 1.15f;
    [SerializeField] private AnimationCurve moveCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Text")]
    [SerializeField, Min(0.01f)] private float startFontSize = 4.2f;
    [SerializeField, Min(0.01f)] private float endFontSize = 2.8f;
    [SerializeField] private Color startColor = new Color(1f, 0.86f, 0.28f, 1f);
    [SerializeField] private Color endColor = new Color(1f, 0.25f, 0.12f, 0f);

    [Header("Render Order")]
    [SerializeField] private string sortingLayerName = "SystemForegroundFX";
    [SerializeField] private int sortingOrder = 1350;
    [SerializeField] private bool forceWorldZ = true;
    [SerializeField] private float worldZ = -9f;

    public bool Enabled => enabled;
    public float LifetimeSeconds => Mathf.Max(0.01f, lifetimeSeconds);
    public float StartDistanceFromShip => Mathf.Max(0f, startDistanceFromShip);
    public float TravelDistance => Mathf.Max(0f, travelDistance);
    public AnimationCurve MoveCurve => moveCurve;
    public float StartFontSize => Mathf.Max(0.01f, startFontSize);
    public float EndFontSize => Mathf.Max(0.01f, endFontSize);
    public Color StartColor => startColor;
    public Color EndColor => endColor;

    public string SortingLayerName =>
        string.IsNullOrWhiteSpace(sortingLayerName)
            ? "SystemForegroundFX"
            : sortingLayerName;

    public int SortingOrder => sortingOrder;
    public bool ForceWorldZ => forceWorldZ;
    public float WorldZ => worldZ;
}