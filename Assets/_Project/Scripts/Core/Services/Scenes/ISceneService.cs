public interface ISceneService
{
    void LoadScene(string sceneName);

    void LoadBootstrap();
    void LoadMainMenu();
    void LoadMeta();
    void LoadMeta2A();
    void LoadCombat();
}