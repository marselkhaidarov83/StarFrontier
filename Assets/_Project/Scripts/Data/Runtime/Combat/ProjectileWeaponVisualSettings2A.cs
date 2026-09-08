using UnityEngine;

[DisallowMultipleComponent]
public sealed class ProjectileWeaponVisualSettings2A : MonoBehaviour
{
    [Header("Tick Timing")]
    [SerializeField, Range(0.01f, 1f)]
    private float shotSequenceTickDuration01 = 0.8f;

    [SerializeField, Range(0f, 0.25f)]
    private float shotGapTickDuration01 = 0.02f;

    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float rapidProjectileLaneOffset = 0.35f;

    [Header("Render Order")]
    [SerializeField] private string sortingLayerName = "SystemForegroundFX";
    [SerializeField] private int sortingOrder = 1200;
    [SerializeField] private bool forceWorldZ = true;
    [SerializeField] private float worldZ = -9f;

    public float ShotSequenceTickDuration01 =>
        Mathf.Clamp(shotSequenceTickDuration01, 0.01f, 1f);

    public float ShotGapTickDuration01 =>
        Mathf.Clamp(shotGapTickDuration01, 0f, 0.25f);

    public float RapidProjectileLaneOffset =>
        Mathf.Max(0f, rapidProjectileLaneOffset);

    public string SortingLayerName =>
        string.IsNullOrWhiteSpace(sortingLayerName)
            ? "SystemForegroundFX"
            : sortingLayerName;

    public int SortingOrder => sortingOrder;
    public bool ForceWorldZ => forceWorldZ;
    public float WorldZ => worldZ;
}