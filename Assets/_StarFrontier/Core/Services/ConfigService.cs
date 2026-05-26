using UnityEngine;

public sealed class ConfigService
{
    public GameBootstrapConfig BootstrapConfig { get; private set; }
    public SaveConfig SaveConfig { get; private set; }
    public DebugConfig DebugConfig { get; private set; }

    public void Load()
    {
        BootstrapConfig = Resources.Load<GameBootstrapConfig>("Configs/GameBootstrapConfig");
        SaveConfig = Resources.Load<SaveConfig>("Configs/SaveConfig");
        DebugConfig = Resources.Load<DebugConfig>("Configs/DebugConfig");

        if (BootstrapConfig == null)
        {
            Debug.LogError("ConfigService: GameBootstrapConfig not found.");
        }

        if (SaveConfig == null)
        {
            Debug.LogError("ConfigService: SaveConfig not found.");
        }

        if (DebugConfig == null)
        {
            Debug.LogWarning("ConfigService: DebugConfig not found.");
        }
    }
}