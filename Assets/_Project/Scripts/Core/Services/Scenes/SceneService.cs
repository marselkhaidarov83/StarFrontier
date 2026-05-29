using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneService : ISceneService
{
    private const string BOOTSTRAP_SCENE = "BootstrapScene";
    private const string MAIN_MENU_SCENE = "MainMenuScene";
    private const string META_SCENE = "MetaScene";
    private const string COMBAT_SCENE = "CombatScene";
    private bool _debugEnabled;

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    public void LoadBootstrap()
    {
        if (_debugEnabled)
            Debug.Log("LoadBootstrap started");
        LoadScene(BOOTSTRAP_SCENE);
    }

    public void LoadMainMenu()
    {
        if (_debugEnabled)
            Debug.Log("LoadMainMenu started");
        LoadScene(MAIN_MENU_SCENE);
    }

    public void LoadMeta()
    {
        if (_debugEnabled)
            Debug.Log("LoadMeta started");
        LoadScene(META_SCENE);
    }

    public void LoadCombat()
    {
        if (_debugEnabled)
            Debug.Log("LoadCombat started");
        LoadScene(COMBAT_SCENE);
    }
}