using UnityEngine;

public sealed class LegacyMigrationValidator : MonoBehaviour
{
    private void Start()
    {
        ValidateNewCore();
        PrintMigrationRules();
    }

    private void ValidateNewCore()
    {
        var services = GameBootstrapper.Services;

        if (services == null)
        {
            Debug.LogError("LegacyMigrationValidator: ServiceRegistry is null.");
            return;
        }

        ValidateService<ServiceRegistry>(services);
        ValidateService<SimpleEventBus>(services);
        ValidateService<ConfigService>(services);
        ValidateService<TickService>(services);
        ValidateService<SaveServiceU>(services);
        ValidateService<GameStateService>(services);

        var gameStateService = services.Get<GameStateService>();

        if (gameStateService != null && gameStateService.State != null)
        {
            Debug.Log($"LegacyMigrationValidator: GameState OK. Current system = {gameStateService.State.player.currentSystemId}");
            Debug.Log($"LegacyMigrationValidator: MetaStateU OK. Total ticks = {gameStateService.State.meta.totalTicks}");
        }
        else
        {
            Debug.LogError("LegacyMigrationValidator: GameState is missing.");
        }
    }

    private void ValidateService<T>(ServiceRegistry services) where T : class
    {
        if (services.TryGet<T>(out _))
        {
            Debug.Log($"LegacyMigrationValidator: {typeof(T).Name} registered.");
        }
        else
        {
            Debug.LogError($"LegacyMigrationValidator: {typeof(T).Name} is missing.");
        }
    }

    private void PrintMigrationRules()
    {
        Debug.Log("LegacyMigrationValidator: Sprint 1 legacy migration mode = SAFE / NO DELETION.");

        foreach (string item in LegacyMigrationReport.KeepLegacySystems)
        {
            Debug.Log($"Legacy Keep: {item}");
        }

        foreach (string item in LegacyMigrationReport.DoNotTouchInSprint1)
        {
            Debug.Log($"Legacy Do Not Touch: {item}");
        }
    }
}