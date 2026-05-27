using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DebugPanelView : MonoBehaviour
{
    [SerializeField] private Button saveButton;
    [SerializeField] private Button resetSaveButton;
    [SerializeField] private Button manualTickButton;
    [SerializeField] private TMP_Text systemText;

    private GameStateService _gameStateService;
    private SaveServiceU _saveService;

    private void Start()
    {
        var registry = GameBootstrapper.Services;

        _gameStateService = registry.Get<GameStateService>();
        _saveService = registry.Get<SaveServiceU>();

        saveButton.onClick.AddListener(Save);
        resetSaveButton.onClick.AddListener(ResetSave);
        manualTickButton.onClick.AddListener(ManualTick);

        Refresh();
    }

    private void Save()
    {
        _saveService.Save(_gameStateService.State);
        Refresh();
    }

    private void ResetSave()
    {
        // _saveService.DeleteSave();
        // _gameStateService.SetState(new SaveValidator().CreateNewState());
        // Refresh();
    }

    private void ManualTick()
    {
        _gameStateService.State.meta.totalTicks++;
        Refresh();
    }

    private void Refresh()
    {
        var state = _gameStateService.State;

        systemText.text =
            $"STAR FRONTIER\n" +
            $"System: {state.player.currentSystemId}\n" +
            $"Ticks: {state.meta.totalTicks}\n" +
            $"Saved: {state.meta.lastSavedAtUtc}";
    }
}