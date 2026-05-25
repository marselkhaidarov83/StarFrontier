using UnityEngine;

public sealed class WeaponFireController : MonoBehaviour
{
    [Header("Base")]
    [SerializeField] private Transform parentTransform;

    [Header("Weapon")]
    [SerializeField] private WeaponConfig weaponConfig;

    [Header("Projectile")]
    [SerializeField] private ProjectileView projectilePrefab;

    [Header("Fire")]
    [SerializeField] private bool autoFireAtSelectedTarget = true;

    private EnemySystemMapEntity _selectedTarget;
    private float _cooldown;

    private void Update()
    {
        if (_cooldown > 0f)
            _cooldown -= Time.deltaTime;

        if (autoFireAtSelectedTarget && _selectedTarget != null)
            TryFire();
    }

    public void SelectTarget(EnemySystemMapEntity target)
    {
        if (target == null || !target.IsBound)
            return;

        _selectedTarget = target;
        Debug.Log($"[WeaponFireController] Target selected: {_selectedTarget.RuntimeEnemyId}");
    }

    public void ClearTarget(EnemySystemMapEntity target)
    {
        if (_selectedTarget == target)
        {
            _selectedTarget = null;
            Debug.Log("[WeaponFireController] Target cleared.");
        }
    }

    private void TryFire()
    {
        if (weaponConfig == null)
        {
            Debug.LogWarning("[WeaponFireController] WeaponConfig is missing.");
            return;
        }

        if (projectilePrefab == null)
        {
            Debug.LogWarning("[WeaponFireController] ProjectilePrefab is missing.");
            return;
        }

        if (_selectedTarget == null)
            return;

        if (_cooldown > 0f)
            return;

        FireAtSelectedTarget();

        float fireRate = Mathf.Max(0.01f, weaponConfig.FireRate);
        _cooldown = 1f / fireRate;
    }

    private void FireAtSelectedTarget()
    {
        if (_selectedTarget == null)
            return;

        Vector3 startPosition = transform.position;
        startPosition.z = 0f;

        Vector3 directionToTarget = _selectedTarget.transform.position - startPosition;
        directionToTarget.z = 0f;

        if (directionToTarget.sqrMagnitude <= 0.0001f)
            return;

        startPosition += directionToTarget.normalized * 0.5f;

        ProjectileView projectile = Instantiate(
            projectilePrefab,
            startPosition,
            Quaternion.identity,
            parentTransform
        );
        // projectile.GetComponent<EnemySystemMapEntity> = 

        ProjectileMover mover = projectile.GetComponent<ProjectileMover>();

        if (mover == null)
        {
            Debug.LogError("[WeaponFireController] ProjectileMover is missing on projectile prefab.");
            Destroy(projectile.gameObject);
            return;
        }

        mover.InitToTarget(
            _selectedTarget.transform,
            weaponConfig.ProjectileSpeed,
            weaponConfig.ProjectileLifetime
        );

        projectile.Init(weaponConfig.BaseDamage, true);
    }
}