using UnityEngine;

[CreateAssetMenu(fileName = "SaveConfig", menuName = "StarFrontier/Configs/Game/Save Config")]
public class SaveConfig : ScriptableObject
{
    [Header("Save Files")]
    public string SaveFileName = "save_starfrontier.json";
    public string BackupFileName = "save_starfrontier_backup.json";

    [Header("Save Rules")]
    public int SaveVersion = 1;
    public int AutosaveIntervalSeconds = 60;
    public bool CreateBackupBeforeSave = true;
    public bool AutoSaveOnPause = true;
    public bool AutoSaveOnQuit = true;
}