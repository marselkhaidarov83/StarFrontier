using UnityEngine;

[CreateAssetMenu(menuName = "Star Frontier/Config/Game Bootstrap Config")]
public sealed class GameBootstrapConfig : ScriptableObject
{
    [Header("Scenes")]
    public string bootSceneName = "BootScene";
    public string startGameSceneName = "StarSystemScene";

    [Header("Player Defaults")]
    public string defaultPlayerName = "Captain";
    public string defaultSystemId = "system_start_01";

    [Header("Debug")]
    public bool enableDebugPanel = true;
}