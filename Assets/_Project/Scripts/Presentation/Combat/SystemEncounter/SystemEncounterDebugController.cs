using UnityEngine;

    public sealed class SystemEncounterDebugController : MonoBehaviour
    {
        [Header("Debug Encounter")]
        [SerializeField] private string encounterId = "encounter_debug_pirates";
        [SerializeField] private string systemId = "system_debug";
        [SerializeField] private int enemies = 3;
        [SerializeField] private int allies = 2;

        private ISystemEncounterService _encounterService;

        private void Awake()
        {
            _encounterService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEncounterService>();
        }

        [ContextMenu("Start Debug Encounter")]
        private void StartDebugEncounter()
        {
            _encounterService.StartEncounter(encounterId, systemId, enemies, allies);
            _encounterService.DebugPrintCurrentState();
        }

        [ContextMenu("Print Current State")]
        private void PrintCurrentState()
        {
            _encounterService.DebugPrintCurrentState();
        }

        [ContextMenu("Kill Enemy By Player")]
        private void KillEnemyByPlayer()
        {
            _encounterService.RegisterEnemyDestroyed(true);
            _encounterService.DebugPrintCurrentState();
        }

        [ContextMenu("Kill Enemy By Ally")]
        private void KillEnemyByAlly()
        {
            _encounterService.RegisterEnemyDestroyed(false);
            _encounterService.DebugPrintCurrentState();
        }

        [ContextMenu("Kill Ally")]
        private void KillAlly()
        {
            _encounterService.RegisterAllyDestroyed();
            _encounterService.DebugPrintCurrentState();
        }

        [ContextMenu("Player Destroyed")]
        private void PlayerDestroyed()
        {
            _encounterService.RegisterPlayerDestroyed();
            _encounterService.DebugPrintCurrentState();
        }

        [ContextMenu("Player Entered Planet")]
        private void PlayerEnteredPlanet()
        {
            _encounterService.RegisterPlayerEnteredPlanet();
            _encounterService.DebugPrintCurrentState();
        }

        [ContextMenu("Player Launched From Planet")]
        private void PlayerLaunchedFromPlanet()
        {
            _encounterService.RegisterPlayerLaunchedFromPlanet();
            _encounterService.DebugPrintCurrentState();
        }

        [ContextMenu("Planet Day Passed")]
        private void PlanetDayPassed()
        {
            _encounterService.RegisterPlanetDayPassed();
            _encounterService.DebugPrintCurrentState();
        }

        [ContextMenu("Player Left System")]
        private void PlayerLeftSystem()
        {
            _encounterService.RegisterPlayerLeftSystem(systemId);
            _encounterService.DebugPrintCurrentState();
        }

        [ContextMenu("Resolve Pending Reward")]
        private void ResolvePendingReward()
        {
            bool resolved = _encounterService.TryResolvePendingReward();
            Debug.Log($"[SystemEncounterDebug] Resolve reward result: {resolved}");
            _encounterService.DebugPrintCurrentState();
        }

        [ContextMenu("Clear Encounter")]
        private void ClearEncounter()
        {
            _encounterService.ClearEncounter();
            Debug.Log("[SystemEncounterDebug] Encounter cleared.");
        }
    }