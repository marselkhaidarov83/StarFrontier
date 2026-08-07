using UnityEngine;

[CreateAssetMenu(
    fileName = "SystemNpcSpawnPointConfig",
    menuName = "StarFrontier/Configs/System/System NPC Spawn Points")]
public sealed class SystemNpcSpawnPointConfig : BaseConfig
{
    [Header("Enemy Spawn Points")]
    [SerializeField] private Vector3[] enemySpawnPoints =
    {
        new Vector3(6f, 0f, 0f)
    };

    [SerializeField] [Min(0f)] private float enemyRandomRadius = 200f;

    public Vector3[] EnemySpawnPoints => enemySpawnPoints;

    public float EnemyRandomRadius => enemyRandomRadius;

    public Vector3 PickEnemySpawnPosition()
    {
        Vector3 basePosition =
            PickEnemySpawnPoint();

        Vector2 randomOffset =
            Random.insideUnitCircle * enemyRandomRadius;

        basePosition.x += randomOffset.x;
        basePosition.y += randomOffset.y;
        basePosition.z = 0f;

        return basePosition;
    }

    private Vector3 PickEnemySpawnPoint()
    {
        if (enemySpawnPoints == null || enemySpawnPoints.Length == 0)
            return new Vector3(6f, 0f, 0f);

        int index =
            Random.Range(0, enemySpawnPoints.Length);

        return enemySpawnPoints[index];
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        enemyRandomRadius =
            Mathf.Max(0f, enemyRandomRadius);

        if (enemySpawnPoints == null || enemySpawnPoints.Length == 0)
        {
            enemySpawnPoints =
                new[]
                {
                    new Vector3(6f, 0f, 0f)
                };
        }
    }
#endif
}
