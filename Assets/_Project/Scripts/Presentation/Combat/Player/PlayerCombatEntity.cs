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

        public PlayerCombatRuntimeState State => _state;
        public bool IsAlive => _state.IsAlive;

        private void Awake()
        {
            _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
            _encounterService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEncounterService>();

            _state.Init(maxHull, maxShield, maxEnergy);

            PublishStatsChanged();
        }

        public void ApplyDamage(int damage)
        {
            if (!_state.IsAlive)
                return;

            if (damage <= 0)
                return;

            int remainingDamage = damage;

            if (_state.CurrentShield > 0)
            {
                int shieldDamage = Mathf.Min(_state.CurrentShield, remainingDamage);
                _state.CurrentShield -= shieldDamage;
                remainingDamage -= shieldDamage;
            }

            if (remainingDamage > 0)
            {
                _state.CurrentHull -= remainingDamage;
            }

            if (_state.CurrentHull < 0)
                _state.CurrentHull = 0;

            _eventBus.Publish(new PlayerDamagedEvent(
                damage,
                _state.CurrentHull,
                _state.CurrentShield
            ));

            PublishStatsChanged();

            Debug.Log(
                $"[PlayerCombatEntity] Damage: {damage}, Hull: {_state.CurrentHull}/{_state.MaxHull}, Shield: {_state.CurrentShield}/{_state.MaxShield}"
            );

            if (_state.CurrentHull <= 0)
                DestroyPlayer();
        }

        // public void RepairFull()
        // {
        //     _state.CurrentHull = _state.MaxHull;
        //     _state.CurrentShield = _state.MaxShield;
        //     _state.CurrentEnergy = _state.MaxEnergy;
        //     _state.IsAlive = true;

        //     PublishStatsChanged();

        //     Debug.Log("[PlayerCombatEntity] Player repaired full.");
        // }

        // [ContextMenu("Debug Damage 10")]
        // private void DebugDamage10()
        // {
        //     ApplyDamage(10);
        // }

        // [ContextMenu("Debug Damage 999")]
        // private void DebugDamage999()
        // {
        //     ApplyDamage(999);
        // }

        // [ContextMenu("Debug Repair Full")]
        // private void DebugRepairFull()
        // {
        //     RepairFull();
        // }

        private void DestroyPlayer()
        {
            if (!_state.IsAlive)
                return;

            _state.IsAlive = false;

            Debug.Log("[PlayerCombatEntity] Player destroyed.");

            _eventBus.Publish(new PlayerDestroyedEvent());

            if (_encounterService != null)
                _encounterService.RegisterPlayerDestroyed();
        }

        private void PublishStatsChanged()
        {
            _eventBus.Publish(new PlayerCombatStatsChangedEvent(
                _state.CurrentHull,
                _state.MaxHull,
                _state.CurrentShield,
                _state.MaxShield,
                _state.CurrentEnergy,
                _state.MaxEnergy
            ));
        }
    }