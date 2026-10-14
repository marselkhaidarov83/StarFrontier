using UnityEngine;

// Конфиг хранит простые отладочные переключатели игры:
// отладочный слой интерфейса, служебные сообщения и неуязвимость игрока.
[CreateAssetMenu(fileName = "DebugConfig", menuName = "StarFrontier/Configs/Game/Debug Config")]
public class DebugConfig : ScriptableObject
{
    [Header("Debug UI")]
    [Tooltip("Показывает или скрывает отладочный интерфейс при запуске в Unity Editor.")]
    public bool showDebugOverlayInEditor = true;

    [Tooltip("Показывает или скрывает отладочный интерфейс в Android/iOS-сборке.")]
    public bool showDebugOverlayInMobileBuild = false;

    [Header("Gameplay Debug")]
    [Tooltip("Включает неуязвимость игрока. Используется при расчёте урона по игроку и может переключаться через меню главного загрузчика.")]
    public bool enableGodMode = true;
}