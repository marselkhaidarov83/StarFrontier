using UnityEngine;

public sealed class ConfigServiceTester : MonoBehaviour
{
    private void Start()
    {
        var configService = new ConfigService();
        configService.Load();

        if (!configService.IsLoaded)
        {
            Debug.LogError("ConfigServiceTester: ConfigService failed to load configs.");
            return;
        }

        Debug.Log("ConfigServiceTester: ConfigService loaded successfully.");
        Debug.Log($"Bootstrap scene: {configService.BootstrapConfig.bootSceneName}");
        Debug.Log($"Start scene: {configService.BootstrapConfig.startGameSceneName}");
        Debug.Log($"Default player: {configService.BootstrapConfig.defaultPlayerName}");
        Debug.Log($"Default system: {configService.BootstrapConfig.defaultSystemId}");
        Debug.Log($"Save file: {configService.SaveConfig.saveFileName}");
        Debug.Log($"Debug panel enabled: {configService.DebugConfig.enableDebugPanel}");
    }
}