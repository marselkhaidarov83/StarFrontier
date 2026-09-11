using UnityEngine;

[CreateAssetMenu(
    fileName = "GameConfig",
    menuName = "StarFrontier/Configs/Game/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Version")]
    public string gameVersion =
        "0.2A-S1";

    [Header("Scenes")]
    public string startSceneName =
        "2A_StarSystemScene";

    public string fallbackSceneName =
        "2A_StarSystemScene";

    [Header("Runtime")]
    public bool enableDebugOverlay =
        true;

    public bool enableAutosave =
        true;

    [Header("Game Time")]

    [Tooltip(
        "Реальная длительность одного игрового тика в секундах. " +
        "От этого зависит скорость квантовой симуляции, полет снарядов, лучи и волны.")]
    [Min(0.01f)]
    public float secondsPerGameTick =
        1.5f;

    [Tooltip(
        "Количество игровых минут, " +
        "которое проходит за один игровой тик.")]
    [Min(1)]
    public int minutesPerGameTick =
        15;

    [Header("Galaxy Map Routes")]

    [Tooltip(
"Целевое расстояние между соседними точками маршрута " +
"на карте галактики, в координатах MapPosition."
)]
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