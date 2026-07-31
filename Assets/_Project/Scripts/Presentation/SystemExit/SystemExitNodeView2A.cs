using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SystemExitNodeView2A : CustomMonoBehaviour, IPointerClickHandler
{
    [Header("View")]
    [SerializeField] private SpriteRenderer systemExitImage;
    [SerializeField] private Sprite systemExitSprite;
    [SerializeField] private TMP_Text systemNameText;

    [Header("Size")]
    [SerializeField] private float size = 50f;

    private RouteConfig _routeConfig;
    private RouteEndpointConfig _endpointConfig;

    private string _currentSystemId;
    private string _targetSystemId;

    private IGameSessionService _gameSessionService;
    private SimpleEventBus _simpleEventBus;

    public void Initialize(
        RouteConfig routeConfig,
        string currentSystemId,
        string targetSystemId,
        RouteEndpointConfig endpointConfig,
        Vector2 center)
    {
        _simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();

        _routeConfig = routeConfig;
        _endpointConfig = endpointConfig;
        _currentSystemId = currentSystemId;
        _targetSystemId = targetSystemId;

        if (_routeConfig == null)
        {
            Debug.LogError("[SystemExitNodeView2] routeConfig is null");
            return;
        }

        if (_endpointConfig == null)
        {
            Debug.LogError("[SystemExitNodeView2] endpointConfig is null");
            return;
        }

        StarSystemConfig targetSystem = _routeConfig.GetOtherSystem(_currentSystemId);

        if (targetSystem != null && systemNameText != null)
            systemNameText.SetText(targetSystem.DisplayName);

        if (systemExitImage != null)
        {
            systemExitImage.sprite = systemExitSprite;

            SpriteRendererSizeUtility.SetWorldSize(
                systemExitImage,
                size
            );
        }

        transform.position = _endpointConfig.ExitPoint;

        if (IsDebug())
        {
            Debug.Log(
                "[SystemExitNodeView2] Initialized route = " +
                _routeConfig.Id +
                " | current = " +
                _currentSystemId +
                " | target = " +
                _targetSystemId +
                " | exitPoint = " +
                _endpointConfig.ExitPoint
            );
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        LogCustom(
            "route = " + (_routeConfig != null ? _routeConfig.Id : "null") +
            " | endpoint = " + (_endpointConfig != null ? _endpointConfig.Id : "null") +
            " | current = " + _currentSystemId +
            " | target = " + _targetSystemId
        );

        if (_routeConfig == null)
        {
            Debug.LogError("[SystemExitNodeView2] Cannot click exit. routeConfig is null");
            return;
        }

        if (_endpointConfig == null)
        {
            Debug.LogError("[SystemExitNodeView2] Cannot click exit. endpointConfig is null");
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentSystemId))
        {
            Debug.LogError("[SystemExitNodeView2] Cannot click exit. currentSystemId is empty");
            return;
        }

        if (string.IsNullOrWhiteSpace(_targetSystemId))
        {
            Debug.LogError("[SystemExitNodeView2] Cannot click exit. targetSystemId is empty");
            return;
        }

        _simpleEventBus.Publish(new RouteExitMapChangedEvent(
            _routeConfig,
            _currentSystemId,
            _targetSystemId,
            _endpointConfig.ExitPoint,
            _routeConfig.GetEntryPoint(_targetSystemId)
        ));
    }
}