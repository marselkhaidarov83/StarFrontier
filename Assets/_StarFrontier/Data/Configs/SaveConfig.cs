using UnityEngine;

[CreateAssetMenu(menuName = "Star Frontier/Config/Save Config")]
public sealed class SaveConfig : ScriptableObject
{
    public string saveFileName = "star_frontier_save.json";
    public string backupFileName = "star_frontier_save_backup.json";
    public int saveVersion = 1;

    public bool useBackupSave = true;
    public bool validateSaveOnLoad = true;
}