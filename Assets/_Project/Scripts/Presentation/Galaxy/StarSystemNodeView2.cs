using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class StarSystemNodeView2 : CustomMonoBehaviour, IPointerClickHandler
{
    [Header("View")]
    [SerializeField] private SpriteRenderer starSystemImage;
    [SerializeField] private SpriteRenderer starSystemCurrentIcon;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text fuelText;
    [SerializeField] private SpriteRenderer fuelImage;

    [Header("Config")]
    [SerializeField] private StarSystemConfig starSystemConfig;

    [Header("System Visual")]
    [SerializeField] private Sprite balancedSystemSprite;
    [SerializeField] private Sprite miningSystemSprite;
    [SerializeField] private Sprite industrialSystemSprite;
    [SerializeField] private Sprite agriculturalSystemSprite;
    [SerializeField] private Sprite tradeSystemSprite;
    [SerializeField] private Sprite highTechSystemSprite;
    [SerializeField] private Sprite frontierSystemSprite;
    [SerializeField] private Sprite hiddenSystemSprite;
    [SerializeField] private Sprite disabledSystemSprite;

    [Header("Fuel Visual")]
    [SerializeField] private Sprite fuelNormalSprite;
    [SerializeField] private Sprite fuelNotEnoughSprite;

    [SerializeField] private Color fuelColorNormal = Color.white;
    [SerializeField] private Color fuelColorNotEnough = Color.red;
    [SerializeField] private Color fuelColorCurrent = Color.cyan;
    [SerializeField] private Color fuelColorDisabled = Color.gray;

    private IGameSessionService _gameSessionService;
    private ITravelService _travelService;
    private GalaxyRuntimeState _galaxyRuntimeState;

    private Action<string> _onClick;
    private bool _isSelected;

    public void Initialize(StarSystemConfig systemConfig, Action<string> onClick)
    {
        starSystemConfig = systemConfig;
        _onClick = onClick;

        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _travelService = Bootstrapper.Instance.ServiceRegistry.Get<ITravelService>();
        _galaxyRuntimeState = _gameSessionService.State.Galaxy;

        transform.position = new Vector3(
            systemConfig.MapPosition.x,
            systemConfig.MapPosition.y,
            0f
        );

        SetState();
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        SetState();
    }

    public void SetState()
    {
        if (starSystemConfig == null)
            return;

        LogCustom("starSystemConfig = " + starSystemConfig);
        LogCustom("starSystemConfig.EconomyType = " + starSystemConfig.EconomyType);

        StarSystemRuntimeState runtimeState = GetRuntimeState();

        bool isDiscovered = runtimeState != null && runtimeState.IsDiscovered;
        bool isVisited = runtimeState != null && runtimeState.IsVisited;
        bool isCurrent = IsCurrentSystem();

        Sprite systemSprite = disabledSystemSprite;
        Sprite fuelSprite = fuelNormalSprite;
        Color fuelColor = fuelColorNormal;
        bool fuelVisible = true;
        string fuelCount = string.Empty;

        if (!isDiscovered)
        {
            systemSprite = hiddenSystemSprite != null ? hiddenSystemSprite : disabledSystemSprite;
            fuelVisible = false;

            if (titleText != null)
                titleText.SetText("???");

            ApplyVisual(systemSprite, fuelSprite, fuelColor, fuelVisible, fuelCount);
            return;
        }

        if (titleText != null)
            titleText.SetText(starSystemConfig.DisplayName);

        TravelFailReason travelFailReason = _travelService.GetTravelFailReason(
            GetCurrentSystemId(),
            starSystemConfig.Id);

        switch (starSystemConfig.EconomyType)
        {
            case SystemEconomyType.Mining:
                systemSprite = miningSystemSprite;
                break;
            case SystemEconomyType.Industrial:
                systemSprite = industrialSystemSprite;
                break;
            case SystemEconomyType.Agricultural:
                systemSprite = agriculturalSystemSprite;
                break;
            case SystemEconomyType.Trade:
                systemSprite = tradeSystemSprite;
                break;
            case SystemEconomyType.HighTech:
                systemSprite = highTechSystemSprite;
                break;
            case SystemEconomyType.Frontier:
                systemSprite = frontierSystemSprite;
                break;
            default:
                systemSprite = balancedSystemSprite;
                break;
        }

        switch (travelFailReason)
        {
            case TravelFailReason.TargetSystemIsCurrent:
                // systemSprite = currentSystemSprite;
                fuelColor = fuelColorCurrent;
                fuelVisible = false;
                break;

            case TravelFailReason.None:
                // systemSprite = normalSystemSprite;
                fuelSprite = fuelNormalSprite;
                fuelColor = fuelColorNormal;
                fuelVisible = true;
                break;

            case TravelFailReason.NotEnoughFuel:
                // systemSprite = normalSystemSprite;
                fuelSprite = fuelNotEnoughSprite;
                fuelColor = fuelColorNotEnough;
                fuelVisible = true;
                break;

            default:
                // systemSprite = disabledSystemSprite;
                fuelColor = fuelColorDisabled;
                fuelVisible = false;
                break;
        }

        int travelCost = _travelService.GetTravelCost(
            GetCurrentSystemId(),
            starSystemConfig.Id
        );

        fuelCount = travelCost == 0 ? string.Empty : travelCost.ToString();

        ApplyVisual(systemSprite, fuelSprite, fuelColor, fuelVisible, fuelCount);
    }

    private void ApplyVisual(
        Sprite systemSprite,
        Sprite fuelSprite,
        Color fuelColor,
        bool fuelVisible,
        string fuelCount)
    {
        if (starSystemImage != null)
            starSystemImage.sprite = systemSprite;

        if (fuelImage != null)
        {
            fuelImage.sprite = fuelSprite;
            fuelImage.color = fuelColor;
            fuelImage.gameObject.SetActive(fuelVisible);
        }

        if (fuelText != null)
        {
            fuelText.text = fuelCount;
            fuelText.gameObject.SetActive(fuelVisible);
        }
    }

    private StarSystemRuntimeState GetRuntimeState()
    {
        if (_galaxyRuntimeState == null || _galaxyRuntimeState.Systems == null)
            return null;

        return _galaxyRuntimeState.Systems.FirstOrDefault(
            system => system.SystemId == starSystemConfig.Id
        );
    }

    private bool IsCurrentSystem()
    {
        return string.Equals(
            GetCurrentSystemId(),
            starSystemConfig.Id,
            StringComparison.Ordinal
        );
    }

    private string GetCurrentSystemId()
    {
        if (_galaxyRuntimeState != null &&
            !string.IsNullOrWhiteSpace(_galaxyRuntimeState.CurrentSystemId))
        {
            return _galaxyRuntimeState.CurrentSystemId;
        }

        return _gameSessionService.State.Player.CurrentSystemId;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick();
    }

    public void OnClick()
    {
        StarSystemRuntimeState runtimeState = GetRuntimeState();

        if (runtimeState != null && !runtimeState.IsDiscovered)
            return;

        _onClick?.Invoke(starSystemConfig.Id);
    }
}