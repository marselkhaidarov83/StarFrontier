using UnityEngine;

[CreateAssetMenu(fileName = "PlayerAnalyticsConfig", menuName = "StarFrontier/Configs/Analytics/Player Analytics")]
public sealed class PlayerAnalyticsConfig : ScriptableObject
{
    [Tooltip("Собирать события действий игрока. Личный выбор в настройках игры также учитывается.")]
    public bool collectionEnabled = true;
    public string productionApiKey;
    public string testApiKey;
    [Tooltip("Менять при изменении цен, наград или правил баланса.")]
    public string balanceVersion = "2A-2026-10-23";
    [Range(100, 10000)] public int maxQueuedReports = 1000;
    [Min(10)] public int dispatchPeriodSeconds = 30;
    [Min(10)] public int sessionTimeoutSeconds = 1800;
}
