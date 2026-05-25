using UnityEngine;

    public sealed class SystemEncounterSpawnDebugController : MonoBehaviour
    {
        [SerializeField] private SystemEncounterSpawner spawner;
        [SerializeField] private Transform spawnCenter;

    void Start()
    {
        // SpawnDebugEncounterAtCenter();
    }

    private void Awake()
        {
            if (spawner == null)
                spawner = FindObjectOfType<SystemEncounterSpawner>();
        }

        [ContextMenu("Spawn Debug Enemy Encounter At Center")]
        private void SpawnDebugEncounterAtCenter()
        {
            if (spawner == null)
            {
                Debug.LogError("[SystemEncounterSpawnDebugController] Spawner is missing.");
                return;
            }

            Vector3 position = Vector3.zero;

            if (spawnCenter != null)
                position = spawnCenter.position;

            spawner.SpawnDebugEncounter(position);
        }

        [ContextMenu("Clear Runtime Objects")]
        private void ClearRuntimeObjects()
        {
            if (spawner == null)
            {
                Debug.LogError("[SystemEncounterSpawnDebugController] Spawner is missing.");
                return;
            }

            spawner.ClearRuntimeObjects();
        }
    }