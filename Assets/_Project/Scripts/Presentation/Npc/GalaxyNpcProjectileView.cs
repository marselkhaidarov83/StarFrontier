using UnityEngine;

public sealed class GalaxyNpcProjectileView : CustomMonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float rotationOffsetDegrees = -90f;

    public string ProjectileId { get; private set; }
    public bool IsActive { get; private set; }

    private ISystemNpcCombatService _combatService;
    private ProjectileWeaponVisualSettings2A _settings;
    private Vector3 _lastPosition;

    public void Init(GalaxyNpcProjectileCreatedEvent evt)
    {
        ProjectileId = evt.ProjectileId;
        IsActive = true;

        ResolveSettings();
        ResolveSpriteRenderer();

        _lastPosition = evt.StartPosition;
        SetPosition(evt.StartPosition);

        ResolveCombatService();
        ApplyRenderOrder();

        gameObject.SetActive(true);
    }

    private void Update()
    {
        if (!IsActive)
            return;

        if (string.IsNullOrWhiteSpace(ProjectileId))
        {
            Complete();
            return;
        }

        ResolveCombatService();

        if (_combatService == null)
            return;

        if (!_combatService.TryGetProjectile(
                ProjectileId,
                out GalaxyNpcProjectileRuntimeState projectile))
        {
            Complete();
            return;
        }

        if (projectile == null || projectile.IsResolved)
        {
            Complete();
            return;
        }

        SetPosition(projectile.CurrentPosition);
    }

    public void SetPosition(Vector3 position)
    {
        ResolveSettings();

        if (_settings != null && _settings.ForceWorldZ)
            position.z = _settings.WorldZ;

        Vector3 direction = position - _lastPosition;

        transform.position = position;

        if (direction.sqrMagnitude > 0.0001f)
        {
            float angle =
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            transform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    angle + rotationOffsetDegrees);
        }

        _lastPosition = position;
    }

    public void Complete()
    {
        ProjectileId = null;
        IsActive = false;
        gameObject.SetActive(false);
    }

    private void ResolveCombatService()
    {
        if (_combatService != null)
            return;

        Bootstrapper bootstrapper = Bootstrapper.Instance;

        if (bootstrapper == null || bootstrapper.ServiceRegistry == null)
            return;

        bootstrapper.ServiceRegistry.TryGet(out _combatService);
    }

    private void ResolveSettings()
    {
        if (_settings != null)
            return;

        _settings = GetComponent<ProjectileWeaponVisualSettings2A>();
    }

    private void ResolveSpriteRenderer()
    {
        if (spriteRenderer != null)
            return;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void ApplyRenderOrder()
    {
        if (spriteRenderer == null)
            return;

        if (_settings == null)
            return;

        spriteRenderer.sortingLayerName = _settings.SortingLayerName;
        spriteRenderer.sortingOrder = _settings.SortingOrder;
    }
}