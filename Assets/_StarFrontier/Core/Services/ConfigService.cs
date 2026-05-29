using UnityEngine;

public sealed class ConfigService
{
    public GameBootstrapConfig BootstrapConfig { get; private set; }
    public SaveConfig SaveConfig { get; private set; }
    public DebugConfig DebugConfig { get; private set; }

    public bool IsLoaded { get; private set; }

    public void Load()
    {
        BootstrapConfig = Resources.Load<GameBootstrapConfig>("Configs/GameBootstrapConfig");
        SaveConfig = Resources.Load<SaveConfig>("Configs/SaveConfig");
        DebugConfig = Resources.Load<DebugConfig>("Configs/DebugConfig");

        Validate();

        IsLoaded = BootstrapConfig != null &&
                   SaveConfig != null &&
                   DebugConfig != null;
    }

    private void Validate()
    {
        if (BootstrapConfig == null)
        {
            Debug.LogError("ConfigService: GameBootstrapConfig not found at Resources/Configs/GameBootstrapConfig.");
        }

        if (SaveConfig == null)
        {
            Debug.LogError("ConfigService: SaveConfig not found at Resources/Configs/SaveConfig.");
        }

        if (DebugConfig == null)
        {
            Debug.LogError("ConfigService: DebugConfig not found at Resources/Configs/DebugConfig.");
        }
    }
}