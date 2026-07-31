using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "StarFrontier/Configs/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Version")]
    public string gameVersion = "0.2A-S1";

    [Header("Scenes")]
    public string startSceneName = "2A_StarSystemScene";
    public string fallbackSceneName = "2A_StarSystemScene";

    [Header("Runtime")]
    public bool enableDebugOverlay = true;
    public bool enableAutosave = true;
}