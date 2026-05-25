using UnityEngine;

    public sealed class SystemEncounterRuntimeRoot : MonoBehaviour
    {
        [Header("Runtime Parents")]
        [SerializeField] private Transform enemiesRoot;
        [SerializeField] private Transform alliesRoot;
        [SerializeField] private Transform projectilesRoot;
        [SerializeField] private Transform vfxRoot;

        public Transform EnemiesRoot => enemiesRoot;
        public Transform AlliesRoot => alliesRoot;
        public Transform ProjectilesRoot => projectilesRoot;
        public Transform VfxRoot => vfxRoot;

        private void OnValidate()
        {
            if (enemiesRoot == null)
                enemiesRoot = transform.Find("EnemiesRoot");

            if (alliesRoot == null)
                alliesRoot = transform.Find("AlliesRoot");

            if (projectilesRoot == null)
                projectilesRoot = transform.Find("ProjectilesRoot");

            if (vfxRoot == null)
                vfxRoot = transform.Find("VfxRoot");
        }
    }