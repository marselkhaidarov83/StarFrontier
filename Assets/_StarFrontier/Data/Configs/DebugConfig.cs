using UnityEngine;

[CreateAssetMenu(menuName = "Star Frontier/Config/Debug Config")]
public sealed class DebugConfig : ScriptableObject
{
    public bool enableLogs = true;
    public bool enableDebugPanel = true;
    public bool enableManualTickButton = true;
    public bool enableResetSaveButton = true;
}