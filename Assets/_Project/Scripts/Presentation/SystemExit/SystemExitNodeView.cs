using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SystemExitNodeView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private TMP_Text systemNameText;

    [SerializeField] private StarSystemLink _systemLink;
    public StarSystemLink SystemLink => _systemLink;
    private IGameSessionService _gameSessionService;
    private SimpleEventBus _simpleEventBus;

    public void Initialize(StarSystemLink systemLink, Vector2 center)
    {
        _simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();

        _systemLink = systemLink;

        systemNameText?.SetText(systemLink.LinkedSystem.name);

        rectTransform = GetComponent<RectTransform>();
        rectTransform.anchoredPosition = _systemLink.ExitPoint;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("[SystemExitNodeView] OnPointerClick -> " + eventData);
        
        // _gameSessionService.CurrentSave.PlayerProfile.CurrentStarSystemLink = _systemLink;
        _simpleEventBus.Publish(new ExitMapChangedEvent(_systemLink));
    }
}