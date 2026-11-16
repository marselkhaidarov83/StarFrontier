using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MarketCardView2A : MonoBehaviour
{
    private const float CardBandHeight = 44f;
    private const float GoodsPriceRowHeight = 42f;
    private const float ImageHorizontalPadding = 12f;
    private const float ImageVerticalPadding = 8f;

    private Image _background, _icon, _sizeBadge, _header, _footer;
    private TMP_Text _name, _quantity, _price, _size, _placeholder;
    private bool _equipment, _hasCondition;
    private bool HasFooter => _equipment || _hasCondition;
    private MarketCardData2A _item;
    private bool _selected;
    private Vector2 _lastSize;
    public void Build(TMP_Text template, Action<string> select, Action<MarketCardData2A> showInfo)
    {
        _background = gameObject.AddComponent<Image>(); _background.color = MissionPanelStyle2A.CardBackground;
        MissionPanelStyle2A.Frame(_background, MissionPanelStyle2A.CardAccent);
        var button = gameObject.AddComponent<Button>(); button.targetGraphic = _background;
        button.onClick.AddListener(() =>
        {
            if (_item == null) return;
            if (_selected) showInfo?.Invoke(_item);
            else select?.Invoke(_item.Key);
        });
        _header = ResearchPanelUi2A.Picture(transform, "CardHeader", new Color(
            MissionPanelStyle2A.CardBackground.r * .6f, MissionPanelStyle2A.CardBackground.g * .6f,
            MissionPanelStyle2A.CardBackground.b * .6f, .98f));
        _icon = ResearchPanelUi2A.Picture(transform, "ItemImage", Color.white);
        _icon.preserveAspect = true;
        ResearchPanelUi2A.Stretch(_icon.rectTransform);
        _icon.transform.SetAsFirstSibling();
        _placeholder = ResearchPanelUi2A.Text(transform, template, "Placeholder", ""); _placeholder.alignment = TextAlignmentOptions.Center;
        _placeholder.color = MissionPanelStyle2A.DisabledAccent;
        _name = ResearchPanelUi2A.Text(transform, template, "Name", ""); _name.alignment = TextAlignmentOptions.Center;
        _name.maxVisibleLines = 2; _name.overflowMode = TextOverflowModes.Ellipsis;
        _quantity = ResearchPanelUi2A.Text(transform, template, "Quantity", "");
        _footer = ResearchPanelUi2A.Picture(transform, "CardFooter", _header.color);
        _price = ResearchPanelUi2A.Text(_footer.transform, template, "UnitPrice", "");
        _sizeBadge = ResearchPanelUi2A.Picture(transform, "CargoSizeBadge", MissionPanelStyle2A.Workspace);
        _size = ResearchPanelUi2A.Text(_sizeBadge.transform, template, "CargoSize", "");
        ResearchPanelUi2A.Stretch(_size.rectTransform); _size.alignment = TextAlignmentOptions.Center;
        foreach (var text in new[] { _name, _quantity, _price, _size, _placeholder })
        { text.fontSize = 24; text.fontSizeMax = 24; text.fontSizeMin = 18; }
        gameObject.AddComponent<LayoutElement>().preferredHeight = 236;
    }
    public void Bind(MarketCardData2A item, bool selected)
    {
        _item = item;
        _selected = selected;
        _equipment = item.Equipment || item.Component;
        _hasCondition = item.MaximumDurability > 0;
        GetComponent<LayoutElement>().preferredHeight = HasFooter ? 300 : 236;
        float bottomBandHeight = HasFooter ? CardBandHeight : GoodsPriceRowHeight;
        _icon.rectTransform.offsetMin = new Vector2(ImageHorizontalPadding, bottomBandHeight + ImageVerticalPadding);
        _icon.rectTransform.offsetMax = new Vector2(-ImageHorizontalPadding, -CardBandHeight - ImageVerticalPadding);
        _footer.gameObject.SetActive(HasFooter);
        _price.transform.SetParent(HasFooter ? _footer.transform : transform, false);
        _price.alignment = HasFooter ? TextAlignmentOptions.Center : TextAlignmentOptions.Right;
        _icon.sprite = item.Icon; _icon.enabled = item.Icon != null;
        _placeholder.gameObject.SetActive(item.Icon == null);
        _placeholder.SetText(item.Icon == null ? (_equipment ? "ОБОРУДОВАНИЕ" : "ТОВАР") : "");
        _name.SetText(item.Title);
        _quantity.gameObject.SetActive(!_equipment);
        _quantity.SetText(_equipment ? "" : item.Quantity + " ед.");
        _price.SetText(item.Locked ? "Задание" : item.Price > 0 ? item.Price.ToString("N0") + " кр." : "—");
        _sizeBadge.gameObject.SetActive(!item.Hull);
        _size.SetText(item.Hull ? string.Empty : item.CargoSize.ToString());
        _background.color = selected ? MissionPanelStyle2A.ActionBackground : MissionPanelStyle2A.CardBackground;
        _background.GetComponent<Outline>().effectColor = selected ? Color.white : MissionPanelStyle2A.CardAccent;
        _icon.color = item.Quantity > 0 && !item.Locked ? Color.white : new Color(1, 1, 1, .35f);
        _quantity.color = item.Quantity > 0 && !item.Locked ? Color.white : MissionPanelStyle2A.DisabledAccent;
        Layout();
    }
    private void LateUpdate()
    { if (_background != null && ((RectTransform)transform).rect.size != _lastSize) Layout(); }
    private void Layout()
    {
        var root = (RectTransform)transform; _lastSize = root.rect.size;
        float w = root.rect.width, h = root.rect.height; if (w <= 0 || h <= 0) return;
        ResearchPanelUi2A.Place(_header.rectTransform, 0, 0, w, CardBandHeight);
        float badge = Mathf.Min(58, w * .2f);
        // Equal margins keep the title at the card's true center, independent of its volume badge.
        float titleMargin = badge + 8;
        ResearchPanelUi2A.Place(_name.rectTransform, titleMargin, 3, w - 2 * titleMargin, 38);
        ResearchPanelUi2A.Place(_sizeBadge.rectTransform, w - badge - 8, 4, badge, 36);
        float bodyCenter = h * .5f;
        ResearchPanelUi2A.Place(_placeholder.rectTransform, 12, bodyCenter - 24, w - 24, 48);
        ResearchPanelUi2A.Place(_quantity.rectTransform, 12, h - (HasFooter ? 76 : GoodsPriceRowHeight), (w - 32) * .42f, 32);
        ResearchPanelUi2A.Place(_footer.rectTransform, 0, h - CardBandHeight, w, CardBandHeight);
        if (HasFooter) ResearchPanelUi2A.Place(_price.rectTransform, 8, 4, w - 16, 36);
        else
        {
            float priceX = 20 + (w - 32) * .42f;
            ResearchPanelUi2A.Place(_price.rectTransform, priceX, h - GoodsPriceRowHeight, w - priceX - 12, 32);
        }
    }
}
