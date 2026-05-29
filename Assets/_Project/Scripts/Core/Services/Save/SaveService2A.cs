using System;
using System.IO;
using UnityEngine;

public sealed class SaveService2A : ISaveService
{
    private float _autosaveIntervalSeconds = 60f;
    private float autosaveTimer = 0f;
    private readonly SaveConfig _config;

    private IGameSessionService _gameSessionService;

    private bool enabledSave = true;

    public SaveService2A()
    {
        _gameSessionService = Bootstrapper2A.Instance.ServiceRegistry.Get<IGameSessionService>();

        IConfigService configService = Bootstrapper2A.Instance.ServiceRegistry.Get<IConfigService>();
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
                return TryLoadBackup();
            }

            Debug.Log($"SaveServiceU: save loaded from {path}");
            return save;
        }
        catch (Exception exception)
        {
            Debug.LogError($"SaveServiceU: failed to load save. Error: {exception.Message}");
            return TryLoadBackup();
        }
    }

    public void Save()
    {
        Save(_gameSessionService?.CurrentSave);
    }

    public void Save(SaveData save)
    {
        if (save == null)
        {
            Debug.LogError("SaveServiceU: cannot save null SaveData.");
            return;
        }

        var path = GetSavePath();

        try
        {
            CreateBackupIfNeeded(path);

            var json = JsonUtility.ToJson(save, true);
            File.WriteAllText(path, json);

            Debug.Log($"SaveServiceU: save written to {path}");
        }
        catch (Exception exception)
        {
            Debug.LogError($"SaveServiceU: failed to save. Error: {exception.Message}");
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