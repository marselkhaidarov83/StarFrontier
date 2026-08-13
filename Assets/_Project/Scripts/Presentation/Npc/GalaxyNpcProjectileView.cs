using UnityEngine;

public sealed class GalaxyNpcProjectileView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float rotationOffsetDegrees = -90f;

    public string ProjectileId { get; private set; }
    public bool IsActive { get; private set; }

    private ISystemNpcCombatService _combatService;
    private Vector3 _lastPosition;

    public void Init(GalaxyNpcProjectileCreatedEvent evt)
    {
        ProjectileId = evt.ProjectileId;
        IsActive = true;

        transform.position = evt.StartPosition;
        _lastPosition = evt.StartPosition;

        ResolveCombatService();
        ApplyColor(evt);

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
        Vector3 direction = position - _lastPosition;

        transform.position = position;

        if (direction.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(
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

    private void ApplyColor(GalaxyNpcProjectileCreatedEvent evt)
    {
        if (spriteRenderer == null)
            return;

        if (string.IsNullOrWhiteSpace(evt.ShooterNpcId))
        {
            spriteRenderer.color = new Color(0.35f, 0.95f, 1f, 1f);
            return;
        }

        if (evt.TargetType == CombatTargetType.Player)
        {
            spriteRenderer.color = new Color(1f, 0.25f, 0.18f, 1f);
            return;
        }

        spriteRenderer.color = new Color(0.45f, 0.7f, 1f, 1f);
    }
}