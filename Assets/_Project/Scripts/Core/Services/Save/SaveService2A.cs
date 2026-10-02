using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public class SaveService2A : CustomService, ISaveService
{
    private enum StagedSavePhase
    {
        None,
        PrepareBase,
        PrepareNpc,
        ValidateState,
        StampIntegrity,
        BuildParts,
        JsonPart,
        WritePart,
        WriteManifest,
        ValidateManifest,
        ValidateManifestPart,
        BackupDeletePrepare,
        BackupDeletePart,
        BackupDeleteFinalize,
        BackupPrepare,
        BackupCopyPart,
        BackupFinalize,
        ActiveDeletePrepare,
        ActiveDeletePart,
        ActiveDeleteFinalize,
        Activate,
        Complete
    }

    private sealed class SavePartWorkItem
    {
        public string Name;
        public string FileName;
        public string Kind;
        public int Index;
        public object Payload;
        public string Json;
        public int JsonChars;
        public string Sha256;
        public double JsonMs;
        public double WriteMs;
    }

    private readonly SaveConfig _saveConfig;
    private readonly float _autosaveIntervalSeconds;
    private readonly string _saveFileName;
    private readonly string _backupFileName;

    private float _autosaveTimer;
    private bool _enabledSave = true;

    private readonly IConfigService _configService;
    private readonly SimpleEventBus _eventBus;
    private readonly IGameSessionService _gameSessionService;
    private readonly ISystemEncounterSaveService _systemEncounterSaveService;
    private readonly ISystemNpcSimulationSaveService _systemNpcSimulationSaveService;
    private readonly SaveMigrationStage _migrationStage = new();
    private readonly SaveValidationStage _validationStage = new();
    private readonly SaveIntegrityStage _integrityStage = new();

    private bool _stagedSaveInProgress;
    private bool _stagedSaveQueuedAgain;
    private StagedSavePhase _stagedSavePhase = StagedSavePhase.None;
    private GameRuntimeState _stagedSaveState;
    private string _stagedSaveReason = string.Empty;
    private string _stagedSaveId = string.Empty;
    private string _stagedDirectoryPath = string.Empty;
    private SaveMultiFileManifest _stagedManifest;
    private string _stagedManifestJson;
    private string _backupStagingDirectoryPath = string.Empty;
    private readonly List<string> _backupCopySourceFiles = new();
    private int _backupCopyFileIndex;
    private bool _backupRequired;

    private int _manifestValidationPartIndex;

    private readonly List<string> _backupDeleteFiles = new();
    private readonly List<string> _backupDeleteDirectories = new();
    private int _backupDeleteFileIndex;

    private readonly List<string> _activeDeleteFiles = new();
    private readonly List<string> _activeDeleteDirectories = new();
    private int _activeDeleteFileIndex;

    private double _stagedBackupDeletePrepareMs;
    private double _stagedBackupDeleteMs;
    private double _stagedBackupDeleteFinalizeMs;
    private double _stagedActiveDeletePrepareMs;
    private double _stagedActiveDeleteFinalizeMs;
    private double _stagedValidateMs;
    private double _stagedStampMs;
    private double _stagedBuildPartsMs;
    private double _stagedWriteManifestMs;
    private double _stagedValidateManifestMs;
    private double _stagedBackupPrepareMs;
    private double _stagedBackupCopyMs;
    private double _stagedBackupFinalizeMs;
    private double _stagedActiveDeleteMs;
    private double _stagedActivateMs;

    private SystemNpcSimulationCaptureSession _npcCaptureSession;
    private readonly List<SavePartWorkItem> _partWorkItems = new();
    private int _currentPartIndex;

    private long _stagedSaveTotalStartedAt;
    private int _stagedSaveStartUnityFrame;
    private int _stagedSaveStartTick;

    private double _stagedPrepareBaseMs;
    private double _stagedNpcPrepareMs;
    private double _stagedJsonMs;
    private double _stagedWriteMs;

    private int _stagedSavedNpcCount;
    private int _stagedSavedNpcSnapshotEntryCount;
    private int _stagedSavedLegacyNpcCount;
    private int _stagedTotalJsonChars;

    private int _stagedGc0Before;
    private int _stagedGc1Before;
    private int _stagedGc2Before;
    private long _stagedManagedMemoryBefore;
    private long _stagedMonoUsedBefore;
    private long _stagedTotalAllocatedBefore;

    public SaveService2A()
    {
        _debugStop = true;

        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _saveConfig = _configService.SaveConfig;
        _autosaveIntervalSeconds = _saveConfig.AutosaveIntervalSeconds;
        _saveFileName = _saveConfig.SaveFileName;
        _backupFileName = _saveConfig.BackupFileName;

        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _systemEncounterSaveService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEncounterSaveService>();
        _systemNpcSimulationSaveService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcSimulationSaveService>();

        _eventBus?.Subscribe<SaveNeedEvent>(OnSaveNeedEvent);
    }

    private void OnSaveNeedEvent(SaveNeedEvent evt)
    {
        QueueStagedSave(
            _gameSessionService?.State,
            "SaveNeedEvent");
    }

    public void EnableSave(bool enable)
    {
        _enabledSave = enable;

        if (!enable && _stagedSaveInProgress)
            CancelStagedSave("EnableSaveFalse");
    }

    public bool HasSave()
    {
        return HasMultiFileSave(GetActiveMultiFileDirectoryPath()) ||
               HasMultiFileSave(GetBackupMultiFileDirectoryPath()) ||
               File.Exists(GetSavePath()) ||
               File.Exists(GetBackupPath());
    }

    public void Save()
    {
        Save(_gameSessionService?.State, "SyncSave");
    }

    public void Save(string reason)
    {
        Save(_gameSessionService?.State, reason);
    }

    public void Save(GameRuntimeState state)
    {
        Save(state, "SyncSave");
    }

    public void Save(
        GameRuntimeState state,
        string reason)
    {
        if (_stagedSaveInProgress)
            CancelStagedSave("SyncSaveOverride");

        if (_saveConfig.UseMultiFileSave)
        {
            SaveMultiFileSynchronously(
                state,
                string.IsNullOrWhiteSpace(reason) ? "SyncSave" : reason);

            return;
        }

        SaveSynchronously(
            state,
            string.IsNullOrWhiteSpace(reason) ? "SyncSave" : reason);
    }

    public GameRuntimeState Load()
    {
        if (_saveConfig.UseMultiFileSave)
        {
            GameRuntimeState multiFileSave =
                TryLoadMultiFileFromDirectory(GetActiveMultiFileDirectoryPath(), "active");

            if (multiFileSave != null)
                return multiFileSave;

            AppLog.Warning("[SaveService] Active multi-file save failed. Trying multi-file backup.");

            GameRuntimeState multiFileBackup =
                TryLoadMultiFileFromDirectory(GetBackupMultiFileDirectoryPath(), "backup");

            if (multiFileBackup != null)
                return multiFileBackup;
        }

        GameRuntimeState mainSave = TryLoadFromPath(GetSavePath());

        if (mainSave != null)
        {
            LogCustom("[SaveService] Main save loaded.");
            return mainSave;
        }

        AppLog.Warning("[SaveService] Main save failed. Trying backup.");

        GameRuntimeState backupSave = TryLoadFromPath(GetBackupPath());

        if (backupSave != null)
        {
            AppLog.Warning("[SaveService] Backup save loaded.");
            return backupSave;
        }

        AppLog.Warning("[SaveService] No valid save found.");
        return null;
    }

    public void DeleteSave()
    {
        if (_stagedSaveInProgress)
            CancelStagedSave("DeleteSave");

        DeleteFileIfExists(GetSavePath());
        DeleteFileIfExists(GetBackupPath());
        DeleteFileIfExists(GetTempPath());
        DeleteDirectoryIfExists(GetMultiFileRootPath());
    }

    public void Tick(float deltaTime)
    {
        if (!_enabledSave)
            return;

        if (_stagedSaveInProgress)
        {
            ProcessStagedSaveStep();
            return;
        }

        _autosaveTimer += deltaTime;

        if (_autosaveTimer >= _autosaveIntervalSeconds)
        {
            QueueStagedSave(
                _gameSessionService?.State,
                "Autosave");
        }
    }

    private void QueueStagedSave(
        GameRuntimeState state,
        string reason)
    {
        if (!_enabledSave)
            return;

        if (!_saveConfig.UseMultiFileSave)
        {
            QueueLegacyStagedSave(state, reason);
            return;
        }

        if (state == null)
        {
            AppLog.Warning("[SaveService] Multi-file staged save skipped: GameState is null.");
            return;
        }

        if (_stagedSaveInProgress)
        {
            _stagedSaveQueuedAgain = true;

            LogSavePerf(
                0d,
                "[SaveService] MULTI_SAVE_ALREADY_RUNNING" +
                " | Reason=" + reason +
                " | CurrentReason=" + _stagedSaveReason +
                " | UnityFrame=" + Time.frameCount +
                " | Tick=" + GetCurrentTick());

            return;
        }

        BeginMultiFileStagedSave(state, reason);
    }

    private void BeginMultiFileStagedSave(
        GameRuntimeState state,
        string reason)
    {
        _stagedSaveInProgress = true;
        _stagedSaveQueuedAgain = false;
        _stagedSavePhase = StagedSavePhase.PrepareBase;
        _stagedSaveState = state;
        _stagedSaveReason =
            string.IsNullOrWhiteSpace(reason) ? "StagedSave" : reason;
        _stagedSaveId =
            DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "_" +
            Guid.NewGuid().ToString("N");

        _stagedDirectoryPath =
            Path.Combine(
                GetStagingMultiFileDirectoryPath(),
                _stagedSaveId);

        _npcCaptureSession = null;
        _partWorkItems.Clear();
        _currentPartIndex = 0;

        _stagedSaveTotalStartedAt = BeginPerfMeasure();
        _stagedSaveStartUnityFrame = Time.frameCount;
        _stagedSaveStartTick = GetCurrentTick();

        _stagedPrepareBaseMs = 0d;
        _stagedNpcPrepareMs = 0d;
        _stagedJsonMs = 0d;
        _stagedWriteMs = 0d;

        _stagedSavedNpcCount = 0;
        _stagedSavedNpcSnapshotEntryCount = 0;
        _stagedSavedLegacyNpcCount = 0;
        _stagedTotalJsonChars = 0;

        _stagedGc0Before = GC.CollectionCount(0);
        _stagedGc1Before = GC.CollectionCount(1);
        _stagedGc2Before = GC.CollectionCount(2);
        _stagedManagedMemoryBefore = GC.GetTotalMemory(false);
        _stagedMonoUsedBefore =
            UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
        _stagedTotalAllocatedBefore =
            UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();

        RecreateDirectory(_stagedDirectoryPath);

        LogSavePerf(
            0d,
            "[SaveService] MULTI_SAVE_QUEUED" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + _stagedSaveStartUnityFrame +
            " | Tick=" + _stagedSaveStartTick +
            " | BudgetMs=" + _saveConfig.NormalizedSaveStepBudgetMs.ToString("F2"));
    }

    private void ProcessStagedSaveStep()
    {
        if (!_stagedSaveInProgress)
            return;

        if (_stagedSaveState == null)
        {
            FailStagedSave("GameState is null.");
            return;
        }

        try
        {
            switch (_stagedSavePhase)
            {
                case StagedSavePhase.PrepareBase:
                    ProcessMultiFilePrepareBaseStep();
                    break;

                case StagedSavePhase.PrepareNpc:
                    ProcessMultiFilePrepareNpcStep();
                    break;

                case StagedSavePhase.ValidateState:
                    ProcessMultiFileValidateStateStep();
                    break;

                case StagedSavePhase.StampIntegrity:
                    ProcessMultiFileStampIntegrityStep();
                    break;

                case StagedSavePhase.BuildParts:
                    ProcessMultiFileBuildPartsStep();
                    break;

                case StagedSavePhase.JsonPart:
                    ProcessMultiFileJsonPartStep();
                    break;

                case StagedSavePhase.WritePart:
                    ProcessMultiFileWritePartStep();
                    break;

                case StagedSavePhase.WriteManifest:
                    ProcessMultiFileWriteManifestStep();
                    break;

                case StagedSavePhase.ValidateManifest:
                    ProcessMultiFileValidateManifestStep();
                    break;

                case StagedSavePhase.ValidateManifestPart:
                    ProcessMultiFileValidateManifestPartStep();
                    break;

                case StagedSavePhase.BackupDeletePrepare:
                    ProcessMultiFileBackupDeletePrepareStep();
                    break;

                case StagedSavePhase.BackupDeletePart:
                    ProcessMultiFileBackupDeletePartStep();
                    break;

                case StagedSavePhase.BackupDeleteFinalize:
                    ProcessMultiFileBackupDeleteFinalizeStep();
                    break;

                case StagedSavePhase.BackupPrepare:
                    ProcessMultiFileBackupPrepareStep();
                    break;

                case StagedSavePhase.BackupCopyPart:
                    ProcessMultiFileBackupCopyPartStep();
                    break;

                case StagedSavePhase.BackupFinalize:
                    ProcessMultiFileBackupFinalizeStep();
                    break;

                case StagedSavePhase.ActiveDeletePrepare:
                    ProcessMultiFileActiveDeletePrepareStep();
                    break;

                case StagedSavePhase.ActiveDeletePart:
                    ProcessMultiFileActiveDeletePartStep();
                    break;

                case StagedSavePhase.ActiveDeleteFinalize:
                    ProcessMultiFileActiveDeleteFinalizeStep();
                    break;

                case StagedSavePhase.Activate:
                    ProcessMultiFileActivateStep();
                    break;

                case StagedSavePhase.Complete:
                    ProcessMultiFileCompleteStep();
                    break;

                default:
                    FailStagedSave("Invalid staged save phase: " + _stagedSavePhase);
                    break;
            }
        }
        catch (Exception exception)
        {
            FailStagedSave(exception.Message);
        }
    }

    private void ProcessMultiFilePrepareBaseStep()
    {
        long startedAt = BeginPerfMeasure();

        PrepareBaseStateBeforeSave(_stagedSaveState);

        _stagedPrepareBaseMs += EndPerfMeasureMs(startedAt);

        LogSavePerf(
            _stagedPrepareBaseMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=PrepareBase" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick());

        _npcCaptureSession =
            _systemNpcSimulationSaveService != null
                ? _systemNpcSimulationSaveService.BeginIncrementalCapture()
                : null;

        _stagedSavePhase = StagedSavePhase.PrepareNpc;
    }

    private void ProcessMultiFilePrepareNpcStep()
    {
        long startedAt = BeginPerfMeasure();

        bool complete = true;

        if (_systemNpcSimulationSaveService != null)
        {
            if (_npcCaptureSession == null)
                _npcCaptureSession =
                    _systemNpcSimulationSaveService.BeginIncrementalCapture();

            complete =
                _systemNpcSimulationSaveService.ContinueIncrementalCapture(
                    _npcCaptureSession,
                    _saveConfig.NormalizedSaveStepBudgetMs,
                    _saveConfig.NormalizedMaxNpcCaptureItemsPerStep);
        }

        double elapsedMs =
            EndPerfMeasureMs(startedAt);

        _stagedNpcPrepareMs += elapsedMs;

        LogSavePerf(
            elapsedMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=PrepareNpc" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick() +
            " | Complete=" + complete +
            " | RuntimeIndex=" +
            (_npcCaptureSession != null ? _npcCaptureSession.NextRuntimeNpcIndex : 0) +
            " | Entries=" +
            (_npcCaptureSession != null && _npcCaptureSession.SaveData != null
                ? _npcCaptureSession.SaveData.PopulationEntries.Count
                : 0));

        if (complete)
            _stagedSavePhase = StagedSavePhase.ValidateState;
    }

    private void ProcessMultiFileValidateStateStep()
    {
        long startedAt = BeginPerfMeasure();

        if (_npcCaptureSession != null)
        {
            _stagedSaveState.SystemNpcSimulation =
                _npcCaptureSession.SaveData;
        }
        else if (_systemNpcSimulationSaveService != null)
        {
            _stagedSaveState.SystemNpcSimulation =
                _systemNpcSimulationSaveService.Capture();
        }

        if (_systemEncounterSaveService != null)
            _stagedSaveState.SystemEncounter =
                _systemEncounterSaveService.Capture();

        SaveValidationResult validation =
            _validationStage.ValidateAndNormalize(_stagedSaveState);

        if (!validation.IsValid)
        {
            throw new InvalidDataException(
                "[SaveService] Save validation failed: " +
                validation.BuildErrorMessage());
        }

        _stagedValidateMs += EndPerfMeasureMs(startedAt);

        LogSavePerf(
            _stagedValidateMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=ValidateState" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick());

        _stagedSavePhase = StagedSavePhase.StampIntegrity;
    }

    private void ProcessMultiFileStampIntegrityStep()
    {
        long startedAt = BeginPerfMeasure();

        if (_stagedSaveState != null &&
            _stagedSaveState.Meta != null)
        {
            // Для multi-file save целостность проверяется через manifest:
            // каждая часть имеет JsonChars и SHA-256.
            // Полный IntegrityChecksum GameRuntimeState не считаем,
            // потому что он заново сериализует весь save одним большим JSON.
            _stagedSaveState.Meta.IntegrityChecksum = string.Empty;
        }

        _stagedStampMs += EndPerfMeasureMs(startedAt);

        LogSavePerf(
            _stagedStampMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=StampIntegrity" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick() +
            " | Mode=ManifestPartChecksums");

        _stagedSavePhase = StagedSavePhase.BuildParts;
    }

    private void ProcessMultiFileBuildPartsStep()
    {
        long startedAt = BeginPerfMeasure();

        _stagedSavedNpcCount =
            GetSavedNpcCount(_stagedSaveState);

        _stagedSavedNpcSnapshotEntryCount =
            GetSavedNpcSnapshotEntryCount(_stagedSaveState);

        _stagedSavedLegacyNpcCount =
            GetSavedLegacyNpcCount(_stagedSaveState);

        BuildPartWorkItems(_stagedSaveState);

        _stagedBuildPartsMs += EndPerfMeasureMs(startedAt);

        LogSavePerf(
            _stagedBuildPartsMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=BuildParts" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick() +
            " | Parts=" + _partWorkItems.Count +
            " | SavedNpcs=" + _stagedSavedNpcCount +
            " | SavedNpcSnapshotEntries=" + _stagedSavedNpcSnapshotEntryCount +
            " | SavedLegacyNpcs=" + _stagedSavedLegacyNpcCount);

        _currentPartIndex = 0;
        _stagedSavePhase = StagedSavePhase.JsonPart;
    }

    private void ProcessMultiFileJsonPartStep()
    {
        if (_currentPartIndex >= _partWorkItems.Count)
        {
            _stagedSavePhase = StagedSavePhase.WriteManifest;
            return;
        }

        SavePartWorkItem item =
            _partWorkItems[_currentPartIndex];

        long startedAt = BeginPerfMeasure();

        item.Json =
            JsonUtility.ToJson(item.Payload, true);

        item.JsonMs =
            EndPerfMeasureMs(startedAt);

        item.JsonChars =
            !string.IsNullOrEmpty(item.Json) ? item.Json.Length : 0;

        item.Sha256 =
            ComputeSha256(item.Json);

        _stagedJsonMs += item.JsonMs;
        _stagedTotalJsonChars += item.JsonChars;

        LogSavePerf(
            item.JsonMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=JsonPart" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick() +
            " | Part=" + item.Name +
            " | Kind=" + item.Kind +
            " | Index=" + item.Index +
            " | JsonChars=" + item.JsonChars);

        _stagedSavePhase = StagedSavePhase.WritePart;
    }

    private void ProcessMultiFileWritePartStep()
    {
        if (_currentPartIndex >= _partWorkItems.Count)
        {
            _stagedSavePhase = StagedSavePhase.WriteManifest;
            return;
        }

        SavePartWorkItem item =
            _partWorkItems[_currentPartIndex];

        if (string.IsNullOrEmpty(item.Json))
        {
            FailStagedSave("Json payload is empty for part: " + item.Name);
            return;
        }

        long startedAt = BeginPerfMeasure();

        WriteTextFileAtomically(
            Path.Combine(_stagedDirectoryPath, item.FileName),
            item.Json);

        item.WriteMs =
            EndPerfMeasureMs(startedAt);

        _stagedWriteMs += item.WriteMs;

        LogSavePerf(
            item.WriteMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=WritePart" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick() +
            " | Part=" + item.Name +
            " | Kind=" + item.Kind +
            " | Index=" + item.Index +
            " | JsonChars=" + item.JsonChars);

        item.Payload = null;
        item.Json = null;

        _currentPartIndex++;

        _stagedSavePhase =
            _currentPartIndex >= _partWorkItems.Count
                ? StagedSavePhase.WriteManifest
                : StagedSavePhase.JsonPart;
    }

    private void ProcessMultiFileWriteManifestStep()
    {
        long startedAt = BeginPerfMeasure();

        _stagedManifest =
            BuildManifest();

        _stagedManifestJson =
            JsonUtility.ToJson(_stagedManifest, true);

        WriteTextFileAtomically(
            Path.Combine(_stagedDirectoryPath, _saveConfig.ManifestFileName),
            _stagedManifestJson);

        _stagedWriteManifestMs += EndPerfMeasureMs(startedAt);

        LogSavePerf(
            _stagedWriteManifestMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=WriteManifest" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick() +
            " | ManifestChars=" +
            (!string.IsNullOrEmpty(_stagedManifestJson)
                ? _stagedManifestJson.Length
                : 0));

        _stagedSavePhase = StagedSavePhase.ValidateManifest;
    }

    private void ProcessMultiFileValidateManifestStep()
    {
        long startedAt = BeginPerfMeasure();

        if (_stagedManifest == null)
            throw new InvalidDataException("Manifest is null.");

        if (_stagedManifest.Parts == null)
            throw new InvalidDataException("Manifest parts are null.");

        string manifestPath =
            Path.Combine(_stagedDirectoryPath, _saveConfig.ManifestFileName);

        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Manifest file is missing.");

        _manifestValidationPartIndex = 0;

        _stagedValidateManifestMs += EndPerfMeasureMs(startedAt);

        LogSavePerf(
            _stagedValidateManifestMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=ValidateManifest" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick() +
            " | Parts=" + _stagedManifest.Parts.Count);

        _stagedSavePhase =
            _stagedManifest.Parts.Count > 0
                ? StagedSavePhase.ValidateManifestPart
                : StagedSavePhase.BackupDeletePrepare;
    }

    private void ProcessMultiFileBackupPrepareStep()
    {
        long startedAt = BeginPerfMeasure();

        string activePath =
            GetActiveMultiFileDirectoryPath();

        string backupPath =
            GetBackupMultiFileDirectoryPath();

        _backupStagingDirectoryPath =
            backupPath + "_staging";

        _backupCopySourceFiles.Clear();
        _backupCopyFileIndex = 0;

        if (_backupRequired)
        {
            DeleteDirectoryIfExists(_backupStagingDirectoryPath);
            Directory.CreateDirectory(_backupStagingDirectoryPath);

            string[] files =
                Directory.GetFiles(
                    activePath,
                    "*",
                    SearchOption.AllDirectories);

            for (int i = 0; i < files.Length; i++)
                _backupCopySourceFiles.Add(files[i]);
        }

        _stagedBackupPrepareMs += EndPerfMeasureMs(startedAt);

        LogSavePerf(
            _stagedBackupPrepareMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=BackupPrepare" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick() +
            " | BackupRequired=" + _backupRequired +
            " | Files=" + _backupCopySourceFiles.Count);

        _stagedSavePhase =
            _backupRequired && _backupCopySourceFiles.Count > 0
                ? StagedSavePhase.BackupCopyPart
                : StagedSavePhase.BackupFinalize;
    }

    private void ProcessMultiFileBackupDeletePrepareStep()
    {
        long startedAt = BeginPerfMeasure();

        string activePath =
            GetActiveMultiFileDirectoryPath();

        string backupPath =
            GetBackupMultiFileDirectoryPath();

        _backupRequired =
            _saveConfig.CreateBackupBeforeSave &&
            Directory.Exists(activePath);

        _backupDeleteFiles.Clear();
        _backupDeleteDirectories.Clear();
        _backupDeleteFileIndex = 0;

        if (_backupRequired &&
            Directory.Exists(backupPath))
        {
            _backupDeleteFiles.AddRange(
                Directory.GetFiles(
                    backupPath,
                    "*",
                    SearchOption.AllDirectories));

            _backupDeleteDirectories.AddRange(
                GetDirectoriesDeepestFirst(backupPath));
        }

        _stagedBackupDeletePrepareMs += EndPerfMeasureMs(startedAt);

        LogSavePerf(
            _stagedBackupDeletePrepareMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=BackupDeletePrepare" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick() +
            " | BackupRequired=" + _backupRequired +
            " | Files=" + _backupDeleteFiles.Count +
            " | Directories=" + _backupDeleteDirectories.Count);

        if (!_backupRequired)
        {
            _stagedSavePhase = StagedSavePhase.ActiveDeletePrepare;
            return;
        }

        _stagedSavePhase =
            _backupDeleteFiles.Count > 0
                ? StagedSavePhase.BackupDeletePart
                : StagedSavePhase.BackupDeleteFinalize;
    }

    private void ProcessMultiFileBackupDeletePartStep()
    {
        if (_backupDeleteFileIndex >= _backupDeleteFiles.Count)
        {
            _stagedSavePhase = StagedSavePhase.BackupDeleteFinalize;
            return;
        }

        string file =
            _backupDeleteFiles[_backupDeleteFileIndex];

        long startedAt = BeginPerfMeasure();

        if (File.Exists(file))
            File.Delete(file);

        _backupDeleteFileIndex++;

        double elapsedMs =
            EndPerfMeasureMs(startedAt);

        _stagedBackupDeleteMs += elapsedMs;

        LogSavePerf(
            elapsedMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=BackupDeletePart" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick() +
            " | FileIndex=" + _backupDeleteFileIndex +
            " | Files=" + _backupDeleteFiles.Count);

        if (_backupDeleteFileIndex >= _backupDeleteFiles.Count)
            _stagedSavePhase = StagedSavePhase.BackupDeleteFinalize;
    }

    private void ProcessMultiFileBackupDeleteFinalizeStep()
    {
        long startedAt = BeginPerfMeasure();

        for (int i = 0; i < _backupDeleteDirectories.Count; i++)
        {
            string directory =
                _backupDeleteDirectories[i];

            if (Directory.Exists(directory))
                Directory.Delete(directory, false);
        }

        string backupPath =
            GetBackupMultiFileDirectoryPath();

        if (Directory.Exists(backupPath))
            Directory.Delete(backupPath, false);

        _stagedBackupDeleteFinalizeMs += EndPerfMeasureMs(startedAt);

        LogSavePerf(
            _stagedBackupDeleteFinalizeMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=BackupDeleteFinalize" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick());

        _stagedSavePhase = StagedSavePhase.BackupPrepare;
    }

    private void ProcessMultiFileBackupCopyPartStep()
    {
        if (!_backupRequired ||
            _backupCopyFileIndex >= _backupCopySourceFiles.Count)
        {
            _stagedSavePhase = StagedSavePhase.BackupFinalize;
            return;
        }

        long startedAt = BeginPerfMeasure();

        string sourceFile =
            _backupCopySourceFiles[_backupCopyFileIndex];

        string activePath =
            GetActiveMultiFileDirectoryPath();

        string relativePath =
            sourceFile.Substring(activePath.Length).TrimStart(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        string targetFile =
            Path.Combine(_backupStagingDirectoryPath, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
        File.Copy(sourceFile, targetFile, true);

        _backupCopyFileIndex++;

        double elapsedMs =
            EndPerfMeasureMs(startedAt);

        _stagedBackupCopyMs += elapsedMs;

        LogSavePerf(
            elapsedMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=BackupCopyPart" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick() +
            " | FileIndex=" + _backupCopyFileIndex +
            " | Files=" + _backupCopySourceFiles.Count +
            " | File=" + relativePath);

        if (_backupCopyFileIndex >= _backupCopySourceFiles.Count)
            _stagedSavePhase = StagedSavePhase.BackupFinalize;
    }

    private void ProcessMultiFileBackupFinalizeStep()
    {
        long startedAt = BeginPerfMeasure();

        if (_backupRequired)
        {
            string backupPath =
                GetBackupMultiFileDirectoryPath();

            if (Directory.Exists(_backupStagingDirectoryPath))
                Directory.Move(_backupStagingDirectoryPath, backupPath);
        }

        _stagedBackupFinalizeMs += EndPerfMeasureMs(startedAt);

        LogSavePerf(
            _stagedBackupFinalizeMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=BackupFinalize" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick() +
            " | BackupRequired=" + _backupRequired);

        _stagedSavePhase = StagedSavePhase.ActiveDeletePrepare;
    }

    private void ProcessMultiFileActiveDeletePrepareStep()
    {
        long startedAt = BeginPerfMeasure();

        string activePath =
            GetActiveMultiFileDirectoryPath();

        _activeDeleteFiles.Clear();
        _activeDeleteDirectories.Clear();
        _activeDeleteFileIndex = 0;

        if (Directory.Exists(activePath))
        {
            _activeDeleteFiles.AddRange(
                Directory.GetFiles(
                    activePath,
                    "*",
                    SearchOption.AllDirectories));

            _activeDeleteDirectories.AddRange(
                GetDirectoriesDeepestFirst(activePath));
        }

        _stagedActiveDeletePrepareMs += EndPerfMeasureMs(startedAt);

        LogSavePerf(
            _stagedActiveDeletePrepareMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=ActiveDeletePrepare" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick() +
            " | Files=" + _activeDeleteFiles.Count +
            " | Directories=" + _activeDeleteDirectories.Count);

        _stagedSavePhase =
            _activeDeleteFiles.Count > 0
                ? StagedSavePhase.ActiveDeletePart
                : StagedSavePhase.ActiveDeleteFinalize;
    }

    private void ProcessMultiFileActiveDeletePartStep()
    {
        if (_activeDeleteFileIndex >= _activeDeleteFiles.Count)
        {
            _stagedSavePhase = StagedSavePhase.ActiveDeleteFinalize;
            return;
        }

        string file =
            _activeDeleteFiles[_activeDeleteFileIndex];

        long startedAt = BeginPerfMeasure();

        if (File.Exists(file))
            File.Delete(file);

        _activeDeleteFileIndex++;

        double elapsedMs =
            EndPerfMeasureMs(startedAt);

        _stagedActiveDeleteMs += elapsedMs;

        LogSavePerf(
            elapsedMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=ActiveDeletePart" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick() +
            " | FileIndex=" + _activeDeleteFileIndex +
            " | Files=" + _activeDeleteFiles.Count);

        if (_activeDeleteFileIndex >= _activeDeleteFiles.Count)
            _stagedSavePhase = StagedSavePhase.ActiveDeleteFinalize;
    }

    private void ProcessMultiFileActiveDeleteFinalizeStep()
    {
        long startedAt = BeginPerfMeasure();

        for (int i = 0; i < _activeDeleteDirectories.Count; i++)
        {
            string directory =
                _activeDeleteDirectories[i];

            if (Directory.Exists(directory))
                Directory.Delete(directory, false);
        }

        string activePath =
            GetActiveMultiFileDirectoryPath();

        if (Directory.Exists(activePath))
            Directory.Delete(activePath, false);

        _stagedActiveDeleteFinalizeMs += EndPerfMeasureMs(startedAt);

        LogSavePerf(
            _stagedActiveDeleteFinalizeMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=ActiveDeleteFinalize" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick());

        _stagedSavePhase = StagedSavePhase.Activate;
    }

    private static List<string> GetDirectoriesDeepestFirst(string rootPath)
    {
        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(rootPath) ||
            !Directory.Exists(rootPath))
        {
            return result;
        }

        string[] directories =
            Directory.GetDirectories(
                rootPath,
                "*",
                SearchOption.AllDirectories);

        result.AddRange(directories);

        result.Sort((left, right) =>
            right.Length.CompareTo(left.Length));

        return result;
    }

    private void ProcessMultiFileActivateStep()
    {
        long startedAt = BeginPerfMeasure();

        string activePath =
            GetActiveMultiFileDirectoryPath();

        Directory.CreateDirectory(GetMultiFileRootPath());
        Directory.Move(_stagedDirectoryPath, activePath);

        _stagedActivateMs += EndPerfMeasureMs(startedAt);

        LogSavePerf(
            _stagedActivateMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=Activate" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick() +
            " | Active=" + activePath);

        _stagedSavePhase = StagedSavePhase.Complete;
    }

    private void ProcessMultiFileCompleteStep()
    {
        _eventBus?.Publish(new GameSavedEvent());
        _autosaveTimer = 0f;

        int gc0After = GC.CollectionCount(0);
        int gc1After = GC.CollectionCount(1);
        int gc2After = GC.CollectionCount(2);

        long managedMemoryAfter = GC.GetTotalMemory(false);
        long monoUsedAfter =
            UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
        long totalAllocatedAfter =
            UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();

        double totalMs =
            EndPerfMeasureMs(_stagedSaveTotalStartedAt);

        double commitMs =
            _stagedWriteManifestMs +
            _stagedValidateManifestMs +
            _stagedBackupDeletePrepareMs +
            _stagedBackupDeleteMs +
            _stagedBackupDeleteFinalizeMs +
            _stagedBackupPrepareMs +
            _stagedBackupCopyMs +
            _stagedBackupFinalizeMs +
            _stagedActiveDeletePrepareMs +
            _stagedActiveDeleteMs +
            _stagedActiveDeleteFinalizeMs +
            _stagedActivateMs;

        LogSavePerf(
            totalMs,
            "[SaveService] MULTI_SAVE_COMPLETE" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | StartUnityFrame=" + _stagedSaveStartUnityFrame +
            " | EndUnityFrame=" + Time.frameCount +
            " | StartTick=" + _stagedSaveStartTick +
            " | EndTick=" + GetCurrentTick() +
            " | PrepareBaseMs=" + _stagedPrepareBaseMs.ToString("F2") +
            " | PrepareNpcMs=" + _stagedNpcPrepareMs.ToString("F2") +
            " | ValidateMs=" + _stagedValidateMs.ToString("F2") +
            " | StampMs=" + _stagedStampMs.ToString("F2") +
            " | BuildPartsMs=" + _stagedBuildPartsMs.ToString("F2") +
            " | JsonMs=" + _stagedJsonMs.ToString("F2") +
            " | WriteMs=" + _stagedWriteMs.ToString("F2") +
            " | WriteManifestMs=" + _stagedWriteManifestMs.ToString("F2") +
            " | ValidateManifestMs=" + _stagedValidateManifestMs.ToString("F2") +
            " | BackupDeletePrepareMs=" + _stagedBackupDeletePrepareMs.ToString("F2") +
            " | BackupDeleteMs=" + _stagedBackupDeleteMs.ToString("F2") +
            " | BackupDeleteFinalizeMs=" + _stagedBackupDeleteFinalizeMs.ToString("F2") +
            " | BackupPrepareMs=" + _stagedBackupPrepareMs.ToString("F2") +
            " | BackupCopyMs=" + _stagedBackupCopyMs.ToString("F2") +
            " | BackupFinalizeMs=" + _stagedBackupFinalizeMs.ToString("F2") +
            " | ActiveDeletePrepareMs=" + _stagedActiveDeletePrepareMs.ToString("F2") +
            " | ActiveDeleteMs=" + _stagedActiveDeleteMs.ToString("F2") +
            " | ActiveDeleteFinalizeMs=" + _stagedActiveDeleteFinalizeMs.ToString("F2") +
            " | ActivateMs=" + _stagedActivateMs.ToString("F2") +
            " | CommitMs=" + commitMs.ToString("F2") +
            " | Parts=" + _partWorkItems.Count +
            " | SavedNpcs=" + _stagedSavedNpcCount +
            " | SavedNpcSnapshotEntries=" + _stagedSavedNpcSnapshotEntryCount +
            " | SavedLegacyNpcs=" + _stagedSavedLegacyNpcCount +
            " | TotalJsonChars=" + _stagedTotalJsonChars +
            " | Gc0Delta=" + (gc0After - _stagedGc0Before) +
            " | Gc1Delta=" + (gc1After - _stagedGc1Before) +
            " | Gc2Delta=" + (gc2After - _stagedGc2Before) +
            " | ManagedMemoryDeltaMb=" +
            BytesToMegabytes(managedMemoryAfter - _stagedManagedMemoryBefore).ToString("F2") +
            " | MonoUsedDeltaMb=" +
            BytesToMegabytes(monoUsedAfter - _stagedMonoUsedBefore).ToString("F2") +
            " | TotalAllocatedDeltaMb=" +
            BytesToMegabytes(totalAllocatedAfter - _stagedTotalAllocatedBefore).ToString("F2"));

        bool queueAgain =
            _stagedSaveQueuedAgain;

        ClearStagedSaveState();

        if (queueAgain)
        {
            QueueStagedSave(
                _gameSessionService?.State,
                "QueuedAgainAfterComplete");
        }
    }

    private void SaveMultiFileSynchronously(
        GameRuntimeState state,
        string reason)
    {
        if (!_enabledSave)
            return;

        if (state == null)
        {
            AppLog.Warning("[SaveService] Multi-file sync save skipped: GameState is null.");
            return;
        }

        BeginMultiFileStagedSave(state, reason);

        while (_stagedSaveInProgress)
            ProcessStagedSaveStep();
    }

    private void PrepareBaseStateBeforeSave(GameRuntimeState state)
    {
        _migrationStage.Run(state);

        TryWriteGameTimeToState(state);
        TryWriteShipMovementToState(state);

        _migrationStage.Run(state);

        state.Meta.SaveVersion++;
        state.Meta.LastSaveUtc = DateTime.UtcNow.Ticks;
        state.Meta.LastSaveReason = _stagedSaveReason;

        DictionaryToList(state);
    }

    private void BuildPartWorkItems(GameRuntimeState state)
    {
        _partWorkItems.Clear();

        _partWorkItems.Add(new SavePartWorkItem
        {
            Name = "core",
            FileName = "core.json",
            Kind = "core",
            Index = 0,
            Payload = new SaveCorePartData
            {
                Meta = state.Meta,
                Player = state.Player,
                Settings = state.Settings,
                Markets = state.Markets
            }
        });

        _partWorkItems.Add(new SavePartWorkItem
        {
            Name = "galaxy",
            FileName = "galaxy.json",
            Kind = "galaxy",
            Index = 0,
            Payload = new SaveGalaxyPartData
            {
                Galaxy = state.Galaxy
            }
        });

        _partWorkItems.Add(new SavePartWorkItem
        {
            Name = "missions",
            FileName = "missions.json",
            Kind = "missions",
            Index = 0,
            Payload = new SaveMissionPartData
            {
                MissionBlock = state.MissionBlock
            }
        });

        _partWorkItems.Add(new SavePartWorkItem
        {
            Name = "encounter",
            FileName = "encounter.json",
            Kind = "encounter",
            Index = 0,
            Payload = new SaveEncounterPartData
            {
                SystemEncounter = state.SystemEncounter
            }
        });

        _partWorkItems.Add(new SavePartWorkItem
        {
            Name = "npc_timers",
            FileName = "npc_timers.json",
            Kind = "npc_timers",
            Index = 0,
            Payload = new SaveNpcTimersPartData
            {
                PopulationTimers = state.SystemNpcSimulation.PopulationTimers
            }
        });

        AddNpcPartWorkItems(state.SystemNpcSimulation);
    }

    private void AddNpcPartWorkItems(SystemNpcSimulationSaveData npcSaveData)
    {
        if (npcSaveData == null ||
            npcSaveData.PopulationEntries == null ||
            npcSaveData.PopulationEntries.Count == 0)
        {
            _partWorkItems.Add(new SavePartWorkItem
            {
                Name = "npc_000",
                FileName = "npc_000.json",
                Kind = "npc",
                Index = 0,
                Payload = new SaveNpcPartData()
            });

            return;
        }

        int maxEntries =
            _saveConfig.NormalizedMaxNpcSnapshotEntriesPerPart;

        int partIndex = 0;

        for (int startIndex = 0;
             startIndex < npcSaveData.PopulationEntries.Count;
             startIndex += maxEntries)
        {
            var part = new SaveNpcPartData();

            int endIndex =
                Mathf.Min(
                    startIndex + maxEntries,
                    npcSaveData.PopulationEntries.Count);

            for (int index = startIndex; index < endIndex; index++)
                part.PopulationEntries.Add(npcSaveData.PopulationEntries[index]);

            string fileName =
                "npc_" + partIndex.ToString("D3") + ".json";

            _partWorkItems.Add(new SavePartWorkItem
            {
                Name = Path.GetFileNameWithoutExtension(fileName),
                FileName = fileName,
                Kind = "npc",
                Index = partIndex,
                Payload = part
            });

            partIndex++;
        }
    }

    private SaveMultiFileManifest BuildManifest()
    {
        var manifest = new SaveMultiFileManifest
        {
            FormatVersion = 1,
            SaveId = _stagedSaveId,
            CreatedUtcTicks = DateTime.UtcNow.Ticks,
            SaveDataVersion =
                _stagedSaveState != null && _stagedSaveState.Meta != null
                    ? _stagedSaveState.Meta.SaveDataVersion
                    : 0,
            SaveVersion =
                _stagedSaveState != null && _stagedSaveState.Meta != null
                    ? _stagedSaveState.Meta.SaveVersion
                    : 0,
            PartCount = _partWorkItems.Count,
            TotalJsonChars = _stagedTotalJsonChars
        };

        for (int i = 0; i < _partWorkItems.Count; i++)
        {
            SavePartWorkItem item =
                _partWorkItems[i];

            manifest.Parts.Add(new SaveMultiFilePartInfo
            {
                Name = item.Name,
                FileName = item.FileName,
                Kind = item.Kind,
                Index = item.Index,
                JsonChars = item.JsonChars,
                Sha256 = item.Sha256
            });
        }

        return manifest;
    }

    private GameRuntimeState TryLoadMultiFileFromDirectory(
        string directoryPath,
        string source)
    {
        string manifestPath =
            Path.Combine(directoryPath, _saveConfig.ManifestFileName);

        if (!File.Exists(manifestPath))
            return null;

        try
        {
            string manifestJson =
                File.ReadAllText(manifestPath);

            SaveMultiFileManifest manifest =
                JsonUtility.FromJson<SaveMultiFileManifest>(manifestJson);

            if (manifest == null ||
                manifest.Parts == null ||
                manifest.Parts.Count == 0)
            {
                AppLog.Error("[SaveService] Multi-file manifest is invalid: " + source);
                return null;
            }

            ValidateMultiFileDirectoryOrThrow(directoryPath, manifest);

            var state = new GameRuntimeState();
            state.SystemNpcSimulation = new SystemNpcSimulationSaveData();

            for (int i = 0; i < manifest.Parts.Count; i++)
            {
                SaveMultiFilePartInfo part =
                    manifest.Parts[i];

                string partJson =
                    File.ReadAllText(Path.Combine(directoryPath, part.FileName));

                ApplyLoadedPart(state, part, partJson);
            }

            SaveValidationResult versionValidation =
                _validationStage.ValidateVersion(state);

            if (!versionValidation.IsValid)
            {
                AppLog.Error(
                    "[SaveService] Unsupported multi-file save version: " +
                    versionValidation.BuildErrorMessage());
                return null;
            }

            bool wasMigrated =
                _migrationStage.Run(state);

            if (wasMigrated)
            {
                AppLog.Info(
                    "[SaveService] Multi-file save migrated to data version " +
                    state.Meta.SaveDataVersion);
            }

            DictionaryFromList(state);

            SaveValidationResult validation =
                _validationStage.ValidateAndNormalize(state);

            if (!validation.IsValid)
            {
                AppLog.Error(
                    "[SaveService] Multi-file post-load validation failed: " +
                    validation.BuildErrorMessage());
                return null;
            }

            TryRestoreGameTimeFromState(state);
            TryInitializeShipMovementFromState(state);

            if (_systemEncounterSaveService != null &&
                state.SystemEncounter != null)
            {
                _systemEncounterSaveService.Restore(state.SystemEncounter);
            }

            if (_systemNpcSimulationSaveService != null &&
                state.SystemNpcSimulation != null)
            {
                _systemNpcSimulationSaveService.Restore(state.SystemNpcSimulation);
            }

            LogSavePerf(
                0d,
                "[SaveService] MULTI_SAVE_LOADED" +
                " | Source=" + source +
                " | SaveId=" + manifest.SaveId +
                " | Parts=" + manifest.Parts.Count +
                " | Integrity=ManifestPartChecksums" +
                " | SavedNpcs=" + GetSavedNpcCount(state) +
                " | SavedNpcSnapshotEntries=" + GetSavedNpcSnapshotEntryCount(state) +
                " | SavedLegacyNpcs=" + GetSavedLegacyNpcCount(state));

            return state;
        }
        catch (Exception exception)
        {
            AppLog.Error(
                "[SaveService] Failed to load multi-file save from " +
                source +
                ": " +
                exception.Message);

            return null;
        }
    }

    private void ApplyLoadedPart(
        GameRuntimeState state,
        SaveMultiFilePartInfo part,
        string json)
    {
        switch (part.Kind)
        {
            case "core":
                {
                    SaveCorePartData data =
                        JsonUtility.FromJson<SaveCorePartData>(json);

                    if (data != null)
                    {
                        state.Meta = data.Meta;
                        state.Player = data.Player;
                        state.Settings = data.Settings;
                        state.Markets = data.Markets;
                    }

                    break;
                }

            case "galaxy":
                {
                    SaveGalaxyPartData data =
                        JsonUtility.FromJson<SaveGalaxyPartData>(json);

                    if (data != null)
                        state.Galaxy = data.Galaxy;

                    break;
                }

            case "missions":
                {
                    SaveMissionPartData data =
                        JsonUtility.FromJson<SaveMissionPartData>(json);

                    if (data != null)
                        state.MissionBlock = data.MissionBlock;

                    break;
                }

            case "encounter":
                {
                    SaveEncounterPartData data =
                        JsonUtility.FromJson<SaveEncounterPartData>(json);

                    if (data != null)
                        state.SystemEncounter = data.SystemEncounter;

                    break;
                }

            case "npc_timers":
                {
                    SaveNpcTimersPartData data =
                        JsonUtility.FromJson<SaveNpcTimersPartData>(json);

                    if (data != null)
                        state.SystemNpcSimulation.PopulationTimers =
                            data.PopulationTimers;

                    break;
                }

            case "npc":
                {
                    SaveNpcPartData data =
                        JsonUtility.FromJson<SaveNpcPartData>(json);

                    if (data != null &&
                        data.PopulationEntries != null)
                    {
                        state.SystemNpcSimulation.PopulationEntries.AddRange(
                            data.PopulationEntries);
                    }

                    break;
                }
        }
    }

    private void ValidateMultiFileDirectoryOrThrow(
        string directoryPath,
        SaveMultiFileManifest manifest)
    {
        if (manifest == null)
            throw new InvalidDataException("Manifest is null.");

        if (manifest.Parts == null)
            throw new InvalidDataException("Manifest parts are null.");

        for (int i = 0; i < manifest.Parts.Count; i++)
        {
            ValidateMultiFilePartOrThrow(
                directoryPath,
                manifest.Parts[i]);
        }

        LogSavePerf(
            0d,
            "[SaveService] MULTI_SAVE_VALIDATE" +
            " | Directory=" + directoryPath +
            " | Parts=" + manifest.Parts.Count +
            " | SaveId=" + manifest.SaveId);
    }

    private void CommitStagedMultiFileDirectory()
    {
        string activePath =
            GetActiveMultiFileDirectoryPath();

        string backupPath =
            GetBackupMultiFileDirectoryPath();

        Directory.CreateDirectory(GetMultiFileRootPath());

        if (_saveConfig.CreateBackupBeforeSave &&
            Directory.Exists(activePath))
        {
            DeleteDirectoryIfExists(backupPath);
            CopyDirectory(activePath, backupPath);

            LogSavePerf(
                0d,
                "[SaveService] MULTI_SAVE_BACKUP" +
                " | From=" + activePath +
                " | To=" + backupPath);
        }

        DeleteDirectoryIfExists(activePath);
        Directory.Move(_stagedDirectoryPath, activePath);

        LogSavePerf(
            0d,
            "[SaveService] MULTI_SAVE_ACTIVE_REPLACED" +
            " | Active=" + activePath);
    }

    private void QueueLegacyStagedSave(
        GameRuntimeState state,
        string reason)
    {
        if (state == null)
        {
            AppLog.Warning("[SaveService] Staged save skipped: GameState is null.");
            return;
        }

        SaveSynchronously(
            state,
            string.IsNullOrWhiteSpace(reason) ? "StagedSaveLegacyFallback" : reason);
    }

    private void SaveSynchronously(
        GameRuntimeState state,
        string reason)
    {
        if (!_enabledSave)
            return;

        if (state == null)
        {
            AppLog.Warning("[SaveService] Save skipped: GameState is null.");
            return;
        }

        int unityFrame = Time.frameCount;
        int currentTick = GetCurrentTick();

        int gc0Before = GC.CollectionCount(0);
        int gc1Before = GC.CollectionCount(1);
        int gc2Before = GC.CollectionCount(2);

        long managedMemoryBefore = GC.GetTotalMemory(false);
        long monoUsedBefore =
            UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
        long totalAllocatedBefore =
            UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();

        long totalStartedAt = BeginPerfMeasure();
        double prepareMs = 0.0;
        double jsonMs = 0.0;
        double writeMs = 0.0;
        int jsonLength = 0;
        int savedNpcCount = 0;
        int savedNpcSnapshotEntryCount = 0;
        int savedLegacyNpcCount = 0;

        try
        {
            long prepareStartedAt = BeginPerfMeasure();

            PrepareStateBeforeSave(state);
            _integrityStage.Stamp(state);

            prepareMs = EndPerfMeasureMs(prepareStartedAt);

            savedNpcCount = GetSavedNpcCount(state);
            savedNpcSnapshotEntryCount = GetSavedNpcSnapshotEntryCount(state);
            savedLegacyNpcCount = GetSavedLegacyNpcCount(state);

            long jsonStartedAt = BeginPerfMeasure();

            string json = JsonUtility.ToJson(state, true);

            jsonMs = EndPerfMeasureMs(jsonStartedAt);
            jsonLength = !string.IsNullOrEmpty(json) ? json.Length : 0;

            long writeStartedAt = BeginPerfMeasure();

            WriteSaveAtomically(json);

            writeMs = EndPerfMeasureMs(writeStartedAt);

            _eventBus?.Publish(new GameSavedEvent());
            _autosaveTimer = 0f;

            LogCustom("Game saved to: " + GetSavePath());

            int gc0After = GC.CollectionCount(0);
            int gc1After = GC.CollectionCount(1);
            int gc2After = GC.CollectionCount(2);

            long managedMemoryAfter = GC.GetTotalMemory(false);
            long monoUsedAfter =
                UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            long totalAllocatedAfter =
                UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();

            LogSavePerf(
                EndPerfMeasureMs(totalStartedAt),
                "[SaveService] Save" +
                " | Reason=" + reason +
                " | UnityFrame=" + unityFrame +
                " | Tick=" + currentTick +
                " | PrepareMs=" + prepareMs.ToString("F2") +
                " | JsonMs=" + jsonMs.ToString("F2") +
                " | WriteMs=" + writeMs.ToString("F2") +
                " | SavedNpcs=" + savedNpcCount +
                " | SavedNpcSnapshotEntries=" + savedNpcSnapshotEntryCount +
                " | SavedLegacyNpcs=" + savedLegacyNpcCount +
                " | JsonChars=" + jsonLength +
                " | Gc0Delta=" + (gc0After - gc0Before) +
                " | Gc1Delta=" + (gc1After - gc1Before) +
                " | Gc2Delta=" + (gc2After - gc2Before) +
                " | ManagedMemoryDeltaMb=" +
                BytesToMegabytes(managedMemoryAfter - managedMemoryBefore).ToString("F2") +
                " | MonoUsedDeltaMb=" +
                BytesToMegabytes(monoUsedAfter - monoUsedBefore).ToString("F2") +
                " | TotalAllocatedDeltaMb=" +
                BytesToMegabytes(totalAllocatedAfter - totalAllocatedBefore).ToString("F2"));
        }
        catch (Exception e)
        {
            AppLog.Error("[SaveService] Failed to save: " + e.Message);
        }
        finally
        {
            DeleteTempFileIfExists();
        }
    }

    private void PrepareStateBeforeSave(GameRuntimeState state)
    {
        PrepareBaseStateBeforeSave(state);

        if (_systemNpcSimulationSaveService != null)
            state.SystemNpcSimulation =
                _systemNpcSimulationSaveService.Capture();

        if (_systemEncounterSaveService != null)
            state.SystemEncounter =
                _systemEncounterSaveService.Capture();

        SaveValidationResult validation =
            _validationStage.ValidateAndNormalize(state);

        if (!validation.IsValid)
        {
            throw new InvalidDataException(
                "[SaveService] Save validation failed: " +
                validation.BuildErrorMessage());
        }
    }

    private GameRuntimeState TryLoadFromPath(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            string json = File.ReadAllText(path);
            GameRuntimeState state =
                JsonUtility.FromJson<GameRuntimeState>(json);

            if (state == null)
            {
                AppLog.Error("[SaveService] Parsed GameState is null: " + path);
                return null;
            }

            SaveValidationResult versionValidation =
                _validationStage.ValidateVersion(state);

            if (!versionValidation.IsValid)
            {
                AppLog.Error(
                    "[SaveService] Unsupported save version: " +
                    versionValidation.BuildErrorMessage());
                return null;
            }

            SaveIntegrityStatus integrityStatus =
                _integrityStage.Verify(state);

            if (integrityStatus == SaveIntegrityStatus.Invalid)
            {
                AppLog.Error(
                    "[SaveService] Save integrity verification failed: " +
                    path);
                return null;
            }

            if (integrityStatus == SaveIntegrityStatus.Missing)
            {
                AppLog.Warning(
                    "[SaveService] Legacy save has no integrity checksum: " +
                    path);
            }

            bool wasMigrated =
                _migrationStage.Run(state);

            if (wasMigrated)
            {
                AppLog.Info(
                    "[SaveService] Save migrated to data version " +
                    state.Meta.SaveDataVersion);
            }

            DictionaryFromList(state);

            SaveValidationResult validation =
                _validationStage.ValidateAndNormalize(state);

            if (!validation.IsValid)
            {
                AppLog.Error(
                    "[SaveService] Post-load validation failed: " +
                    validation.BuildErrorMessage());
                return null;
            }

            TryRestoreGameTimeFromState(state);
            TryInitializeShipMovementFromState(state);

            if (_systemEncounterSaveService != null &&
                state.SystemEncounter != null)
            {
                _systemEncounterSaveService.Restore(state.SystemEncounter);
            }

            if (_systemNpcSimulationSaveService != null &&
                state.SystemNpcSimulation != null)
            {
                _systemNpcSimulationSaveService.Restore(state.SystemNpcSimulation);
            }

            return state;
        }
        catch (Exception e)
        {
            AppLog.Error("[SaveService] Failed to load from " + path + ": " + e.Message);
            return null;
        }
    }

    private void FailStagedSave(string message)
    {
        AppLog.Error("[SaveService] Multi-file staged save failed: " + message);

        LogSavePerf(
            EndPerfMeasureMs(_stagedSaveTotalStartedAt),
            "[SaveService] MULTI_SAVE_FAILED" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | Phase=" + _stagedSavePhase +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick() +
            " | Error=" + message);

        DeleteDirectoryIfExists(_stagedDirectoryPath);
        ClearStagedSaveState();
    }

    private void CancelStagedSave(string reason)
    {
        LogSavePerf(
            EndPerfMeasureMs(_stagedSaveTotalStartedAt),
            "[SaveService] MULTI_SAVE_CANCELLED" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | CancelReason=" + reason +
            " | Phase=" + _stagedSavePhase +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick());

        DeleteDirectoryIfExists(_stagedDirectoryPath);
        ClearStagedSaveState();
    }

    private void ClearStagedSaveState()
    {
        _manifestValidationPartIndex = 0;

        _backupDeleteFiles.Clear();
        _backupDeleteDirectories.Clear();
        _backupDeleteFileIndex = 0;

        _activeDeleteFiles.Clear();
        _activeDeleteDirectories.Clear();
        _activeDeleteFileIndex = 0;

        _stagedBackupDeletePrepareMs = 0d;
        _stagedBackupDeleteMs = 0d;
        _stagedBackupDeleteFinalizeMs = 0d;
        _stagedActiveDeletePrepareMs = 0d;
        _stagedActiveDeleteMs = 0d;
        _stagedActiveDeleteFinalizeMs = 0d;

        _stagedManifest = null;
        _stagedManifestJson = null;
        _backupStagingDirectoryPath = string.Empty;
        _backupCopySourceFiles.Clear();
        _backupCopyFileIndex = 0;
        _backupRequired = false;

        _stagedValidateMs = 0d;
        _stagedStampMs = 0d;
        _stagedBuildPartsMs = 0d;
        _stagedWriteManifestMs = 0d;
        _stagedValidateManifestMs = 0d;
        _stagedBackupPrepareMs = 0d;
        _stagedBackupCopyMs = 0d;
        _stagedBackupFinalizeMs = 0d;
        _stagedActiveDeleteMs = 0d;
        _stagedActivateMs = 0d;

        _stagedSaveInProgress = false;
        _stagedSaveQueuedAgain = false;
        _stagedSavePhase = StagedSavePhase.None;
        _stagedSaveState = null;
        _stagedSaveReason = string.Empty;
        _stagedSaveId = string.Empty;
        _stagedDirectoryPath = string.Empty;
        _npcCaptureSession = null;
        _partWorkItems.Clear();
        _currentPartIndex = 0;

        _stagedSaveTotalStartedAt = 0L;
        _stagedSaveStartUnityFrame = -1;
        _stagedSaveStartTick = -1;

        _stagedPrepareBaseMs = 0d;
        _stagedNpcPrepareMs = 0d;
        _stagedJsonMs = 0d;
        _stagedWriteMs = 0d;

        _stagedSavedNpcCount = 0;
        _stagedSavedNpcSnapshotEntryCount = 0;
        _stagedSavedLegacyNpcCount = 0;
        _stagedTotalJsonChars = 0;

        _stagedGc0Before = 0;
        _stagedGc1Before = 0;
        _stagedGc2Before = 0;
        _stagedManagedMemoryBefore = 0L;
        _stagedMonoUsedBefore = 0L;
        _stagedTotalAllocatedBefore = 0L;
    }

    private void TryWriteGameTimeToState(GameRuntimeState state)
    {
        if (state == null)
            return;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        if (Bootstrapper.Instance.ServiceRegistry.TryGet<IGameTimeService>(
            out IGameTimeService gameTimeService))
        {
            gameTimeService.WriteTimeToSave(state);
        }
    }

    private void TryWriteShipMovementToState(GameRuntimeState state)
    {
        if (state == null)
            return;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        if (Bootstrapper.Instance.ServiceRegistry.TryGet<IPlayerShipSaveSyncService>(
                out IPlayerShipSaveSyncService syncService))
        {
            syncService.WriteMovementToSave(state);
        }
    }

    private void TryRestoreGameTimeFromState(GameRuntimeState state)
    {
        if (state == null)
            return;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        if (Bootstrapper.Instance.ServiceRegistry.TryGet<IGameTimeService>(
                out IGameTimeService gameTimeService))
        {
            gameTimeService.RestoreTimeFromSave(state);
        }
    }

    private void TryInitializeShipMovementFromState(GameRuntimeState state)
    {
        if (state == null)
            return;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        if (Bootstrapper.Instance.ServiceRegistry.TryGet<IPlayerShipSaveSyncService>(
                out IPlayerShipSaveSyncService syncService))
        {
            syncService.InitializeMovementFromSave(state);
        }
    }

    private static int GetSavedNpcCount(GameRuntimeState state)
    {
        if (state == null ||
            state.SystemNpcSimulation == null)
        {
            return 0;
        }

        if (state.SystemNpcSimulation.PopulationEntries != null &&
            state.SystemNpcSimulation.PopulationEntries.Count > 0)
        {
            int count = 0;

            for (int i = 0; i < state.SystemNpcSimulation.PopulationEntries.Count; i++)
            {
                SystemNpcPopulationSnapshotEntrySaveData entry =
                    state.SystemNpcSimulation.PopulationEntries[i];

                if (entry != null)
                    count += Mathf.Max(0, entry.Count);
            }

            return count;
        }

        return state.SystemNpcSimulation.Npcs != null
            ? state.SystemNpcSimulation.Npcs.Count
            : 0;
    }

    private static int GetSavedNpcSnapshotEntryCount(GameRuntimeState state)
    {
        if (state == null ||
            state.SystemNpcSimulation == null ||
            state.SystemNpcSimulation.PopulationEntries == null)
        {
            return 0;
        }

        return state.SystemNpcSimulation.PopulationEntries.Count;
    }

    private static int GetSavedLegacyNpcCount(GameRuntimeState state)
    {
        if (state == null ||
            state.SystemNpcSimulation == null ||
            state.SystemNpcSimulation.Npcs == null)
        {
            return 0;
        }

        return state.SystemNpcSimulation.Npcs.Count;
    }

    private void DictionaryToList(GameRuntimeState state)
    {
        if (state.MissionBlock == null)
            return;

        state.MissionBlock.OffersByPlanet_List = new();

        foreach (var pair in state.MissionBlock.OffersByPlanet)
        {
            pair.Value.PlanetId = pair.Key;
            state.MissionBlock.OffersByPlanet_List.Add(pair.Value);
        }
    }

    private void DictionaryFromList(GameRuntimeState state)
    {
        if (state.MissionBlock == null)
            return;

        state.MissionBlock.OffersByPlanet = new();

        if (state.MissionBlock.OffersByPlanet_List == null)
            return;

        foreach (PlanetOfferedMissionData item in state.MissionBlock.OffersByPlanet_List)
            state.MissionBlock.OffersByPlanet.Add(item.PlanetId, item);
    }

    private void WriteSaveAtomically(string json)
    {
        string savePath = GetSavePath();
        string backupPath = GetBackupPath();
        string tempPath = GetTempPath();

        DeleteTempFileIfExists();

        WriteTextFile(tempPath, json);

        if (File.Exists(savePath))
        {
            File.Replace(tempPath, savePath, backupPath);
            LogCustom("Backup created: " + backupPath);
            return;
        }

        File.Move(tempPath, savePath);
    }

    private static void WriteTextFileAtomically(
        string path,
        string text)
    {
        string tempPath =
            path + ".temp";

        if (File.Exists(tempPath))
            File.Delete(tempPath);

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        WriteTextFile(tempPath, text);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(tempPath, path);
    }

    private static void WriteTextFile(
        string path,
        string text)
    {
        using (var stream = new FileStream(
                   path,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None))
        using (var writer = new StreamWriter(
                   stream,
                   new UTF8Encoding(false)))
        {
            writer.Write(text);
            writer.Flush();
            stream.Flush(true);
        }
    }

    private void DeleteTempFileIfExists()
    {
        string tempPath = GetTempPath();

        if (!File.Exists(tempPath))
            return;

        try
        {
            File.Delete(tempPath);
        }
        catch (Exception exception)
        {
            AppLog.Warning(
                "[SaveService] Failed to delete temp save: " +
                exception.Message);
        }
    }

    private void DeleteFileIfExists(string path)
    {
        if (!File.Exists(path))
            return;

        File.Delete(path);
        AppLog.Info("[SaveService] Deleted file: " + path);
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !Directory.Exists(path))
        {
            return;
        }

        Directory.Delete(path, true);
    }

    private static void RecreateDirectory(string path)
    {
        DeleteDirectoryIfExists(path);
        Directory.CreateDirectory(path);
    }

    private static void CopyDirectory(
        string sourceDirectory,
        string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (string directory in Directory.GetDirectories(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(
                directory.Replace(sourceDirectory, targetDirectory));
        }

        foreach (string file in Directory.GetFiles(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            string targetFile =
                file.Replace(sourceDirectory, targetDirectory);

            File.Copy(file, targetFile, true);
        }
    }

    private bool HasMultiFileSave(string directoryPath)
    {
        return File.Exists(
            Path.Combine(directoryPath, _saveConfig.ManifestFileName));
    }

    public string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, _saveFileName);
    }

    public string GetBackupPath()
    {
        return Path.Combine(Application.persistentDataPath, _backupFileName);
    }

    public string GetTempPath()
    {
        return GetSavePath() + ".temp";
    }

    private string GetMultiFileRootPath()
    {
        return Path.Combine(
            Application.persistentDataPath,
            _saveConfig.MultiFileSaveDirectoryName);
    }

    private string GetActiveMultiFileDirectoryPath()
    {
        return Path.Combine(
            GetMultiFileRootPath(),
            _saveConfig.ActiveDirectoryName);
    }

    private string GetBackupMultiFileDirectoryPath()
    {
        return Path.Combine(
            GetMultiFileRootPath(),
            _saveConfig.BackupDirectoryName);
    }

    private string GetStagingMultiFileDirectoryPath()
    {
        return Path.Combine(
            GetMultiFileRootPath(),
            _saveConfig.StagingDirectoryName);
    }

    private int GetCurrentTick()
    {
        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null &&
            Bootstrapper.Instance.ServiceRegistry.TryGet<IGameTimeService>(
                out IGameTimeService gameTimeService) &&
            gameTimeService != null)
        {
            return gameTimeService.CurrentQuantTick;
        }

        return -1;
    }

    private static string ComputeSha256(string text)
    {
        byte[] payload =
            Encoding.UTF8.GetBytes(text ?? string.Empty);

        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hash =
                sha256.ComputeHash(payload);

            var builder =
                new StringBuilder(hash.Length * 2);

            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2"));

            return builder.ToString();
        }
    }

    private static long BeginPerfMeasure()
    {
        return System.Diagnostics.Stopwatch.GetTimestamp();
    }

    private static double EndPerfMeasureMs(long startedAt)
    {
        long elapsedTicks =
            System.Diagnostics.Stopwatch.GetTimestamp() - startedAt;

        return elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }

    private static double BytesToMegabytes(long bytes)
    {
        return bytes / (1024.0 * 1024.0);
    }

    private void LogSavePerf(double elapsedMs, string message)
    {
        if (Bootstrapper.Instance == null ||
            !Bootstrapper.Instance.IsPerformanceLogEnabled(
                DebugLogPerformanceArea.Save))
        {
            return;
        }

        Bootstrapper.Instance.LogPerformance(
            DebugLogPerformanceArea.Save,
            message +
            " | Ms=" +
            elapsedMs.ToString("F2"));
    }

    private void ProcessMultiFileValidateManifestPartStep()
    {
        if (_stagedManifest == null ||
            _stagedManifest.Parts == null ||
            _manifestValidationPartIndex >= _stagedManifest.Parts.Count)
        {
            _stagedSavePhase = StagedSavePhase.BackupDeletePrepare;
            return;
        }

        SaveMultiFilePartInfo part =
            _stagedManifest.Parts[_manifestValidationPartIndex];

        long startedAt = BeginPerfMeasure();

        ValidateMultiFilePartOrThrow(
            _stagedDirectoryPath,
            part);

        _manifestValidationPartIndex++;

        double elapsedMs =
            EndPerfMeasureMs(startedAt);

        _stagedValidateManifestMs += elapsedMs;

        LogSavePerf(
            elapsedMs,
            "[SaveService] MULTI_SAVE_STEP" +
            " | Step=ValidateManifestPart" +
            " | Reason=" + _stagedSaveReason +
            " | SaveId=" + _stagedSaveId +
            " | UnityFrame=" + Time.frameCount +
            " | Tick=" + GetCurrentTick() +
            " | FileIndex=" + _manifestValidationPartIndex +
            " | Files=" + _stagedManifest.Parts.Count +
            " | File=" + part.FileName);

        if (_manifestValidationPartIndex >= _stagedManifest.Parts.Count)
            _stagedSavePhase = StagedSavePhase.BackupDeletePrepare;
    }

    private void ValidateMultiFilePartOrThrow(
    string directoryPath,
    SaveMultiFilePartInfo part)
    {
        if (part == null ||
            string.IsNullOrWhiteSpace(part.FileName))
        {
            throw new InvalidDataException("Manifest contains invalid part.");
        }

        string path =
            Path.Combine(directoryPath, part.FileName);

        if (!File.Exists(path))
            throw new FileNotFoundException("Save part missing: " + part.FileName);

        string json =
            File.ReadAllText(path);

        string sha256 =
            ComputeSha256(json);

        if (part.JsonChars != json.Length)
        {
            throw new InvalidDataException(
                "Save part length mismatch: " + part.FileName);
        }

        if (!string.Equals(part.Sha256, sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Save part checksum mismatch: " + part.FileName);
        }
    }
}