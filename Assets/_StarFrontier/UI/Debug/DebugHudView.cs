using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DebugHudView : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text infoText;

    [Header("Buttons")]
    [SerializeField] private Button manualTickButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button resetSaveButton;

    private GameStateService _gameStateService;
    private SaveServiceU _saveService;
    private TickService _tickService;

    private void Start()
    {
        var registry = GameBootstrapper.Services;

        if (registry == null)
        {
            UnityEngine.Debug.LogError("DebugHudView: ServiceRegistry is null.");
            return;
        }

        _gameStateService = registry.Get<GameStateService>();
        _saveService = registry.Get<SaveServiceU>();
        _tickService = registry.Get<TickService>();

        manualTickButton.onClick.AddListener(OnManualTickClicked);
        saveButton.onClick.AddListener(OnSaveClicked);
        loadButton.onClick.AddListener(OnLoadClicked);
        resetSaveButton.onClick.AddListener(OnResetSaveClicked);

        Refresh();
    }

    private void OnDestroy()
    {
        if (manualTickButton != null)
        {
            manualTickButton.onClick.RemoveListener(OnManualTickClicked);
        }

        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(OnSaveClicked);
        }

        if (loadButton != null)
        {
            loadButton.onClick.RemoveListener(OnLoadClicked);
        }

        if (resetSaveButton != null)
        {
            resetSaveButton.onClick.RemoveListener(OnResetSaveClicked);
        }
    }

    private void OnManualTickClicked()
    {
        if (_gameStateService?.State == null)
        {
            return;
        }

        _gameStateService.State.meta.totalTicks++;
        Refresh();
    }

    private void OnSaveClicked()
    {
        if (_gameStateService?.State == null)
        {
            return;
        }

        _saveService.Save(_gameStateService.State);
        Refresh();
    }

    private void OnLoadClicked()
    {
        var loadedState = _saveService.Load();
        _gameStateService.SetState(loadedState);
        Refresh();
    }

    private void OnResetSaveClicked()
    {
        _saveService.DeleteSave();

        var loadedState = _saveService.Load();
        _gameStateService.SetState(loadedState);

        Refresh();
    }

    private void Refresh()
    {
        if (infoText == null || _gameStateService?.State == null)
        {
            return;
        }

        var state = _gameStateService.State;

        infoText.text =
            "STAR FRONTIER DEBUG\n" +
            $"System: {state.player.currentSystemId}\n" +
            $"Ticks: {state.meta.totalTicks}\n" +
            $"Save Version: {state.meta.saveVersion}\n" +
            $"TickService Running: {_tickService.IsRunning}";
    }
}