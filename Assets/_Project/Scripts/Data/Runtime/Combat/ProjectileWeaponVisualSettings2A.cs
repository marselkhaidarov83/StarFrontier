using UnityEngine;

[DisallowMultipleComponent]
public sealed class ProjectileWeaponVisualSettings2A : MonoBehaviour
{
    [Header("Tick Timing")]
    [SerializeField, Range(0.01f, 1f)]
    private float shotSequenceTickDuration01 = 1f;

    [SerializeField, Range(0f, 0.25f)]
    private float shotGapTickDuration01 = 0.02f;

    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float rapidProjectileLaneOffset = 0.35f;

    [SerializeField, Min(0f)]
    private float missileLaneOffset = 0.45f;

    [Header("Missile Movement")]
    [SerializeField, Min(0.01f)]
    private float missileInitialSpeedUnitsPerSecond = 60f;

    [SerializeField, Min(0.01f)]
    private float missileFinalSpeedUnitsPerSecond = 220f;

    [SerializeField, Min(0.01f)]
    private float missileArrivalDistance = 0.25f;

    [SerializeField, Min(0f)]
    private float missileCurveOffset = 0.8f;

    [Header("Render Order")]
    [SerializeField] private string sortingLayerName = "SystemShipFX";
    [SerializeField] private int sortingOrder = 805;
    [SerializeField] private bool forceWorldZ = true;
    [SerializeField] private float worldZ = -9f;

    public float ShotSequenceTickDuration01 =>
        Mathf.Clamp(shotSequenceTickDuration01, 0.01f, 1f);

    public float ShotGapTickDuration01 =>
        Mathf.Clamp(shotGapTickDuration01, 0f, 0.25f);

    public float RapidProjectileLaneOffset =>
        Mathf.Max(0f, rapidProjectileLaneOffset);

    public float MissileLaneOffset =>
        Mathf.Max(0f, missileLaneOffset);

    public float MissileInitialSpeedUnitsPerSecond =>
        Mathf.Max(0.01f, missileInitialSpeedUnitsPerSecond);

    public float MissileFinalSpeedUnitsPerSecond =>
        Mathf.Max(MissileInitialSpeedUnitsPerSecond, missileFinalSpeedUnitsPerSecond);

    public float MissileArrivalDistance =>
        Mathf.Max(0.01f, missileArrivalDistance);

    public float MissileCurveOffset =>
        Mathf.Max(0f, missileCurveOffset);

    public string SortingLayerName =>
        string.IsNullOrWhiteSpace(sortingLayerName)
            ? "SystemShipFX"
            : sortingLayerName;

    public int SortingOrder => sortingOrder;
    public bool ForceWorldZ => forceWorldZ;
    public float WorldZ => worldZ;
}