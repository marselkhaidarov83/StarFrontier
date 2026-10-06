using UnityEngine;

// Конфиг отвечает за внешний вид всплывающих чисел урона в бою:
// включение показа, время жизни, движение, размер, цвет и порядок отрисовки.
[CreateAssetMenu(
    fileName = "DamagePopupVisualConfig2A",
    menuName = "StarFrontier/Configs/Combat/Damage Popup Visual Config 2A")]
public sealed class CombatDamagePopupVisualConfig2A : BaseConfig
{
    [Tooltip("Включает или выключает появление числа урона над кораблём. Проверяется перед созданием всплывающей надписи в боевой визуализации.")]
    [SerializeField] private bool enabled = true;

    [Header("Motion")]
    [Tooltip("Сколько секунд число урона живёт на экране. Используется при обновлении всплывающей надписи, после окончания времени объект удаляется.")]
    [SerializeField, Min(0.01f)] private float lifetimeSeconds = 0.85f;

    [Tooltip("Начальный отступ числа урона от корабля. Используется при выборе стартовой точки всплывающей надписи.")]
    [SerializeField, Min(0f)] private float startDistanceFromShip = 0.55f;

    [Tooltip("Расстояние, на которое число урона улетает от стартовой точки. Используется при расчёте конечного положения надписи.")]
    [SerializeField, Min(0f)] private float travelDistance = 1.15f;

    [Tooltip("Задаёт плавность движения числа урона от начала к концу. Используется каждый кадр при пересчёте положения надписи.")]
    [SerializeField] private AnimationCurve moveCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Text")]
    [Tooltip("Размер текста в момент появления числа урона. Используется при создании и обновлении надписи.")]
    [SerializeField, Min(0.01f)] private float startFontSize = 4.2f;

    [Tooltip("Размер текста перед исчезновением числа урона. Используется при плавном изменении размера надписи.")]
    [SerializeField, Min(0.01f)] private float endFontSize = 2.8f;

    [Tooltip("Цвет числа урона в момент появления. Используется при отрисовке надписи в начале её жизни.")]
    [SerializeField] private Color startColor = new Color(1f, 0.86f, 0.28f, 1f);

    [Tooltip("Цвет числа урона перед исчезновением. Используется для плавного затухания надписи.")]
    [SerializeField] private Color endColor = new Color(1f, 0.25f, 0.12f, 0f);

    [Header("Render Order")]
    [Tooltip("Слой отрисовки числа урона. Используется, чтобы надпись была видна поверх нужных объектов боя.")]
    [SerializeField] private string sortingLayerName = "SystemForegroundFX";

    [Tooltip("Порядок внутри слоя отрисовки. Чем больше число, тем выше надпись относительно объектов на том же слое.")]
    [SerializeField] private int sortingOrder = 1350;

    [Tooltip("Принудительно ставит число урона на указанную глубину сцены. Используется, чтобы надпись не проваливалась за другие объекты.")]
    [SerializeField] private bool forceWorldZ = true;

    [Tooltip("Глубина сцены для числа урона, если включена принудительная глубина. Используется при установке положения надписи.")]
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