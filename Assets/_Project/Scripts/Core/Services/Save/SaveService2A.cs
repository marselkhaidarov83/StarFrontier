using System;
using System.IO;
using UnityEngine;

public sealed class SaveService2A : CustomService, ISaveService
{
    private float _autosaveIntervalSeconds = 60f;
    private float autosaveTimer = 0f;
    private readonly SaveConfig _config;
    private readonly GalaxyConfig _galaxyConfig;

    private IGameSessionService _gameSessionService;

    private bool enabledSave = true;

    public SaveService2A()
    {
        _gameSessionService = Bootstrapper2A.Instance.ServiceRegistry.Get<IGameSessionService>();

        IConfigService configService = Bootstrapper2A.Instance.ServiceRegistry.Get<IConfigService>();
        _galaxyConfig = configService.GalaxyConfig;
        _config = configService.SaveConfig;
    }

    public void EnableSave(bool enable)
    {
        enabledSave = enable;
    }

    public SaveData Load()
    {
        var path = GetSavePath();

        if (!File.Exists(path))
        {
            Debug.LogWarning("SaveServiceU: save file not found.");
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var save = JsonUtility.FromJson<SaveData>(json);

            if (save == null)
            {
                Debug.LogError("SaveServiceU: loaded save is null.");
                LogCustom($"save loaded from backup");
                save = TryLoadBackup();
            }
            else
                LogCustom($"save loaded from {path}");

            EnsureGalaxyState(save.GameState);
            return save;
        }
        catch (Exception exception)
        {
            Debug.LogError($"failed to load save. Error: {exception.Message}");
            SaveData save = TryLoadBackup();
            EnsureGalaxyState(save.GameState);
            return save;
        }
    }

    public void EnsureGalaxyState(GameState gameState)
    {
        if (gameState == null)
        {
            Debug.LogError("SaveService: GameState is null. Cannot ensure GalaxyState.");
            return;
        }

        if (gameState.Galaxy != null)
            return;

        var galaxySimulationService = new GalaxySimulationService();
        gameState.Galaxy = galaxySimulationService.CreateGalaxyState(_galaxyConfig);

        LogCustom("GalaxyState was missing and has been created from GalaxyConfig.");
    }

    public void Save()
    {
        Save(_gameSessionService?.CurrentSave);
    }

    public void Save(SaveData save)
    {
        if (save == null)
        {
            LogCustomError("SaveServiceU: cannot save null SaveData.");
            return;
        }

        var path = GetSavePath();

        try
        {
            CreateBackupIfNeeded(path);

            var json = JsonUtility.ToJson(save, true);
            File.WriteAllText(path, json);

            LogCustom($"save written to {path}");
        }
        catch (Exception exception)
        {
            LogCustomError($"failed to save. Error: {exception.Message}");
        }
    }

    public void DeleteSave()
    {
        DeleteFileIfExists(GetSavePath());
        DeleteFileIfExists(GetBackupPath());
        Debug.Log("SaveServiceU: save and backup deleted.");
    }

    public bool HasSave()
    {
        return File.Exists(GetSavePath());
    }

    private SaveData TryLoadBackup()
    {
        var path = GetBackupPath();

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<SaveData>(json);
        }
        catch
        {
            return null;
        }
    }

    private void CreateBackupIfNeeded(string savePath)
    {
        if (_config == null || !_config.useBackupSave)
        {
            return;
        }

        if (File.Exists(savePath))
        {
            File.Copy(savePath, GetBackupPath(), true);
        }
    }

    private void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, _config.saveFileName);
    }

    private string GetBackupPath()
    {
        return Path.Combine(Application.persistentDataPath, _config.backupFileName);
    }

    public void Tick(float deltaTime)
    {
        // if (!hasUnsavedChanges)
        //     return;

        autosaveTimer += deltaTime;

        if (autosaveTimer >= _autosaveIntervalSeconds && enabledSave)
        {
            Save();
            Debug.Log("SaveService: Periodic autosave completed.");
        }
    }
}