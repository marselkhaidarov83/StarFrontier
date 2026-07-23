using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneService : ISceneService
{
    private const string BOOTSTRAP_SCENE = "BootstrapScene";
    private const string MAIN_MENU_SCENE = "MainMenuScene";
    private const string META_SCENE = "MetaScene";
    private const string GALAXY_SCENE = "GalaxyScene";
    private const string SYSTEM_SCENE = "SystemScene";
    private const string COMBAT_SCENE = "CombatScene";

    private bool _debugEnabled;

    public event Action<string> SceneLoadCompleted;
    public event Action<string, string> SceneLoadFailed;

    public void LoadScene(string sceneName)
    {
        if (!CanLoadScene(sceneName, out string error))
        {
            NotifyFailure(sceneName, error);
            return;
        }

        try
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            NotifyCompleted(sceneName);
        }
        catch (Exception exception)
        {
            NotifyFailure(sceneName, exception.Message);
            AppLog.Exception(exception);
        }
    }

    public AsyncOperation LoadSceneAsync(string sceneName)
    {
        if (!CanLoadScene(sceneName, out string error))
        {
            NotifyFailure(sceneName, error);
            return null;
        }

        try
        {
            AsyncOperation operation =
                SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            if (operation == null)
            {
                NotifyFailure(
                    sceneName,
                    "Unity did not create a scene loading operation.");

                return null;
            }

            operation.completed += _ => NotifyCompleted(sceneName);

            if (_debugEnabled)
                AppLog.Info($"Async scene load started: {sceneName}");

            return operation;
        }
        catch (Exception exception)
        {
            NotifyFailure(sceneName, exception.Message);
            AppLog.Exception(exception);
            return null;
        }
    }

    public bool TryLoadFallback(string fallbackSceneName)
    {
        if (_debugEnabled)
            AppLog.Warning($"Trying fallback scene: {fallbackSceneName}");

        return LoadSceneAsync(fallbackSceneName) != null;
    }

    public void LoadBootstrap()
    {
        if (_debugEnabled)
            AppLog.Info("LoadBootstrap started");

        LoadScene(BOOTSTRAP_SCENE);
    }

    public void LoadMainMenu()
    {
        if (_debugEnabled)
            AppLog.Info("LoadMainMenu started");

        LoadScene(MAIN_MENU_SCENE);
    }

    public void LoadMeta()
    {
        if (_debugEnabled)
            AppLog.Info("LoadMeta started");

        LoadScene(META_SCENE);
    }

    public void LoadGalaxy()
    {
        if (_debugEnabled)
            AppLog.Info("LoadGalaxy started");

        LoadScene(GALAXY_SCENE);
    }

    public void LoadSystem()
    {
        if (_debugEnabled)
            AppLog.Info("LoadSystem started");

        LoadScene(SYSTEM_SCENE);
    }

    public void LoadCombat()
    {
        if (_debugEnabled)
            AppLog.Info("LoadCombat started");

        LoadScene(COMBAT_SCENE);
    }

    private static bool CanLoadScene(string sceneName, out string error)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            error = "Scene name is empty.";
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            error =
                $"Scene is not available in enabled Build Settings: {sceneName}";

            return false;
        }

        error = string.Empty;
        return true;
    }

    private void NotifyCompleted(string sceneName)
    {
        if (_debugEnabled)
            AppLog.Info($"Scene load completed: {sceneName}");

        try
        {
            SceneLoadCompleted?.Invoke(sceneName);
        }
        catch (Exception exception)
        {
            AppLog.Exception(exception);
        }
    }

    private void NotifyFailure(string sceneName, string error)
    {
        string safeSceneName = sceneName ?? string.Empty;

        string safeError = string.IsNullOrWhiteSpace(error)
            ? "Unknown scene loading error."
            : error;

        AppLog.Error(
            $"Scene load failed: {safeSceneName}. {safeError}");

        try
        {
            SceneLoadFailed?.Invoke(safeSceneName, safeError);
        }
        catch (Exception exception)
        {
            AppLog.Exception(exception);
        }
    }
}