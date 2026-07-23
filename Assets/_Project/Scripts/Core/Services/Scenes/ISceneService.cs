using System;
using UnityEngine;

public interface ISceneService
{
    event Action<string> SceneLoadCompleted;
    event Action<string, string> SceneLoadFailed;

    void LoadScene(string sceneName);
    AsyncOperation LoadSceneAsync(string sceneName);
    bool TryLoadFallback(string fallbackSceneName);

    void LoadBootstrap();
    void LoadMainMenu();
    void LoadMeta();
    void LoadGalaxy();
    void LoadSystem();
    void LoadCombat();
}