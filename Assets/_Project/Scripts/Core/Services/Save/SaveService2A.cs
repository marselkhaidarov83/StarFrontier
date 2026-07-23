using System;
using System.IO;
using System.Text;
using UnityEngine;

public class SaveService2A : CustomService, ISaveService
{
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

    public SaveService2A()
    {
        _configService = Bootstrapper.Instance.ServiceRegistry.Get<IConfigService>();
        _autosaveIntervalSeconds = _configService.SaveConfig.AutosaveIntervalSeconds;
        _saveFileName = _configService.SaveConfig.SaveFileName;
        _backupFileName = _configService.SaveConfig.BackupFileName;

        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _systemEncounterSaveService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEncounterSaveService>();
        _systemNpcSimulationSaveService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcSimulationSaveService>();

        _eventBus?.Subscribe<SaveNeedEvent>(OnSaveNeedEvent);
    }

    private void OnSaveNeedEvent(SaveNeedEvent evt)
    {
        Save();
    }

    public void EnableSave(bool enable)
    {
        _enabledSave = enable;
    }

    public bool HasSave()
    {
        return File.Exists(GetSavePath()) ||
                File.Exists(GetBackupPath());
    }

    public void Save()
    {
        Save(_gameSessionService?.State);
    }

    public void Save(GameRuntimeState state)
    {
        if (!_enabledSave)
            return;

        if (state == null)
        {
            AppLog.Warning("[SaveService] Save skipped: GameState is null.");
            return;
        }

        try
        {
            _enabledSave = false;

            PrepareStateBeforeSave(state);
            _integrityStage.Stamp(state);

            string json = JsonUtility.ToJson(state, true);
            WriteSaveAtomically(json);

            _eventBus?.Publish(new GameSavedEvent());

            _autosaveTimer = 0f;

            AppLog.Info("[SaveService] Game saved to: " + GetSavePath());
        }
        catch (Exception e)
        {
            AppLog.Error("[SaveService] Failed to save: " + e.Message);
        }
        finally
        {
            DeleteTempFileIfExists();
            _enabledSave = true;
        }
    }

    public GameRuntimeState Load()
    {
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
        DeleteFileIfExists(GetSavePath());
        DeleteFileIfExists(GetBackupPath());
        DeleteFileIfExists(GetTempPath());
    }

    public void Tick(float deltaTime)
    {
        if (!_enabledSave)
            return;

        _autosaveTimer += deltaTime;

        if (_autosaveTimer >= _autosaveIntervalSeconds)
        {
            Save();
            AppLog.Info("[SaveService] Periodic autosave completed.");
        }
    }

    private void PrepareStateBeforeSave(GameRuntimeState state)
    {
        _migrationStage.Run(state);

        TryWriteGameTimeToState(state);
        TryWriteShipMovementToState(state);

        _migrationStage.Run(state);

        state.Meta.SaveVersion++;
        state.Meta.LastSaveUtc = DateTime.UtcNow.Ticks;

        if (_systemNpcSimulationSaveService != null)
            state.SystemNpcSimulation =
                _systemNpcSimulationSaveService.Capture();

        if (_systemEncounterSaveService != null)
            state.SystemEncounter =
                _systemEncounterSaveService.Capture();

        DictionaryToList(state);

        SaveValidationResult validation =
            _validationStage.ValidateAndNormalize(state);

        if (!validation.IsValid)
        {
            throw new InvalidDataException(
                "[SaveService] Save validation failed: " +
                validation.BuildErrorMessage());
        }
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

    private void TryWriteShipMovementToState(
        GameRuntimeState state)
    {
        if (state == null)
            return;

        if (Bootstrapper.Instance == null
            || Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        if (Bootstrapper.Instance.ServiceRegistry.TryGet<
                IPlayerShipSaveSyncService>(
                out IPlayerShipSaveSyncService syncService))
        {
            syncService.WriteMovementToSave(state);
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
                AppLog.Error(
                    "[SaveService] Parsed GameState is null: " + path);

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

            if (_systemEncounterSaveService != null && state.SystemEncounter != null)
                _systemEncounterSaveService.Restore(state.SystemEncounter);

            if (_systemNpcSimulationSaveService != null && state.SystemNpcSimulation != null)
                _systemNpcSimulationSaveService.Restore(state.SystemNpcSimulation);

            return state;
        }
        catch (Exception e)
        {
            AppLog.Error("[SaveService] Failed to load from " + path + ": " + e.Message);
            return null;
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

    private void TryInitializeShipMovementFromState(
    GameRuntimeState state)
    {
        if (state == null)
            return;

        if (Bootstrapper.Instance == null
            || Bootstrapper.Instance.ServiceRegistry == null)
        {
            return;
        }

        if (Bootstrapper.Instance.ServiceRegistry.TryGet<
                IPlayerShipSaveSyncService>(
                out IPlayerShipSaveSyncService syncService))
        {
            syncService.InitializeMovementFromSave(state);
        }
    }

    private void DeleteFileIfExists(string path)
    {
        if (!File.Exists(path))
            return;

        File.Delete(path);
        AppLog.Info("[SaveService] Deleted file: " + path);
    }

    private void WriteSaveAtomically(string json)
    {
        string savePath = GetSavePath();
        string backupPath = GetBackupPath();
        string tempPath = GetTempPath();

        DeleteTempFileIfExists();

        using (var stream = new FileStream(
            tempPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        using (var writer = new StreamWriter(
            stream,
            new UTF8Encoding(false)))
        {
            writer.Write(json);
            writer.Flush();
            stream.Flush(true);
        }

        if (File.Exists(savePath))
        {
            File.Replace(tempPath, savePath, backupPath);
            AppLog.Info("[SaveService] Backup created: " + backupPath);
            return;
        }

        File.Move(tempPath, savePath);
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
        {
            state.MissionBlock.OffersByPlanet.Add(item.PlanetId, item);
        }
    }
}
