using System;
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
    [SerializeField] private bool fireImmediatelyOnSelect = false;

    private EnemySystemMapEntity _selectedTarget;
    private SimpleEventBus _simpleEventBus;

    private bool _isSubscribed;
    private int _lastFiredTick = int.MinValue;
    private int _manualFallbackTick;

    private void Awake()
    {
        if (parentTransform == null)
            parentTransform = transform.parent;
    }

    private void OnEnable()
    {
        TryResolveEventBus();
        TrySubscribe();
    }

    private void OnDisable()
    {
        TryUnsubscribe();
    }

    public void SelectTarget(EnemySystemMapEntity target)
    {
        if (target == null || !target.IsBound)
            return;

        _selectedTarget = target;

        Debug.Log($"[WeaponFireController] Target selected: {_selectedTarget.RuntimeEnemyId}");

        if (fireImmediatelyOnSelect)
        {
            _manualFallbackTick++;
            TryFire(_manualFallbackTick);
        }
    }

    public void ClearTarget(EnemySystemMapEntity target)
    {
        if (_selectedTarget == target)
        {
            _selectedTarget = null;
            Debug.Log("[WeaponFireController] Target cleared.");
        }
    }

    private void OnGameDayChanged(GameDayChangedEvent eventData)
    {
        if (!autoFireAtSelectedTarget)
            return;

        if (_selectedTarget == null)
            return;

        TryFire(eventData.CurrentDay);
    }

    private void TryFire(int tick)
    {
        if (_selectedTarget == null)
            return;

        if (!_selectedTarget.IsBound)
        {
            _selectedTarget = null;
            return;
        }

        if (_lastFiredTick == tick)
            return;

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

        WeaponRuntimeStats weaponStats =
            weaponConfig.RollRuntimeStats(
                BuildWeaponRollSeed(
                    "player",
                    _selectedTarget.RuntimeEnemyId,
                    weaponConfig.Id,
                    tick.ToString()
                )
            );

        Vector3 startPosition = transform.position;
        startPosition.z = 0f;

        Vector3 targetPosition = _selectedTarget.transform.position;
        targetPosition.z = 0f;

        float distance = Vector3.Distance(startPosition, targetPosition);

        if (distance > weaponStats.Range)
        {
            Debug.Log(
                "[WeaponFireController] Target out of range. " +
                $"Target: {_selectedTarget.RuntimeEnemyId}, " +
                $"Distance: {distance:F2}, Range: {weaponStats.Range:F2}"
            );

            return;
        }

        FireAtSelectedTarget(weaponStats);

        _lastFiredTick = tick;
    }

    private void FireAtSelectedTarget(WeaponRuntimeStats weaponStats)
    {
        if (_selectedTarget == null)
            return;

        Vector3 startPosition = transform.position;
        startPosition.z = 0f;

        Vector3 targetPosition = _selectedTarget.transform.position;
        targetPosition.z = 0f;

        Vector3 directionToTarget = targetPosition - startPosition;
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

        ProjectileMover mover = projectile.GetComponent<ProjectileMover>();

        if (mover == null)
        {
            Debug.LogError("[WeaponFireController] ProjectileMover is missing on projectile prefab.");
            Destroy(projectile.gameObject);
            return;
        }

        int projectileLifetimeTicks =
            Mathf.Max(1, weaponStats.ProjectileLifetime);

        float projectileLifetimeSeconds =
            Mathf.Max(
                0.1f,
                GameTimeService.SecondsPerDay * projectileLifetimeTicks
            );

        float distance =
            Vector3.Distance(
                startPosition,
                targetPosition
            );

        /*
         * projectileSpeed удалён из WeaponConfig v0.6.
         * Для визуального полёта считаем скорость автоматически:
         * снаряд должен долететь до цели за projectileLifetime.
         */
        float visualProjectileSpeed =
            Mathf.Max(
                0.01f,
                distance / projectileLifetimeSeconds
            );

        mover.InitToTarget(
            _selectedTarget.transform,
            visualProjectileSpeed,
            projectileLifetimeSeconds
        );

        projectile.Init(
            weaponStats.Damage,
            fromPlayer: true
        );

        Debug.Log(
            "[WeaponFireController] Projectile fired. " +
            $"Target: {_selectedTarget.RuntimeEnemyId}, " +
            $"Weapon: {weaponConfig.Id}, " +
            $"Damage: {weaponStats.Damage}, " +
            $"Range: {weaponStats.Range:F2}, " +
            $"LifetimeTicks: {projectileLifetimeTicks}, " +
            $"VisualSpeed: {visualProjectileSpeed:F2}"
        );
    }

    private void TryResolveEventBus()
    {
        if (_simpleEventBus != null)
            return;

        try
        {
            if (Bootstrapper.Instance == null)
                return;

            if (Bootstrapper.Instance.ServiceRegistry == null)
                return;

            _simpleEventBus =
                Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[WeaponFireController] Failed to resolve SimpleEventBus: " + exception.Message);
        }
    }

    private void TrySubscribe()
    {
        if (_isSubscribed)
            return;

        if (_simpleEventBus == null)
            return;

        _simpleEventBus.Subscribe<GameDayChangedEvent>(OnGameDayChanged);
        _isSubscribed = true;
    }

    private void TryUnsubscribe()
    {
        if (!_isSubscribed)
            return;

        if (_simpleEventBus == null)
            return;

        _simpleEventBus.Unsubscribe<GameDayChangedEvent>(OnGameDayChanged);
        _isSubscribed = false;
    }

    private static int BuildWeaponRollSeed(params string[] parts)
    {
        unchecked
        {
            int hash = 17;

            if (parts == null)
                return hash;

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];

                if (string.IsNullOrEmpty(part))
                {
                    hash = hash * 31;
                    continue;
                }

                for (int j = 0; j < part.Length; j++)
                    hash = hash * 31 + part[j];
            }

            return hash;
        }
    }
}