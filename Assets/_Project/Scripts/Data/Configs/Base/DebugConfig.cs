using UnityEngine;

[CreateAssetMenu(menuName = "StarFrontier/Base/Debug Config")]
public sealed class DebugConfig : ScriptableObject
{
    [Header("Debug")]
    public bool enableLogs = true;
    public bool enableDebugPanel = true;

    [Header("Debug Buttons")]
    public bool enableManualTickButton = true;
    public bool enableResetSaveButton = true;
}