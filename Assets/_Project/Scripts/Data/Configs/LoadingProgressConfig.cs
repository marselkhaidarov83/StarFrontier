using UnityEngine;

[CreateAssetMenu(
    fileName = "loadingProgressConfig_01",
    menuName = "StarFrontier/Configs/Loading/Loading Progress Config")]
public sealed class LoadingProgressConfig : ScriptableObject
{
    [Header("Scene Duration")]
    [SerializeField, Min(0f)] private float gameStartShowSeconds = 1f;
    [SerializeField, Min(0f)] private float systemTravelShowSeconds = 0.3f;

    [Header("Progress View")]
    [SerializeField, Range(0f, 1f)] private float maxProgressBeforeWorkComplete = 0.98f;
    [SerializeField, Min(0.01f)] private float progressSmoothSpeed = 1.5f;

    [Header("Offline Relocation")]
    [SerializeField, Min(1)] private int offlineRelocationMaxNpcsPerSlice = 1000;
    [SerializeField, Min(0.1f)] private float offlineRelocationMaxSliceMs = 12f;
    [SerializeField, Range(0f, 100f)] private float offlineRelocationNpcPercent = 25f;

    [Header("Startup Progress")]
    [SerializeField, Range(0f, 1f)] private float startProgress = 0.05f;
    [SerializeField, Range(0f, 1f)] private float canContinueProgress = 0.10f;
    [SerializeField, Range(0f, 1f)] private float saveLoadedProgress = 0.22f;
    [SerializeField, Range(0f, 1f)] private float sessionLoadedProgress = 0.25f;
    [SerializeField, Range(0f, 1f)] private float offlineRelocationFromProgress = 0.25f;
    [SerializeField, Range(0f, 1f)] private float offlineRelocationToProgress = 0.78f;
    [SerializeField, Range(0f, 1f)] private float populationProgress = 0.80f;
    [SerializeField, Range(0f, 1f)] private float enterSystemProgress = 0.82f;
    [SerializeField, Range(0f, 1f)] private float warmupFromProgress = 0.82f;
    [SerializeField, Range(0f, 1f)] private float warmupToProgress = 0.98f;
    [SerializeField, Range(0f, 1f)] private float beforeLoadSystemProgress = 0.99f;

    public float GameStartShowSeconds => gameStartShowSeconds;
    public float SystemTravelShowSeconds => systemTravelShowSeconds;
    public float MaxProgressBeforeWorkComplete => maxProgressBeforeWorkComplete;
    public float ProgressSmoothSpeed => progressSmoothSpeed;

    public int OfflineRelocationMaxNpcsPerSlice => offlineRelocationMaxNpcsPerSlice;
    public float OfflineRelocationMaxSliceMs => offlineRelocationMaxSliceMs;
    public float OfflineRelocationNpcPercent => offlineRelocationNpcPercent;

    public float StartProgress => startProgress;
    public float CanContinueProgress => canContinueProgress;
    public float SaveLoadedProgress => saveLoadedProgress;
    public float SessionLoadedProgress => sessionLoadedProgress;
    public float OfflineRelocationFromProgress => offlineRelocationFromProgress;
    public float OfflineRelocationToProgress => offlineRelocationToProgress;
    public float PopulationProgress => populationProgress;
    public float EnterSystemProgress => enterSystemProgress;
    public float WarmupFromProgress => warmupFromProgress;
    public float WarmupToProgress => warmupToProgress;
    public float BeforeLoadSystemProgress => beforeLoadSystemProgress;
}