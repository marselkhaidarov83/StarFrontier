using UnityEngine;

[CreateAssetMenu(fileName = "DebugConfig", menuName = "StarFrontier/Configs/Debug Config")]
public class DebugConfig : ScriptableObject
{
    [Header("Debug UI")]
    public bool showDebugOverlay = true;
    public bool showServiceLogs = true;
    public bool showEventLogs = true;

    [Header("Gameplay Debug")]
    public bool enableGodMode = false;
    public bool enableFastTravelDebug = false;
    public bool enableSpawnDebug = false;
}