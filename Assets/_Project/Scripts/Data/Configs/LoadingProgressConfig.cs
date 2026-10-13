using UnityEngine;

[CreateAssetMenu(
    fileName = "loadingProgressConfig_01",
    menuName = "StarFrontier/Configs/Loading/Loading Progress Config")]
public sealed class LoadingProgressConfig : BaseConfig
{
    [Header("Длительность показа")]
    [Tooltip(
        "Минимальное время показа загрузочного экрана при старте игры. " +
        "Используется загрузочным экраном перед входом в игровую сцену.")]
    [SerializeField, Min(0f)] private float gameStartShowSeconds = 1f;

    [Tooltip(
        "Минимальное время показа загрузочного экрана при перелёте между системами. " +
        "Используется загрузочным экраном во время системного перехода.")]
    [SerializeField, Min(0f)] private float systemTravelShowSeconds = 0.3f;

    [Header("Шкала загрузки")]
    [Tooltip(
        "Максимальное значение шкалы до полного завершения работы. " +
        "Используется, чтобы шкала не показывала 100 процентов раньше времени.")]
    [SerializeField, Range(0f, 1f)] private float maxProgressBeforeWorkComplete = 0.98f;

    [Tooltip(
        "Скорость плавного движения шкалы загрузки к целевому значению. " +
        "Используется загрузочным экраном каждый кадр.")]
    [SerializeField, Min(0.01f)] private float progressSmoothSpeed = 1.5f;

    [Header("Фоновая перестановка кораблей")]
    [Tooltip(
        "Сколько неигровых кораблей можно обработать за один кусок фоновой перестановки при загрузке. " +
        "Используется, чтобы не перегружать один кадр.")]
    [SerializeField, Min(1)] private int offlineRelocationMaxNpcsPerSlice = 1000;

    [Tooltip(
        "Сколько миллисекунд максимум можно тратить на один кусок фоновой перестановки кораблей. " +
        "Используется, чтобы загрузка оставалась плавной.")]
    [SerializeField, Min(0.1f)] private float offlineRelocationMaxSliceMs = 12f;

    [Tooltip(
        "Какой процент подходящих неигровых кораблей можно переставить при фоновой обработке загрузки. " +
        "Используется при продолжении игры после пропущенного времени.")]
    [SerializeField, Range(0f, 100f)] private float offlineRelocationNpcPercent = 25f;

    [Header("Этапы загрузки")]
    [Tooltip(
        "Значение шкалы в самом начале загрузки.")]
    [SerializeField, Range(0f, 1f)] private float startProgress = 0.05f;

    [Tooltip(
        "Значение шкалы после проверки, можно ли продолжить игру.")]
    [SerializeField, Range(0f, 1f)] private float canContinueProgress = 0.10f;

    [Tooltip(
        "Значение шкалы после загрузки сохранения.")]
    [SerializeField, Range(0f, 1f)] private float saveLoadedProgress = 0.22f;

    [Tooltip(
        "Значение шкалы после подготовки игровой сессии.")]
    [SerializeField, Range(0f, 1f)] private float sessionLoadedProgress = 0.25f;

    [Tooltip(
        "Начало участка шкалы для фоновой перестановки неигровых кораблей.")]
    [SerializeField, Range(0f, 1f)] private float offlineRelocationFromProgress = 0.25f;

    [Tooltip(
        "Конец участка шкалы для фоновой перестановки неигровых кораблей.")]
    [SerializeField, Range(0f, 1f)] private float offlineRelocationToProgress = 0.78f;

    [Tooltip(
        "Значение шкалы после наполнения системы кораблями.")]
    [SerializeField, Range(0f, 1f)] private float populationProgress = 0.80f;

    [Tooltip(
        "Значение шкалы перед входом в игровую систему.")]
    [SerializeField, Range(0f, 1f)] private float enterSystemProgress = 0.82f;

    [Tooltip(
        "Начало участка шкалы для предварительной подготовки неигровых кораблей перед входом в систему.")]
    [SerializeField, Range(0f, 1f)] private float warmupFromProgress = 0.82f;

    [Tooltip(
        "Конец участка шкалы для предварительной подготовки неигровых кораблей перед входом в систему.")]
    [SerializeField, Range(0f, 1f)] private float warmupToProgress = 0.98f;

    [Tooltip(
        "Значение шкалы прямо перед загрузкой сцены системы.")]
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