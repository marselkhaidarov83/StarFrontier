using UnityEngine;

public sealed class GalaxyNpcProjectileView : MonoBehaviour
{
    private string _projectileId;
    private bool _initialized;

    private ISystemNpcCombatService _combatService;

    public string ProjectileId => _projectileId;

    private void Awake()
    {
        _combatService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcCombatService>();
    }

    public void Init(GalaxyNpcProjectileCreatedEvent evt)
    {
        _projectileId = evt.ProjectileId;
        transform.position = evt.StartPosition;
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized)
            return;

        if (!_combatService.TryGetProjectile(
                _projectileId,
                out GalaxyNpcProjectileRuntimeState projectile))
        {
            Destroy(gameObject);
            return;
        }

        transform.position = projectile.CurrentPosition;
    }

    public void Complete()
    {
        Destroy(gameObject);
    }
}