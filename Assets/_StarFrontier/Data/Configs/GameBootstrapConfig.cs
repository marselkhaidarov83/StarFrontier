using UnityEngine;

[CreateAssetMenu(menuName = "Star Frontier/Config/Game Bootstrap Config")]
public sealed class GameBootstrapConfig : ScriptableObject
{
    public string bootSceneName = "BootScene";
    public string startGameSceneName = "StarSystemScene";

    public string defaultPlayerName = "Captain";
    public string defaultSystemId = "system_start_01";

    public bool enableDebugPanel = true;
}