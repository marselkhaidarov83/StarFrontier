public interface ISceneService
{
    void LoadScene(string sceneName);

    void LoadBootstrap();
    void LoadMainMenu();
    void LoadMeta();
    void LoadGalaxy();
    void LoadSystem();
    void LoadCombat();
}