using UnityEngine;

public sealed class SaveSystemTester : MonoBehaviour
{
    [SerializeField] private SaveConfig saveConfig;

    private SaveServiceU _saveService;

    private void Start()
    {
        if (saveConfig == null)
        {
            Debug.LogError("SaveSystemTester: SaveConfig is not assigned.");
            return;
        }

        var validator = new SaveValidator(
            defaultSaveVersion: saveConfig.saveVersion,
            defaultPlayerName: "Captain",
            defaultSystemId: "system_start_01"
        );

        _saveService = new SaveServiceU(saveConfig, validator);

        RunTest();
    }

    private void RunTest()
    {
        Debug.Log("SaveSystemTester: starting save/load test.");
        Debug.Log($"Save path: {_saveService.GetDebugSavePath()}");
        Debug.Log($"Backup path: {_saveService.GetDebugBackupPath()}");

        _saveService.DeleteSave();

        GameState state = _saveService.Load();

        state.meta.totalTicks = 123;
        state.player.playerName = "Captain";
        state.player.currentSystemId = "system_start_01";
        state.currentSystem.systemId = "system_start_01";
        state.currentSystem.isLoaded = true;

        _saveService.Save(state);

        GameState loadedState = _saveService.Load();

        Debug.Log("SaveSystemTester: loaded state:");
        Debug.Log($"Player Name: {loadedState.player.playerName}");
        Debug.Log($"Current System: {loadedState.player.currentSystemId}");
        Debug.Log($"System Loaded: {loadedState.currentSystem.isLoaded}");
        Debug.Log($"Save Version: {loadedState.meta.saveVersion}");
        Debug.Log($"Total Ticks: {loadedState.meta.totalTicks}");

        if (loadedState.meta.totalTicks == 123)
        {
            Debug.Log("SaveSystemTester: SUCCESS. Save/load works.");
        }
        else
        {
            Debug.LogError("SaveSystemTester: FAILED. Loaded ticks are wrong.");
        }
    }
}