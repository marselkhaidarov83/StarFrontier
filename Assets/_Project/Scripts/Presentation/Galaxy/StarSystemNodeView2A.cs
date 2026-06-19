using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class StarSystemNodeView2A : CustomMonoBehaviour, IPointerClickHandler
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

    [Header("System Tint")]
    [SerializeField] private Color systemColorNormal = Color.white;
    [SerializeField] private Color systemColorCurrent = Color.white;
    [SerializeField] private Color systemColorSelected = Color.white;
    [SerializeField] private Color systemColorDisabled = new Color(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private Color systemColorHidden = new Color(0.35f, 0.35f, 0.45f, 1f);

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int systemImageOrder = 20;
    [SerializeField] private int currentIconOrder = 25;
    [SerializeField] private int titleOrder = 30;
    [SerializeField] private int fuelImageOrder = 31;
    [SerializeField] private int fuelTextOrder = 32;

    [Header("Selection")]
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float selectedScale = 1.12f;

    private IGameSessionService _gameSessionService;
    private ITravelService _travelService;
    private GalaxyRuntimeState _galaxyRuntimeState;

    private Action<string> _onClick;
    private bool _isSelected;

    public string SystemId => starSystemConfig != null ? starSystemConfig.Id : string.Empty;
    public StarSystemConfig Config => starSystemConfig;

    public void Initialize(StarSystemConfig systemConfig, Action<string> onClick)
    {
        starSystemConfig = systemConfig;
        _onClick = onClick;

        _gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _travelService = Bootstrapper.Instance.ServiceRegistry.Get<ITravelService>();

        if (_gameSessionService != null &&
            _gameSessionService.State != null)
        {
            _galaxyRuntimeState = _gameSessionService.State.Galaxy;
        }

        transform.position = new Vector3(
            systemConfig.MapPosition.x,
            systemConfig.MapPosition.y,
            0f
        );

        ApplySorting();
        SetState();
    }

    public void RefreshState()
    {
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

        StarSystemRuntimeState runtimeState = GetRuntimeState();

        bool isDiscovered = runtimeState != null && runtimeState.IsDiscovered;
        bool isCurrent = IsCurrentSystem();

        Sprite systemSprite = GetEconomySprite();
        Color systemColor = systemColorNormal;

        Sprite fuelSprite = fuelNormalSprite;
        Color fuelColor = fuelColorNormal;

        bool fuelVisible = true;
        string fuelCount = string.Empty;

        if (!isDiscovered)
        {
            systemSprite = hiddenSystemSprite != null
                ? hiddenSystemSprite
                : disabledSystemSprite;

            systemColor = systemColorHidden;
            fuelVisible = false;

            if (titleText != null)
                titleText.SetText("???");

            ApplyVisual(
                systemSprite,
                systemColor,
                fuelSprite,
                fuelColor,
                fuelVisible,
                fuelCount,
                isCurrent
            );

            return;
        }

        if (titleText != null)
            titleText.SetText(starSystemConfig.DisplayName);

        TravelFailReason travelFailReason = _travelService.GetTravelFailReason(
            GetCurrentSystemId(),
            starSystemConfig.Id
        );

        LogCustom("travelFailReason = " + starSystemConfig.Id + " / " +travelFailReason);
        switch (travelFailReason)
        {
            case TravelFailReason.TargetSystemIsCurrent:
                systemSprite = GetEconomySprite();
                systemColor = systemColorCurrent;
                fuelColor = fuelColorCurrent;
                fuelVisible = false;
                break;

            case TravelFailReason.None:
                systemSprite = GetEconomySprite();
                systemColor = systemColorNormal;
                fuelSprite = fuelNormalSprite;
                fuelColor = fuelColorNormal;
                fuelVisible = true;
                break;

            case TravelFailReason.NotEnoughFuel:
                systemSprite = GetEconomySprite();
                systemColor = systemColorNormal;
                fuelSprite = fuelNotEnoughSprite;
                fuelColor = fuelColorNotEnough;
                fuelVisible = true;
                break;

            case TravelFailReason.SystemsAreNotNeighbors:
                systemSprite = GetEconomySprite();
                systemColor = systemColorNormal;
                fuelSprite = fuelNotEnoughSprite;
                fuelColor = fuelColorNotEnough;
                fuelVisible = false;
                break;

            default:
                systemSprite = disabledSystemSprite != null
                    ? disabledSystemSprite
                    : GetEconomySprite();

                systemColor = systemColorDisabled;
                fuelColor = fuelColorDisabled;
                fuelVisible = false;
                break;
        }

        if (_isSelected && !isCurrent)
        {
            systemColor = systemColorSelected;
        }

        int travelCost = _travelService.GetTravelCost(
            GetCurrentSystemId(),
            starSystemConfig.Id
        );
        LogCustom("travelCost = " + GetCurrentSystemId() + " / " + starSystemConfig.Id + " / " +travelCost);

        fuelCount = travelCost == 0 ? string.Empty : travelCost.ToString();

        ApplyVisual(
            systemSprite,
            systemColor,
            fuelSprite,
            fuelColor,
            fuelVisible,
            fuelCount,
            isCurrent
        );
    }

    private void ApplyVisual(
        Sprite systemSprite,
        Color systemColor,
        Sprite fuelSprite,
        Color fuelColor,
        bool fuelVisible,
        string fuelCount,
        bool isCurrent)
    {
        if (starSystemImage != null)
        {
            starSystemImage.sprite = systemSprite;
            starSystemImage.color = systemColor;

            // float scale = _isSelected ? selectedScale : normalScale;

            // starSystemImage.transform.localScale = new Vector3(
            //     scale,
            //     scale,
            //     1f
            // );
        }

        if (starSystemCurrentIcon != null)
        {
            starSystemCurrentIcon.gameObject.SetActive(isCurrent);
        }

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

    private Sprite GetEconomySprite()
    {
        if (starSystemConfig == null)
            return balancedSystemSprite;

        switch (starSystemConfig.EconomyType)
        {
            case SystemEconomyType.Mining:
                return miningSystemSprite != null ? miningSystemSprite : balancedSystemSprite;

            case SystemEconomyType.Industrial:
                return industrialSystemSprite != null ? industrialSystemSprite : balancedSystemSprite;

            case SystemEconomyType.Agricultural:
                return agriculturalSystemSprite != null ? agriculturalSystemSprite : balancedSystemSprite;

            case SystemEconomyType.Trade:
                return tradeSystemSprite != null ? tradeSystemSprite : balancedSystemSprite;

            case SystemEconomyType.HighTech:
                return highTechSystemSprite != null ? highTechSystemSprite : balancedSystemSprite;

            case SystemEconomyType.Frontier:
                return frontierSystemSprite != null ? frontierSystemSprite : balancedSystemSprite;

            default:
                return balancedSystemSprite;
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

        if (_gameSessionService != null &&
            _gameSessionService.State != null &&
            _gameSessionService.State.Player != null)
        {
            return _gameSessionService.State.Player.CurrentSystemId;
        }

        return string.Empty;
    }

    private void ApplySorting()
    {
        if (starSystemImage != null)
        {
            starSystemImage.sortingLayerName = sortingLayerName;
            starSystemImage.sortingOrder = systemImageOrder;
        }

        if (starSystemCurrentIcon != null)
        {
            starSystemCurrentIcon.sortingLayerName = sortingLayerName;
            starSystemCurrentIcon.sortingOrder = currentIconOrder;
        }

        if (fuelImage != null)
        {
            fuelImage.sortingLayerName = sortingLayerName;
            fuelImage.sortingOrder = fuelImageOrder;
        }

        if (titleText != null)
        {
            Renderer titleRenderer = titleText.GetComponent<Renderer>();

            if (titleRenderer != null)
            {
                titleRenderer.sortingLayerName = sortingLayerName;
                titleRenderer.sortingOrder = titleOrder;
            }
        }

        if (fuelText != null)
        {
            Renderer fuelTextRenderer = fuelText.GetComponent<Renderer>();

            if (fuelTextRenderer != null)
            {
                fuelTextRenderer.sortingLayerName = sortingLayerName;
                fuelTextRenderer.sortingOrder = fuelTextOrder;
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        LogCustom("");
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