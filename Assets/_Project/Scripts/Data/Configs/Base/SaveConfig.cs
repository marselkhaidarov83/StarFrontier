using UnityEngine;

[CreateAssetMenu(menuName = "StarFrontier/Base/Save Config")]
public sealed class SaveConfig : ScriptableObject
{
    [Header("Files")]
    public string saveFileName = "star_frontier_save.json";
    public string backupFileName = "star_frontier_save_backup.json";

    [Header("Version")]
    public int saveVersion = 1;

    [Header("Safety")]
    public bool useBackupSave = true;
    public bool validateSaveOnLoad = true;
}