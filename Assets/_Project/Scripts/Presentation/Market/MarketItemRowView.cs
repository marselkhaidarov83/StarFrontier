using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MarketItemRowView : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text basePriceText;
    [SerializeField] private TMP_Text buyPriceText;
    [SerializeField] private TMP_Text sellPriceText;
    [SerializeField] private TMP_Text ownedQuantityText;

    [Header("Buttons")]
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;

    [Header("Visuals")]
    [SerializeField] private Image itemIcon;

    private string _itemId;
    private System.Action<string> _onBuyClicked;
    private System.Action<string> _onSellClicked;

    private IEconomyService economyService;

    public MarketItemRowView()
    {
        if (Bootstrapper.Instance != null)
        {
            economyService = Bootstrapper.Instance.ServiceRegistry.Get<IEconomyService>();
        }
    }

    public void Bind(
        MarketItemEntry itemData,
        int ownedQuantity,
        int buyPrice,
        int sellPrice,
        System.Action<string> onBuyClicked,
        System.Action<string> onSellClicked,
        bool? canSell = null, bool canBuy = true)
    {
        _itemId = itemData.ItemConfig.Id;
        _onBuyClicked = onBuyClicked;
        _onSellClicked = onSellClicked;

        if (itemNameText != null)
            itemNameText.text = itemData.ItemConfig.DisplayName;

        if (basePriceText != null)
            basePriceText.text = $"Base: {(itemData.BasePrice > 0 ? itemData.BasePrice : itemData.ItemConfig.BasePrice)}";

        if (buyPriceText != null)
            buyPriceText.text = $"Buy: {buyPrice}";

        if (sellPriceText != null)
            sellPriceText.text = $"Sell: {sellPrice}";

        if (ownedQuantityText != null)
            // ownedQuantityText.text = $"Owned: {ownedQuantity}";
            ownedQuantityText.text = "Груз: " + ownedQuantity + " | Рынок: " + itemData.Quantity;

        if (buyButton != null)
        {
            buyButton.interactable = canBuy;
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyClicked);
        }

        if (sellButton != null)
        {
            sellButton.onClick.RemoveAllListeners();
            sellButton.onClick.AddListener(OnSellClicked);
            sellButton.interactable = canSell ?? (ownedQuantity > 0);
        }

        FillIcon(itemData.ItemConfig.Icon);
    }

    public void BindEquipment(SystemNpcWeaponRuntimeState equipment, WeaponConfig config, int price, bool buying,
        System.Action<string> action)
    {
        _itemId = equipment.InstanceId;
        if (itemNameText != null) itemNameText.text = (config != null ? config.DisplayName : equipment.WeaponConfigId) + " · " + equipment.CurrentDurability + " / " + equipment.MaximumDurability;
        if (basePriceText != null) basePriceText.text = "Оборудование";
        if (buyPriceText != null) buyPriceText.text = buying ? "Купить: " + price : "";
        if (sellPriceText != null) sellPriceText.text = buying ? "" : "Продать: " + price;
        if (ownedQuantityText != null) ownedQuantityText.text = buying ? "На рынке" : "В грузовом отсеке";
        _onBuyClicked = buying ? action : null;
        _onSellClicked = buying ? null : action;
        if (buyButton != null) { buyButton.onClick.RemoveAllListeners(); buyButton.onClick.AddListener(OnBuyClicked); buyButton.interactable = buying && price > 0; }
        if (sellButton != null) { sellButton.onClick.RemoveAllListeners(); sellButton.onClick.AddListener(OnSellClicked); sellButton.interactable = !buying && price > 0; }
        FillIcon(config != null ? config.Icon : null);
    }

    private void FillIcon(Sprite iconSprite)
    {
        if (itemIcon == null)
            return;

        itemIcon.sprite = iconSprite;
        itemIcon.enabled = iconSprite != null;

        // if (iconSprite != null)
        // {
        //     itemIcon.preserveAspect = true;
        //     itemIcon.SetNativeSize();
        // }
    }
        
    private void OnBuyClicked()
    {
        if (buyButton != null) buyButton.interactable = false;
        if (sellButton != null) sellButton.interactable = false;
        _onBuyClicked?.Invoke(_itemId);
    }

    private void OnSellClicked()
    {
        if (buyButton != null) buyButton.interactable = false;
        if (sellButton != null) sellButton.interactable = false;
        _onSellClicked?.Invoke(_itemId);
    }
}