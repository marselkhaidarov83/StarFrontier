using UnityEngine;

// Конфиг SaveConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(fileName = "SaveConfig", menuName = "StarFrontier/Configs/Game/Save Config")]
public class SaveConfig : BaseConfig
{
    [Header("Save Files")]
    [Tooltip("Параметр SaveFileName. Используется связанными игровыми системами этого конфига.")]
    public string SaveFileName = "save_star_frontier.json";
    [Tooltip("Параметр BackupFileName. Используется связанными игровыми системами этого конфига.")]
    public string BackupFileName = "save_star_frontier_backup.json";

    [Header("Multi File Save")]
    [Tooltip("Переключатель UseMultiFileSave. Включает или выключает соответствующее правило или отображение.")]
    public bool UseMultiFileSave = true;
    [Tooltip("Параметр MultiFileSaveDirectoryName. Используется связанными игровыми системами этого конфига.")]
    public string MultiFileSaveDirectoryName = "save_star_frontier_parts";
    [Tooltip("Параметр ActiveDirectoryName. Используется связанными игровыми системами этого конфига.")]
    public string ActiveDirectoryName = "active";
    [Tooltip("Параметр BackupDirectoryName. Используется связанными игровыми системами этого конфига.")]
    public string BackupDirectoryName = "backup";
    [Tooltip("Параметр StagingDirectoryName. Используется связанными игровыми системами этого конфига.")]
    public string StagingDirectoryName = "staging";
    [Tooltip("Параметр ManifestFileName. Используется связанными игровыми системами этого конфига.")]
    public string ManifestFileName = "manifest.json";

    [Header("Save Budgets")]
    [Tooltip("Параметр SaveStepBudgetMs. Используется связанными игровыми системами этого конфига.")]
    public float SaveStepBudgetMs = 3f;
    [Tooltip("Максимальное значение параметра MaxNpcCaptureItemsPerStep. Используется как верхняя граница диапазона.")]
    public int MaxNpcCaptureItemsPerStep = 256;
    [Tooltip("Максимальное значение параметра MaxNpcSnapshotEntriesPerPart. Используется как верхняя граница диапазона.")]
    public int MaxNpcSnapshotEntriesPerPart = 64;

    [Header("Save Rules")]
    [Tooltip("Параметр SaveVersion. Используется связанными игровыми системами этого конфига.")]
    public int SaveVersion = 1;
    [Tooltip("Переключатель AutosaveIntervalSeconds. Включает или выключает соответствующее правило или отображение.")]
    public int AutosaveIntervalSeconds = 60;
    [Tooltip("Переключатель CreateBackupBeforeSave. Включает или выключает соответствующее правило или отображение.")]
    public bool CreateBackupBeforeSave = true;
    [Tooltip("Переключатель AutoSaveOnPause. Включает или выключает соответствующее правило или отображение.")]
    public bool AutoSaveOnPause = true;
    [Tooltip("Переключатель AutoSaveOnQuit. Включает или выключает соответствующее правило или отображение.")]
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
