using UnityEngine;

// Конфиг хранит простые отладочные переключатели игры:
// отладочный слой интерфейса, служебные сообщения и неуязвимость игрока.
[CreateAssetMenu(fileName = "DebugConfig", menuName = "StarFrontier/Configs/Game/Debug Config")]
public class DebugConfig : ScriptableObject
{
    [Header("Debug UI")]
    [Tooltip("Показывает или скрывает отладочный слой интерфейса. В текущем коде хранится в конфиге и готов к подключению панели.")]
    public bool showDebugOverlay = true;

    [Tooltip("Разрешает служебные сообщения игровых систем. В текущем коде хранится в конфиге и может использоваться отладочными панелями.")]
    public bool showServiceLogs = true;

    [Tooltip("Разрешает сообщения о событиях игры. В текущем коде хранится в конфиге и может использоваться отладочными панелями.")]
    public bool showEventLogs = true;

    [Header("Gameplay Debug")]
    [Tooltip("Включает неуязвимость игрока. Используется при расчёте урона по игроку и может переключаться через меню главного загрузчика.")]
    public bool enableGodMode = false;

    [Tooltip("Разрешает отладку быстрого путешествия. В текущем коде хранится в конфиге и готов к подключению отладочного перехода.")]
    public bool enableFastTravelDebug = false;

    [Tooltip("Разрешает отладку создания объектов. В текущем коде хранится в конфиге и готов к подключению отладочного создания кораблей или событий.")]
    public bool enableSpawnDebug = false;
}