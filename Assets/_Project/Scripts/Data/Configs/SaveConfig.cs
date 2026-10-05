using UnityEngine;

[CreateAssetMenu(fileName = "SaveConfig", menuName = "StarFrontier/Configs/Game/Save Config")]
public class SaveConfig : ScriptableObject
{
    [Header("Save Files")]
    public string SaveFileName = "save_star_frontier.json";
    public string BackupFileName = "save_star_frontier_backup.json";

    [Header("Multi File Save")]
    public bool UseMultiFileSave = true;
    public string MultiFileSaveDirectoryName = "save_star_frontier_parts";
    public string ActiveDirectoryName = "active";
    public string BackupDirectoryName = "backup";
    public string StagingDirectoryName = "staging";
    public string ManifestFileName = "manifest.json";

    [Header("Save Budgets")]
    public float SaveStepBudgetMs = 3f;
    public int MaxNpcCaptureItemsPerStep = 256;
    public int MaxNpcSnapshotEntriesPerPart = 64;

    [Header("Save Rules")]
    public int SaveVersion = 1;
    public int AutosaveIntervalSeconds = 60;
    public bool CreateBackupBeforeSave = true;
    public bool AutoSaveOnPause = true;
    public bool AutoSaveOnQuit = true;

    public float NormalizedSaveStepBudgetMs =>
        Mathf.Max(0.25f, SaveStepBudgetMs);

    public int NormalizedMaxNpcCaptureItemsPerStep =>
        Mathf.Max(1, MaxNpcCaptureItemsPerStep);

    public int NormalizedMaxNpcSnapshotEntriesPerPart =>
        Mathf.Max(1, MaxNpcSnapshotEntriesPerPart);

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(SaveFileName))
            SaveFileName = "save_star_frontier.json";

        if (string.IsNullOrWhiteSpace(BackupFileName))
            BackupFileName = "save_star_frontier_backup.json";

        if (string.IsNullOrWhiteSpace(MultiFileSaveDirectoryName))
            MultiFileSaveDirectoryName = "save_star_frontier_parts";

        if (string.IsNullOrWhiteSpace(ActiveDirectoryName))
            ActiveDirectoryName = "active";

        if (string.IsNullOrWhiteSpace(BackupDirectoryName))
            BackupDirectoryName = "backup";

        if (string.IsNullOrWhiteSpace(StagingDirectoryName))
            StagingDirectoryName = "staging";

        if (string.IsNullOrWhiteSpace(ManifestFileName))
            ManifestFileName = "manifest.json";

        SaveStepBudgetMs = Mathf.Max(0.25f, SaveStepBudgetMs);
        MaxNpcCaptureItemsPerStep = Mathf.Max(1, MaxNpcCaptureItemsPerStep);
        MaxNpcSnapshotEntriesPerPart = Mathf.Max(1, MaxNpcSnapshotEntriesPerPart);
        SaveVersion = Mathf.Max(1, SaveVersion);
        AutosaveIntervalSeconds = Mathf.Max(1, AutosaveIntervalSeconds);
    }
}