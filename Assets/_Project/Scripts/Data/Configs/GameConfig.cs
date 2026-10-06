using UnityEngine;

[CreateAssetMenu(
    fileName = "GameConfig",
    menuName = "StarFrontier/Configs/Game/Game Config")]
public class GameConfig : BaseConfig
{
    [Header("Версия")]
    [Tooltip(
        "Версия игры или этапа разработки. " +
        "Используется как служебная информация о текущем наборе правил и сборке.")]
    public string gameVersion =
        "0.2A-S1";

    [Header("Сцены")]
    [Tooltip(
        "Название сцены, с которой должна начинаться игра. " +
        "Используется при запуске игрового потока.")]
    public string startSceneName =
        "2A_StarSystemScene";

    [Tooltip(
        "Название запасной сцены. " +
        "Используется, если основной переход на сцену не сработал.")]
    public string fallbackSceneName =
        "2A_StarSystemScene";

    [Header("Работа игры")]
    [Tooltip(
        "Включает отладочный слой интерфейса на уровне общих настроек игры. " +
        "Используется как общий переключатель для отладочного отображения.")]
    public bool enableDebugOverlay =
        true;

    [Tooltip(
        "Включает автосохранение на уровне общих настроек игры. " +
        "Используется как общий переключатель сохранения.")]
    public bool enableAutosave =
        true;

    [Header("Игровое время")]

    [Tooltip(
        "Реальная длительность одного игрового шага в секундах. " +
        "От этого зависит скорость игровой симуляции, полёт снарядов, лучи и волны.")]
    [Min(0.01f)]
    public float secondsPerGameTick =
        1.5f;

    [Tooltip(
        "Количество игровых минут, которое проходит за один игровой шаг. " +
        "Используется в интерфейсе путешествия для расчёта отображаемого игрового времени.")]
    [Min(1)]
    public int minutesPerGameTick =
        15;

    [Header("Маршруты карты галактики")]

    [Tooltip(
        "Целевое расстояние между соседними точками маршрута на карте галактики. " +
        "Используется при построении линий маршрутов между системами.")]
    [Min(0.01f)]
    public float galaxyMapRoutePointSpacing = 0.25f;

    public float SecondsPerGameTick =>
        Mathf.Max(0.01f, secondsPerGameTick);

    private void OnValidate()
    {
        secondsPerGameTick =
            Mathf.Max(0.01f, secondsPerGameTick);

        minutesPerGameTick =
            Mathf.Max(1, minutesPerGameTick);

        galaxyMapRoutePointSpacing =
            Mathf.Max(0.01f, galaxyMapRoutePointSpacing);
    }
}