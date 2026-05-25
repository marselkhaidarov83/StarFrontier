using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class SystemNodeView2 : CustomMonoBehaviour, IPointerClickHandler
{
    [Header("View")]
    [SerializeField] private SpriteRenderer icon;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text fuelText;
    [SerializeField] private SpriteRenderer fuelImage;

    [Header("Params")]
    [SerializeField] private StarSystemConfig starSystemConfig;

    [Header("Visual")]
    [SerializeField] private Sprite normalSystemSprite;
    [SerializeField] private Color fuelColorNormal;
    // [SerializeField] private bool fuelVisibleNormal = true;
    [SerializeField] private Sprite fuelNormalSprite;

    [SerializeField] private Color fuelColorNotEnough;
    [SerializeField] private Sprite fuelNotEnoughSprite;

    [SerializeField] private Sprite currentSystemSprite;
    [SerializeField] private Color fuelColorCurrent;
    // [SerializeField] private bool fuelVisibleCurrent = false;

    // [SerializeField] private Sprite selectedSystemSprite;
    // [SerializeField] private Color fuelColorSelected;
    // [SerializeField] private bool fuelVisibleSelected = true;

    [SerializeField] private Sprite disabledSystemSprite;
    [SerializeField] private Color fuelColorDisabled;
    // [SerializeField] private bool fuelVisibleDisabled = false;
    private bool isSelected;

    private IGameSessionService _gameSessionService;
    private ITravelService _travelService;
    private Action<string> _onClick;

    public void Initialize(StarSystemConfig systemConfig, Action<string> onClick)
    {
        starSystemConfig = systemConfig;

        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _travelService = Bootstrapper.Instance.ServiceRegistry.Get<ITravelService>();

        // if (IsDebug())
        //     Debug.Log("SystemNodeView2: OnClick = " + onClick);

        _onClick = onClick;

        // if (_debugEnabled)
        //     Debug.Log("SystemNodeView2: Initialize ended");

        transform.position = new Vector3(
            systemConfig.MapPosition.x,
            systemConfig.MapPosition.y,
            0f
        );

        titleText?.SetText(starSystemConfig.DisplayName);

        if (icon != null)
        {
            if (starSystemConfig.Icon != null)
                icon.sprite = normalSystemSprite;
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (icon != null)
        {
            // iconRenderer.color = isSelected ? selectedColor : normalColor;
        }
    }

    public void SetState()
    {
        if (IsDebug())
            Debug.Log("[SystemNodeView2] SetState");
        if (IsDebug())
            Debug.Log("[SystemNodeView2] starSystemConfig = " + starSystemConfig);
        // if (IsDebug())
        //     Debug.Log("[SystemNodeView2] gameSessionService = " + _gameSessionService);
        
        Sprite sprite = normalSystemSprite;
        Color color = fuelColorNormal;
        Sprite fuelSprite = fuelNormalSprite;
        bool fuelVisible = true;

        TravelFailReason travelFailReason = _travelService.GetTravelFailReason(
            _gameSessionService.CurrentSave.PlayerProfile.CurrentSystemId, starSystemConfig.Id);

        if (IsDebug())
            Debug.Log("[SystemNodeView2] travelFailReason = " + travelFailReason);
        switch (travelFailReason)
        {
            case TravelFailReason.TargetSystemIsCurrent:
                sprite = currentSystemSprite;
                color = fuelColorCurrent;
                fuelVisible = false;
                break;

            case TravelFailReason.None:
                sprite = normalSystemSprite;
                color = fuelColorNormal;
                fuelSprite = fuelNormalSprite;
                fuelVisible = true;
                break;

            case TravelFailReason.NotEnoughFuel:
                sprite = normalSystemSprite;
                color = fuelColorNotEnough;
                fuelSprite = fuelNotEnoughSprite;
                fuelVisible = true;
                break;

            default:
                sprite = disabledSystemSprite;
                color = fuelColorDisabled;
                fuelVisible = false;
                break;
        }

        int travelCost = _travelService.GetTravelCost(
                    _gameSessionService.CurrentSave.PlayerProfile.CurrentSystemId,
                    starSystemConfig.Id);
        String fuelCount = travelCost == 0 ? "" : travelCost.ToString();

        if (icon != null)
        {
            if (IsDebug())
                Debug.Log("[SystemNodeView2] sprite = " + sprite);
            icon.sprite = sprite;
        }

        if (fuelImage != null)
        {
            if (IsDebug())
            {
                Debug.Log("[SystemNodeView2] FuelColor = " + color);
                Debug.Log("[SystemNodeView2] fuelSprite = " + fuelSprite);                
            }
            // fuelImage.color = color;  
            fuelImage.sprite = fuelSprite;
            fuelImage.gameObject.SetActive(fuelVisible);          
        }

        if (fuelText != null)
        {
            if (_debugEnabled)
                Debug.Log("[SystemNodeView2] FuelCount = " + fuelCount);
            fuelText.text = fuelCount;            
            fuelText.gameObject.SetActive(fuelVisible);          
        }        
    }

    private GalaxyMapNodeState GetNodeState(string nodeSystemId, string currentSystemId)
    {
        if (string.Equals(nodeSystemId, currentSystemId, StringComparison.Ordinal))
            return GalaxyMapNodeState.Current;

        if (_travelService.CanTravel(currentSystemId, nodeSystemId))
            return GalaxyMapNodeState.Reachable;

        return GalaxyMapNodeState.Unreachable;
    }    

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick();
    }

    public void OnClick()
    {
        if (IsDebug())
            Debug.Log("SystemNodeView2: _OnClick = " + _onClick);
        _onClick?.Invoke(starSystemConfig.Id);
    }
}