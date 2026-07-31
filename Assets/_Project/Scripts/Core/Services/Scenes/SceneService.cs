using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneService : ISceneService
{
    private const string BOOTSTRAP_SCENE = "BootstrapScene";
    private const string LOADING_SCENE = "LoadingScene";
    private const string MAIN_MENU_SCENE = "MainMenuScene";
    private const string META_SCENE = "MetaScene";
    private const string GALAXY_SCENE = "GalaxyScene";
    private const string SYSTEM_SCENE = "SystemScene";
    private const string COMBAT_SCENE = "CombatScene";

    private bool _debugEnabled;
    private AsyncOperation _activeLoadOperation;
    private Action<AsyncOperation> _activeLoadCompleted;
    private string _activeSceneName = string.Empty;

    public event Action<string> SceneLoadCompleted;
    public event Action<string, string> SceneLoadFailed;
    public event Action<string> SceneLoadCancelled;

    public bool HasActiveLoad =>
        _activeLoadOperation != null &&
        !_activeLoadOperation.isDone;

    public void LoadScene(string sceneName)
    {
        if (!CanLoadScene(sceneName, out string error))
        {
            NotifyFailure(sceneName, error);
            return;
        }

        try
        {
            CancelActiveLoad();
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
            CancelActiveLoad();

            AsyncOperation operation =
                SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            if (operation == null)
            {
                NotifyFailure(
                    sceneName,
                    "Unity did not create a scene loading operation.");

                return null;
            }

            _activeLoadOperation = operation;
            _activeSceneName = sceneName;
            _activeLoadCompleted = completedOperation =>
            {
                if (!ReferenceEquals(
                        _activeLoadOperation,
                        completedOperation))
                {
                    return;
                }

                string completedSceneName = _activeSceneName;
                ClearActiveLoad();
                NotifyCompleted(completedSceneName);
            };

            operation.completed += _activeLoadCompleted;

            if (_debugEnabled)
                AppLog.Info($"Async scene load started: {sceneName}");

            return operation;
        }
        catch (Exception exception)
        {
            ClearActiveLoad();
            NotifyFailure(sceneName, exception.Message);
            AppLog.Exception(exception);
            return null;
        }
    }

    public AsyncOperation LoadLoadingAsync()
    {
        return LoadSceneAsync(LOADING_SCENE);
    }

    public bool CancelActiveLoad()
    {
        if (_activeLoadOperation == null)
            return false;

        string cancelledSceneName = _activeSceneName;
        ClearActiveLoad();

        AppLog.Warning(
            $"Scene load callbacks cancelled: {cancelledSceneName}. " +
            "Unity may still finish the underlying AsyncOperation.");

        NotifyCancelled(cancelledSceneName);
        return true;
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

        LoadSceneAsync(BOOTSTRAP_SCENE);
    }

    public void LoadMainMenu()
    {
        if (_debugEnabled)
            AppLog.Info("LoadMainMenu started");

        LoadSceneAsync(MAIN_MENU_SCENE);
    }

    public void LoadMeta()
    {
        if (_debugEnabled)
            AppLog.Info("LoadMeta started");

        LoadSceneAsync(META_SCENE);
    }

    public void LoadGalaxy()
    {
        if (_debugEnabled)
            AppLog.Info("LoadGalaxy started");

        LoadSceneAsync(GALAXY_SCENE);
    }

    public void LoadSystem()
    {
        if (_debugEnabled)
            AppLog.Info("LoadSystem started");

        LoadSceneAsync(SYSTEM_SCENE);
    }

    public void LoadCombat()
    {
        if (_debugEnabled)
            AppLog.Info("LoadCombat started");

        LoadSceneAsync(COMBAT_SCENE);
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

    private void ClearActiveLoad()
    {
        if (_activeLoadOperation != null &&
            _activeLoadCompleted != null)
        {
            _activeLoadOperation.completed -=
                _activeLoadCompleted;
        }

        _activeLoadOperation = null;
        _activeLoadCompleted = null;
        _activeSceneName = string.Empty;
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

    private void NotifyCancelled(string sceneName)
    {
        try
        {
            SceneLoadCancelled?.Invoke(sceneName ?? string.Empty);
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
