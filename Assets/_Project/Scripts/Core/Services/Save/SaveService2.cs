using System;
using System.IO;
using UnityEngine;

public class SaveService2 : ISaveService
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

    public SaveService2()
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
            Debug.LogWarning("[SaveService] Save skipped: GameState is null.");
            return;
        }

        try
        {
            _enabledSave = false;

            PrepareStateBeforeSave(state);

            if (File.Exists(GetSavePath()))
            {
                File.Copy(GetSavePath(), GetBackupPath(), true);
                Debug.Log("[SaveService] Backup created: " + GetBackupPath());
            }

            string json = JsonUtility.ToJson(state, true);
            File.WriteAllText(GetSavePath(), json);

            _eventBus?.Publish(new GameSavedEvent());

            _autosaveTimer = 0f;

            Debug.Log("[SaveService] Game saved to: " + GetSavePath());
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveService] Failed to save: " + e.Message);
        }
        finally
        {
            _enabledSave = true;
        }
    }

    public GameRuntimeState Load()
    {
        GameRuntimeState mainSave = TryLoadFromPath(GetSavePath());

        if (mainSave != null)
        {
            Debug.Log("[SaveService] Main save loaded.");
            return mainSave;
        }

        Debug.LogWarning("[SaveService] Main save failed. Trying backup.");

        GameRuntimeState backupSave = TryLoadFromPath(GetBackupPath());

        if (backupSave != null)
        {
            Debug.LogWarning("[SaveService] Backup save loaded.");
            return backupSave;
        }

        Debug.LogWarning("[SaveService] No valid save found.");
        return null;
    }

    public void DeleteSave()
    {
        DeleteFileIfExists(GetSavePath());
        DeleteFileIfExists(GetBackupPath());
    }

    public void Tick(float deltaTime)
    {
        if (!_enabledSave)
            return;

        _autosaveTimer += deltaTime;

        if (_autosaveTimer >= _autosaveIntervalSeconds)
        {
            Save();
            Debug.Log("[SaveService] Periodic autosave completed.");
        }
    }

    private void PrepareStateBeforeSave(GameRuntimeState state)
    {
        state.Meta.SaveVersion++;
        state.Meta.LastSaveUtc = DateTime.UtcNow.Ticks;

        if (_systemNpcSimulationSaveService != null)
            state.SystemNpcSimulation = _systemNpcSimulationSaveService.Capture();

        if (_systemEncounterSaveService != null)
            state.SystemEncounter = _systemEncounterSaveService.Capture();

        DictionaryToList(state);
    }

    private GameRuntimeState TryLoadFromPath(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            string json = File.ReadAllText(path);
            GameRuntimeState state = JsonUtility.FromJson<GameRuntimeState>(json);

            if (state == null)
            {
                Debug.LogError("[SaveService] Parsed GameState is null: " + path);
                return null;
            }

            DictionaryFromList(state);

            if (_systemEncounterSaveService != null && state.SystemEncounter != null)
                _systemEncounterSaveService.Restore(state.SystemEncounter);

            if (_systemNpcSimulationSaveService != null && state.SystemNpcSimulation != null)
                _systemNpcSimulationSaveService.Restore(state.SystemNpcSimulation);

            return state;
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveService] Failed to load from " + path + ": " + e.Message);
            return null;
        }
    }

    private void DeleteFileIfExists(string path)
    {
        if (!File.Exists(path))
            return;

        File.Delete(path);
        Debug.Log("[SaveService] Deleted file: " + path);
    }

    public string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, _saveFileName);
    }

    public string GetBackupPath()
    {
        return Path.Combine(Application.persistentDataPath, _backupFileName);
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