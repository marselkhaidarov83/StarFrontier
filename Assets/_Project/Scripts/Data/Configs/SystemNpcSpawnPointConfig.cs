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

    [SerializeField] [Min(0f)] private float enemyRandomRadius = 35f;

    public Vector3[] EnemySpawnPoints => enemySpawnPoints;

    public float EnemyRandomRadius => enemyRandomRadius;

    public Vector3 PickEnemySpawnBasePosition()
    {
        Vector3 basePosition =
            PickEnemySpawnPoint();

        basePosition.z = 0f;

        return basePosition;
    }

    public Vector3 PickEnemySpawnPosition()
    {
        Vector3 basePosition =
            PickEnemySpawnBasePosition();

        return BuildTangentialScatterPosition(
            basePosition,
            enemyRandomRadius);
    }

    private Vector3 PickEnemySpawnPoint()
    {
        if (enemySpawnPoints == null || enemySpawnPoints.Length == 0)
            return new Vector3(6f, 0f, 0f);

        int index =
            Random.Range(0, enemySpawnPoints.Length);

        return enemySpawnPoints[index];
    }

    private static Vector3 BuildTangentialScatterPosition(
        Vector3 basePosition,
        float scatterRadius)
    {
        Vector2 radial =
            new Vector2(basePosition.x, basePosition.y);

        if (radial.sqrMagnitude <= 0.0001f)
        {
            radial = Vector2.right;
        }
        else
        {
            radial.Normalize();
        }

        Vector2 tangent =
            new Vector2(-radial.y, radial.x);

        float tangentOffset =
            Random.Range(-scatterRadius, scatterRadius);

        float outwardOffset =
            Random.Range(0f, scatterRadius * 0.25f);

        Vector2 scattered =
            new Vector2(basePosition.x, basePosition.y) +
            tangent * tangentOffset +
            radial * outwardOffset;

        return new Vector3(
            scattered.x,
            scattered.y,
            0f);
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
