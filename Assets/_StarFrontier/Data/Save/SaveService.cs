using System;
using System.IO;
using UnityEngine;

public sealed class SaveServiceU
{
    private readonly SaveConfig _config;
    private readonly SaveValidator _validator;

    public SaveServiceU(SaveConfig config, SaveValidator validator)
    {
        _config = config;
        _validator = validator;
    }

    public GameState Load()
    {
        var path = GetSavePath();

        if (!File.Exists(path))
        {
            Debug.Log("SaveService: save file not found. Creating new GameState.");
            return _validator.CreateNewState();
        }

        try
        {
            var json = File.ReadAllText(path);
            var state = JsonUtility.FromJson<GameState>(json);

            Debug.Log($"SaveService: save loaded from {path}");

            return _validator.ValidateOrCreate(state);
        }
        catch (Exception exception)
        {
            Debug.LogError($"SaveService: failed to load save. Error: {exception.Message}");

            var backupState = TryLoadBackup();

            if (backupState != null)
            {
                Debug.LogWarning("SaveService: backup save loaded.");
                return backupState;
            }

            Debug.LogWarning("SaveService: backup not found or broken. Creating new GameState.");
            return _validator.CreateNewState();
        }
    }

    public void Save(GameState state)
    {
        state = _validator.ValidateOrCreate(state);
        state.meta.lastSavedAtUtc = DateTime.UtcNow.ToString("O");

        var path = GetSavePath();

        try
        {
            CreateBackupIfNeeded(path);

            var json = JsonUtility.ToJson(state, true);
            File.WriteAllText(path, json);

            Debug.Log($"SaveService: save written to {path}");
        }
        catch (Exception exception)
        {
            Debug.LogError($"SaveService: failed to save. Error: {exception.Message}");
        }
    }

    public void DeleteSave()
    {
        DeleteFileIfExists(GetSavePath());
        DeleteFileIfExists(GetBackupPath());

        Debug.Log("SaveService: save and backup deleted.");
    }

    public bool HasSave()
    {
        return File.Exists(GetSavePath());
    }

    public string GetDebugSavePath()
    {
        return GetSavePath();
    }

    public string GetDebugBackupPath()
    {
        return GetBackupPath();
    }

    private void CreateBackupIfNeeded(string savePath)
    {
        if (!_config.useBackupSave)
        {
            return;
        }

        if (!File.Exists(savePath))
        {
            return;
        }

        File.Copy(savePath, GetBackupPath(), true);
    }

    private GameState TryLoadBackup()
    {
        var backupPath = GetBackupPath();

        if (!File.Exists(backupPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(backupPath);
            var state = JsonUtility.FromJson<GameState>(json);

            return _validator.ValidateOrCreate(state);
        }
        catch
        {
            return null;
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
}