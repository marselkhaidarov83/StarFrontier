using UnityEngine;

public class SaveDebugView : MonoBehaviour
{
    private ISaveService _saveService;
    private IGameSessionService _sessionService;

    private void Start()
    {
        if (Bootstrapper.Instance == null)
        {
            Debug.LogError("[SaveDebugView] Bootstrapper.Instance is null");
            return;
        }

        var registry = Bootstrapper.Instance.ServiceRegistry;

        _saveService = registry.Get<ISaveService>();
        _sessionService = registry.Get<IGameSessionService>();

        PrintPaths();
    }

    [ContextMenu("Print Save Paths")]
    public void PrintPaths()
    {
        if (_saveService == null)
        {
            Debug.LogError("[SaveDebugView] SaveService is null");
            return;
        }

        Debug.Log($"[SaveDebugView] Save path: {((SaveService2) _saveService).GetSavePath()}");
        Debug.Log($"[SaveDebugView] Backup path: {((SaveService2) _saveService).GetBackupPath()}");
    }

    [ContextMenu("Save Game")]
    public void SaveGame()
    {
        if (_saveService == null || _sessionService == null)
        {
            Debug.LogError("[SaveDebugView] Services not ready");
            return;
        }

        ((SaveService2) _saveService).Save();
    }

    [ContextMenu("Load Game")]
    public void LoadGame()
    {
        if (_saveService == null || _sessionService == null)
        {
            Debug.LogError("[SaveDebugView] Services not ready");
            return;
        }

        var state = _saveService.Load();
        _sessionService.LoadSession(state);

        Debug.Log("[SaveDebugView] Loaded GameState into session");
    }

    [ContextMenu("Delete Save")]
    public void DeleteSave()
    {
        if (_saveService == null)
        {
            Debug.LogError("[SaveDebugView] SaveService is null");
            return;
        }

        _saveService.DeleteSave();

        Debug.Log("[SaveDebugView] Save deleted");
    }

    [ContextMenu("Add 100 Credits")]
    public void Add100Credits()
    {
        if (_sessionService?.State?.Player == null)
        {
            Debug.LogError("[SaveDebugView] PlayerState is null");
            return;
        }

        _sessionService.State.Player.Credits += 100;

        Debug.Log($"[SaveDebugView] Credits: {_sessionService.State.Player.Credits}");
    }
}