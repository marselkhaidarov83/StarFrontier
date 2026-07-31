using UnityEngine;

[CreateAssetMenu(
    fileName = "GameConfig",
    menuName = "StarFrontier/Configs/Game Config")]
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
}