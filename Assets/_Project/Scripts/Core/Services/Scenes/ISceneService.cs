using System;
using UnityEngine;

public interface ISceneService
{
    event Action<string> SceneLoadCompleted;
    event Action<string, string> SceneLoadFailed;
    event Action<string> SceneLoadCancelled;

    bool HasActiveLoad { get; }

    void LoadScene(string sceneName);
    AsyncOperation LoadSceneAsync(string sceneName);
    AsyncOperation LoadLoadingAsync();
    bool CancelActiveLoad();
    bool TryLoadFallback(string fallbackSceneName);

    void LoadBootstrap();
    void LoadMainMenu();
    void LoadMeta();
    void LoadGalaxy();
    void LoadSystem();
    void LoadCombat();
}
