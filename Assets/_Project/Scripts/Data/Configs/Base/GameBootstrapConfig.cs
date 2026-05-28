using UnityEngine;

[CreateAssetMenu(menuName = "StarFrontier/Base/Game Bootstrap Config")]
public sealed class GameBootstrapConfig : ScriptableObject
{
    [Header("Scenes")]
    public string bootSceneName = "2A_BootScene";
    public string startGameSceneName = "2A_StarSystemScene";

    [Header("Player Defaults")]
    public string defaultPlayerName = "Captain";
    public string defaultSystemId = "system_heliosGate_01";

    [Header("Debug")]
    public bool enableDebugPanel = true;
}