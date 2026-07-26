using UnityEngine;

/// <summary>
/// Синхронизирует Movement State и persistent PlayerState.
/// Источником position/direction является MovementService.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerShipSaveRuntimeBridge2A :
    MonoBehaviour
{
    [SerializeField]
    private bool initializeFromSaveOnStart = true;

    [SerializeField]
    private bool keepRuntimePlayerStateUpdated = true;

    [SerializeField]
    private bool logInitialization = true;

    private IGameSessionService _gameSessionService;
    private IShipMovementService _movementService;
    private PlayerShipSaveSyncService2A _saveSyncService;

    private bool _initialized;
    private bool _errorReported;

    private void Start()
    {
        TryInitialize();
    }

    private void LateUpdate()
    {
        if (!keepRuntimePlayerStateUpdated)
            return;

        if (!TryInitialize())
            return;

        if (_gameSessionService.State == null)
            return;

        _saveSyncService.WriteMovementToSave(
            _gameSessionService.State);
    }

    public void RefreshMovementFromCurrentSave()
    {
        if (!TryInitialize())
            return;

        _saveSyncService.InitializeMovementFromSave(
            _gameSessionService.State);
    }

    public void WriteMovementToRuntimeSaveState()
    {
        if (!TryInitialize())
            return;

        _saveSyncService.WriteMovementToSave(
            _gameSessionService.State);
    }

    private bool TryInitialize()
    {
        if (_initialized)
            return true;

        if (Bootstrapper.Instance == null ||
            Bootstrapper.Instance.ServiceRegistry == null)
        {
            return false;
        }

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _gameSessionService);

        Bootstrapper.Instance.ServiceRegistry.TryGet(
            out _movementService);

        if (_gameSessionService == null ||
            _movementService == null)
        {
            ReportErrorOnce(
                "[PlayerShipSaveRuntimeBridge2A] " +
                "GameSession or Movement service not found.");
            return false;
        }

        // Создаём чистый adapter вокруг уже существующего
        // authoritative MovementService.
        _saveSyncService =
            new PlayerShipSaveSyncService2A(
                _movementService);

        _initialized = true;

        if (initializeFromSaveOnStart &&
            _gameSessionService.State != null)
        {
            _saveSyncService.InitializeMovementFromSave(
                _gameSessionService.State);
        }

        if (logInitialization)
        {
            Debug.Log(
                "[PlayerShipSaveRuntimeBridge2A] Initialized.",
                this);
        }

        return true;
    }

    private void ReportErrorOnce(string message)
    {
        if (_errorReported)
            return;

        _errorReported = true;
        Debug.LogError(message, this);
    }
}