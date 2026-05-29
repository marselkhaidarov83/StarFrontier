using UnityEngine;

public sealed class Sprint1FinalValidator : MonoBehaviour
{
    private void Start()
    {
        var services = GameBootstrapper.Services;

        if (services == null)
        {
            Debug.LogError("Sprint1FinalValidator: ServiceRegistry is null.");
            return;
        }

        Validate<ServiceRegistry>(services);
        Validate<SimpleEventBus>(services);
        Validate<ConfigService>(services);
        Validate<TickService>(services);
        Validate<SaveServiceU>(services);
        Validate<GameStateService>(services);

        var gameStateService = services.Get<GameStateService>();

        if (gameStateService == null || gameStateService.State == null)
        {
            Debug.LogError("Sprint1FinalValidator: GameState is missing.");
            return;
        }

        Debug.Log("Sprint1FinalValidator: GameState exists.");
        Debug.Log($"Sprint1FinalValidator: Current system = {gameStateService.State.player.currentSystemId}");
        Debug.Log($"Sprint1FinalValidator: Total ticks = {gameStateService.State.meta.totalTicks}");
        Debug.Log("Sprint1FinalValidator: Sprint 1 validation completed.");
    }

    private void Validate<T>(ServiceRegistry services) where T : class
    {
        if (services.TryGet<T>(out _))
        {
            Debug.Log($"Sprint1FinalValidator: {typeof(T).Name} OK.");
        }
        else
        {
            Debug.LogError($"Sprint1FinalValidator: {typeof(T).Name} missing.");
        }
    }
}