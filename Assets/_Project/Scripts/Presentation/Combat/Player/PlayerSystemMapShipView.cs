using UnityEngine;
using UnityEngine.EventSystems;

public sealed class PlayerSystemMapShipView :
    MonoBehaviour,
    IPointerClickHandler
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
        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        double getShipMs = 0.0;
        double applyPositionMs = 0.0;

        bool activeShipFound = false;
        bool hidden = false;

        try
        {
            if (_isDestroyed)
                return;

            double phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            ShipRuntimeData activeShip = GetActiveShip();

            getShipMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;

            activeShipFound =
                activeShip != null;

            if (activeShip == null)
                return;

            if (activeShip.CurrentHull <= 0)
            {
                hidden = true;
                HideShip();
                return;
            }

            phaseStartedAt =
                Time.realtimeSinceStartupAsDouble;

            Vector3 position = _gameSessionService.State.Player.SystemMapShipPosition;
            position.z = transform.position.z;
            transform.position = position;

            applyPositionMs =
                (Time.realtimeSinceStartupAsDouble - phaseStartedAt) * 1000.0;
        }
        finally
        {
            double elapsedMs =
                (Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0;

            string details =
                "Name=" + gameObject.name +
                " | IsDestroyed=" + _isDestroyed +
                " | ActiveShipFound=" + activeShipFound +
                " | Hidden=" + hidden +
                " | GetShipMs=" + getShipMs.ToString("F3") +
                " | ApplyPositionMs=" + applyPositionMs.ToString("F3");

            VisualUpdateAggregateLog.Record(
                "PlayerSystemMapShipView.Update",
                elapsedMs,
                details);

            VisualUpdatePerfLog.LogIfSlow(
                "PlayerSystemMapShipView.Update",
                startedAt,
                details);
        }
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
        if (_gameSessionService?.State?.Player?.PlayerShipState == null)
            return null;

        return _gameSessionService.State.Player.PlayerShipState.GetActiveShip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _eventBus?.Publish(
            new SystemObjectsPanelCloseRequestedEvent2A());
    }
}
