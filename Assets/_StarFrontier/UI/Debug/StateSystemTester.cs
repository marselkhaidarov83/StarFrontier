using UnityEngine;

public sealed class StateSystemTester : MonoBehaviour
{
    private void Start()
    {
        var state = StateFactory.CreateNewGameState(
            saveVersion: 1,
            playerName: "Captain",
            startSystemId: "system_start_01"
        );

        var gameStateService = new GameStateService();
        gameStateService.SetState(state);

        Debug.Log("StateSystemTester: GameState created successfully.");
        Debug.Log($"Player Name: {gameStateService.State.player.playerName}");
        Debug.Log($"Current System: {gameStateService.State.player.currentSystemId}");
        Debug.Log($"System Loaded: {gameStateService.State.currentSystem.isLoaded}");
        Debug.Log($"Save Version: {gameStateService.State.meta.saveVersion}");
        Debug.Log($"Created At UTC: {gameStateService.State.meta.createdAtUtc}");
        Debug.Log($"Total Ticks: {gameStateService.State.meta.totalTicks}");
    }
}