using UnityEngine;

public sealed class PlayerCombatEntity : MonoBehaviour
{
    [Header("Temporary Combat Stats")]
    [SerializeField] private int maxHull = 100;
    [SerializeField] private int maxShield = 50;
    [SerializeField] private int maxEnergy = 100;

    private readonly PlayerCombatRuntimeState _state = new();

    private SimpleEventBus _eventBus;
    private ISystemEncounterService _encounterService;
    private IDamageService2A _damageService;

    public PlayerCombatRuntimeState State => _state;
    public bool IsAlive => _state.IsAlive;

    private void Awake()
    {
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _encounterService = ResolveEncounterService();
        _damageService = ResolveDamageService();

        _state.Init(maxHull, maxShield, maxEnergy);

        PublishStatsChanged();
    }

    public void ApplyDamage(int damage)
    {
        if (!_state.IsAlive)
            return;

        CombatDamageResult2A result = _damageService.ApplyDamage(
            _state.CurrentShield,
            _state.CurrentHull,
            damage);

        if (result.AppliedDamage <= 0)
            return;

        _state.CurrentShield = result.CurrentShield;
        _state.CurrentHull = result.CurrentHull;

        _eventBus.Publish(new PlayerDamagedEvent(
            result.AppliedDamage,
            _state.CurrentHull,
            _state.CurrentShield));

        PublishStatsChanged();

        if (result.IsDestroyed)
            DestroyPlayer();
    }

    private void DestroyPlayer()
    {
        if (!_state.IsAlive)
            return;

        _state.IsAlive = false;
        _state.CurrentHull = 0;

        _eventBus.Publish(new PlayerDestroyedEvent());

        if (_encounterService != null)
            _encounterService.RegisterPlayerDestroyed();

        PublishStatsChanged();
    }

    private void PublishStatsChanged()
    {
        _eventBus.Publish(new PlayerCombatStatsChangedEvent(
            _state.CurrentHull,
            _state.MaxHull,
            _state.CurrentShield,
            _state.MaxShield,
            _state.CurrentEnergy,
            _state.MaxEnergy));
    }

    private static IDamageService2A ResolveDamageService()
    {
        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null &&
            Bootstrapper.Instance.ServiceRegistry.TryGet(out IDamageService2A damageService))
        {
            return damageService;
        }

        return new DamageService2A();
    }

    private static ISystemEncounterService ResolveEncounterService()
    {
        if (Bootstrapper.Instance != null &&
            Bootstrapper.Instance.ServiceRegistry != null &&
            Bootstrapper.Instance.ServiceRegistry.TryGet(out ISystemEncounterService encounterService))
        {
            return encounterService;
        }

        return null;
    }
}