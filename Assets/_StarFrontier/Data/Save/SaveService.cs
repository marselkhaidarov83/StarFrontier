using System;
using System.IO;
using UnityEngine;

public sealed class SaveService
{
    private readonly SaveConfig _config;
    private readonly SaveValidator _validator;

    public SaveService(SaveConfig config, SaveValidator validator)
    {
        _config = config;
        _validator = validator;
    }

    public GameState Load()
    {
        var path = GetSavePath();

        if (!File.Exists(path))
        {
            Debug.Log("SaveService: save not found. Creating new state.");
            return _validator.CreateNewState();
        }

        try
        {
            var json = File.ReadAllText(path);
            var state = JsonUtility.FromJson<GameState>(json);
            return _validator.ValidateOrCreate(state);
        }
        catch (Exception exception)
        {
            Debug.LogError($"SaveService: load failed: {exception}");
            return TryLoadBackup() ?? _validator.CreateNewState();
        }
    }

    public void Save(GameState state)
    {
        state = _validator.ValidateOrCreate(state);
        state.meta.lastSavedAtUtc = DateTime.UtcNow.ToString("O");

        var path = GetSavePath();

        try
        {
            if (_config.useBackupSave && File.Exists(path))
            {
                File.Copy(path, GetBackupPath(), true);
            }

            var json = JsonUtility.ToJson(state, true);
            File.WriteAllText(path, json);

            Debug.Log($"SaveService: saved to {path}");
        }
        catch (Exception exception)
        {
            Debug.LogError($"SaveService: save failed: {exception}");
        }
    }

    public void DeleteSave()
    {
        var path = GetSavePath();

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private GameState TryLoadBackup()
    {
        var path = GetBackupPath();

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var state = JsonUtility.FromJson<GameState>(json);
            return _validator.ValidateOrCreate(state);
        }
        catch
        {
            return null;
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