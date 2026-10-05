using UnityEngine;

[DisallowMultipleComponent]
public sealed class WaveWeaponVisualSettings2A : MonoBehaviour
{
    [Header("Tick Timing")]
    [SerializeField, Range(0.01f, 1f)]
    private float waveTickDuration01 = 0.8f;

    [Header("Renderers")]
    [SerializeField] private SpriteRenderer[] waveRenderers;

    [Header("Visual Animation")]
    [SerializeField, Min(0f)] private float startRadiusWorld = 0.15f;
    [SerializeField, Range(0f, 1f)] private float startAlpha = 0.9f;
    [SerializeField, Range(0f, 1f)] private float endAlpha = 0f;
    [SerializeField, Min(0f)] private float layerRadiusStep = 0.08f;

    [SerializeField]
    private AnimationCurve radiusCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Render Order")]
    [SerializeField] private string sortingLayerName = "SystemShipFX";
    [SerializeField] private int sortingOrder = 900;
    [SerializeField] private bool forceWorldZ = true;
    [SerializeField] private float worldZ = -9f;

    public float WaveTickDuration01 =>
        Mathf.Clamp(waveTickDuration01, 0.01f, 1f);

    public SpriteRenderer[] WaveRenderers => waveRenderers;

    public float StartRadiusWorld =>
        Mathf.Max(0f, startRadiusWorld);

    public float StartAlpha =>
        Mathf.Clamp01(startAlpha);

    public float EndAlpha =>
        Mathf.Clamp01(endAlpha);

    public float LayerRadiusStep =>
        Mathf.Max(0f, layerRadiusStep);

    public AnimationCurve RadiusCurve => radiusCurve;

    public string SortingLayerName =>
        string.IsNullOrWhiteSpace(sortingLayerName)
            ? "SystemShipFX"
            : sortingLayerName;

    public int SortingOrder => sortingOrder;
    public bool ForceWorldZ => forceWorldZ;
    public float WorldZ => worldZ;
}