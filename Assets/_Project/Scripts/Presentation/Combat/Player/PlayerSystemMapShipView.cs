using UnityEngine;

public sealed class PlayerSystemMapShipView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Collider2D shipCollider;

    private IGameSessionService _gameSessionService;
    private SimpleEventBus _eventBus;

    private bool _isDestroyed;

    private void Awake()
    {
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (shipCollider == null)
            shipCollider = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        _eventBus.Subscribe<PlayerShipDestroyedByNpcEvent>(OnPlayerShipDestroyed);
    }

    private void OnDisable()
    {
        if (_eventBus == null)
            return;

        _eventBus.Unsubscribe<PlayerShipDestroyedByNpcEvent>(OnPlayerShipDestroyed);
    }

    private void Update()
    {
        if (_isDestroyed)
            return;

        ShipRuntimeData activeShip = GetActiveShip();

        if (activeShip == null)
            return;

        if (activeShip.CurrentHull <= 0)
        {
            HideShip();
            return;
        }

        Vector3 position = _gameSessionService.CurrentSave.PlayerProfile.SystemMapShipPosition;
        position.z = transform.position.z;
        transform.position = position;
    }

    private void OnPlayerShipDestroyed(PlayerShipDestroyedByNpcEvent evt)
    {
        HideShip();
    }

    private void HideShip()
    {
        _isDestroyed = true;

        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        if (shipCollider != null)
            shipCollider.enabled = false;

        gameObject.SetActive(false);

        Debug.Log("[PlayerSystemMapShipView] Player ship hidden after destruction.");
    }

    private ShipRuntimeData GetActiveShip()
    {
        if (_gameSessionService?.CurrentSave?.PlayerProfile?.PlayerShipState == null)
            return null;

        return _gameSessionService.CurrentSave.PlayerProfile.PlayerShipState.GetActiveShip();
    }
}