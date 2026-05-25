using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SystemExitNodeView2 : CustomMonoBehaviour, IPointerClickHandler
{
    [SerializeField] private SpriteRenderer systemExitImage;
    [SerializeField] private Sprite systemExitSprite;
    [SerializeField] private TMP_Text systemNameText;
    // [SerializeField] private float size = 50f;

    private StarSystemLink _systemLink;

    private IGameSessionService _gameSessionService;
    private SimpleEventBus _simpleEventBus;

    public void Initialize(StarSystemLink systemLink, Vector2 center)
    {
        _simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();

        _systemLink = systemLink;

        systemNameText?.SetText(systemLink.LinkedSystem.name);
        if (systemLink.Sprite != null)
            systemExitImage.sprite = systemLink.Sprite;
        else
            systemExitImage.sprite = systemExitSprite;

        transform.position = _systemLink.ExitPoint;
        SpriteRendererSizeUtility.SetWorldSize(
            systemExitImage,
            systemLink.Size
        );
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (IsDebug())
            Debug.Log("[SystemExitNodeView2] OnPointerClick -> " + eventData);
        
        // _gameSessionService.CurrentSave.PlayerProfile.CurrentStarSystemLink = _systemLink;
        _simpleEventBus.Publish(new ExitMapChangedEvent(_systemLink));
    }
}