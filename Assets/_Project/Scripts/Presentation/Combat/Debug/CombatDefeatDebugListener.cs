#if UNITY_EDITOR || DEVELOPMENT_BUILD

using UnityEngine;

public sealed class CombatDefeatDebugListener : MonoBehaviour
{
    private SimpleEventBus _eventBus;
    private bool _isSubscribed;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        if (_isSubscribed)
            return;

        Bootstrapper bootstrapper = Bootstrapper.Instance;

        if (bootstrapper == null || bootstrapper.ServiceRegistry == null)
        {
            Debug.LogWarning("[CombatDefeatDebugListener] Bootstrapper or ServiceRegistry is unavailable.");
            return;
        }

        if (!bootstrapper.ServiceRegistry.TryGet(out _eventBus) || _eventBus == null)
        {
            Debug.LogWarning("[CombatDefeatDebugListener] SimpleEventBus is not registered.");
            return;
        }

        _eventBus.Subscribe<SystemEncounterDefeatedEvent>(OnSystemEncounterDefeated);
        _eventBus.Subscribe<CombatDefeatEvent2A>(OnCombatDefeat);

        _isSubscribed = true;

        Debug.Log("[CombatDefeatDebugListener] Subscribed to defeat events.");
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed || _eventBus == null)
            return;

        _eventBus.Unsubscribe<SystemEncounterDefeatedEvent>(OnSystemEncounterDefeated);
        _eventBus.Unsubscribe<CombatDefeatEvent2A>(OnCombatDefeat);

        _isSubscribed = false;
        _eventBus = null;
    }

    private void OnSystemEncounterDefeated(SystemEncounterDefeatedEvent evt)
    {
        Debug.Log(
            "[CombatDefeatDebugListener] SystemEncounterDefeatedEvent " +
            $"EncounterId={evt.EncounterId}, " +
            $"SystemId={evt.SystemId}, " +
            $"Reason={evt.Reason}");
    }

    private void OnCombatDefeat(CombatDefeatEvent2A evt)
    {
        Debug.Log(
            "[CombatDefeatDebugListener] CombatDefeatEvent2A " +
            $"EncounterId={evt.EncounterId}, " +
            $"SystemId={evt.SystemId}, " +
            $"Reason={evt.Reason}");
    }
}

#endif