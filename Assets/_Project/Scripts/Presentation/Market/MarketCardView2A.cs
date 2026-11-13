using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MarketCardView2A : MonoBehaviour
{
    private Image _background, _icon, _sizeBadge, _header, _footer, _durabilityFill;
    private TMP_Text _name, _quantity, _price, _size, _placeholder, _range, _damage, _shots, _durability, _cargoInfo;
    private bool _equipment, _showDetails, _hull, _component, _hasCondition;
    private bool HasFooter => _equipment || _hasCondition;
    private Button _button, _detailsButton;
    private Action<string> _select;
    private string _key;
    private Vector2 _lastSize;
    public void Build(TMP_Text template, Action<string> select)
    {
        _select = select;
        _background = gameObject.AddComponent<Image>(); _background.color = MissionPanelStyle2A.CardBackground;
        MissionPanelStyle2A.Frame(_background, MissionPanelStyle2A.CardAccent);
        _button = gameObject.AddComponent<Button>(); _button.targetGraphic = _background;
        _button.onClick.AddListener(() => _select?.Invoke(_key));
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
        _durabilityFill = ResearchPanelUi2A.Picture(_footer.transform, "DurabilityFill", new Color(.12f, .46f, .62f, .85f));
        _price = ResearchPanelUi2A.Text(_footer.transform, template, "UnitPrice", ""); _price.alignment = TextAlignmentOptions.Right;
        _durability = ResearchPanelUi2A.Text(_footer.transform, template, "Durability", "");
        _range = ResearchPanelUi2A.Text(transform, template, "Range", "");
        _damage = ResearchPanelUi2A.Text(transform, template, "Damage", "");
        _shots = ResearchPanelUi2A.Text(transform, template, "ShotCount", "");
        _cargoInfo = ResearchPanelUi2A.Text(transform, template, "CargoInfo", "");
        _sizeBadge = ResearchPanelUi2A.Picture(transform, "CargoSizeBadge", MissionPanelStyle2A.Workspace);
        _size = ResearchPanelUi2A.Text(_sizeBadge.transform, template, "CargoSize", "");
        ResearchPanelUi2A.Stretch(_size.rectTransform); _size.alignment = TextAlignmentOptions.Center;
        foreach (var text in new[] { _name, _quantity, _price, _size, _placeholder, _range, _damage, _shots, _durability, _cargoInfo })
        { text.fontSize = 24; text.fontSizeMax = 24; text.fontSizeMin = 18; }
        _detailsButton = ResearchPanelUi2A.Button(transform, template, "DetailsToggle", "?", out var detailsLabel);
        detailsLabel.fontSizeMax = 24; detailsLabel.fontSizeMin = 18;
        detailsLabel.margin = Vector4.zero;
        _detailsButton.onClick.AddListener(ToggleDetails);
        _detailsButton.gameObject.SetActive(false);
        gameObject.AddComponent<LayoutElement>().preferredHeight = 236;
    }
    public void Bind(MarketCardData2A item, bool selected)
    {
        if (_key != item.Key || _equipment != item.Equipment || _hull != item.Hull || _component != item.Component) _showDetails = false;
        _key = item.Key; _equipment = item.Equipment; _hull = item.Hull; _component = item.Component;
        _hasCondition = item.MaximumDurability > 0;
        GetComponent<LayoutElement>().preferredHeight = HasFooter ? 300 : 236;
        _detailsButton.gameObject.SetActive(true);
        var details = MarketScreenRules2A.WeaponDetails(item.Range, item.Damage, item.ShotCount, item.CargoSize);
        _cargoInfo.SetText(_hull ? string.Empty : details.Cargo);
        _footer.gameObject.SetActive(HasFooter);
        _price.transform.SetParent(HasFooter ? _footer.transform : transform, false);
        _price.alignment = HasFooter ? TextAlignmentOptions.Center : TextAlignmentOptions.Right;
        _durability.SetText($"{item.CurrentDurability} / {item.MaximumDurability}");
        var fill = _durabilityFill.rectTransform;
        float condition = _hasCondition ? Mathf.Clamp01(item.CurrentDurability / (float)item.MaximumDurability) : 0f;
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(condition, 1);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        _range.SetText(item.Hull ? "Прочность: " + item.Damage : details.Range);
        _damage.SetText(details.Damage);
        _shots.SetText(details.Shots);
        _icon.sprite = item.Icon; _icon.enabled = item.Icon != null;
        _placeholder.SetText(item.Icon == null ? (item.Equipment ? "ОБОРУДОВАНИЕ" : "ТОВАР") : "");
        _name.SetText(item.Title);
        _quantity.gameObject.SetActive(!item.Equipment);
        _quantity.SetText(item.Equipment ? "" : item.Quantity + " ед.");
        _price.SetText(item.Locked ? "Задание" : item.Price > 0 ? item.Price.ToString("N0") + " кр." : "—");
        _sizeBadge.gameObject.SetActive(!_hull);
        _size.SetText(_hull ? string.Empty : item.CargoSize.ToString());
        _background.color = selected ? MissionPanelStyle2A.ActionBackground : MissionPanelStyle2A.CardBackground;
        _background.GetComponent<Outline>().effectColor = selected ? Color.white : MissionPanelStyle2A.CardAccent;
        _icon.color = item.Quantity > 0 && !item.Locked ? Color.white : new Color(1, 1, 1, .35f);
        _quantity.color = item.Quantity > 0 && !item.Locked ? Color.white : MissionPanelStyle2A.DisabledAccent;
        UpdateDetailsVisibility();
        Layout();
    }
    private void ToggleDetails()
    {
        _showDetails = !_showDetails;
        UpdateDetailsVisibility();
        AppLog.Info(DebugLogChannel.Market, "[MARKET_CARD_DETAILS] Item=" + _key + " | Details=" + _showDetails);
    }
    private void UpdateDetailsVisibility()
    {
        bool details = _showDetails;
        _icon.gameObject.SetActive(!details);
        _placeholder.gameObject.SetActive(!details && _icon.sprite == null);
        _range.gameObject.SetActive(details && _equipment && !_component);
        _damage.gameObject.SetActive(details && _equipment && !_hull && !_component);
        _shots.gameObject.SetActive(details && _equipment && !_hull && !_component);
        _cargoInfo.gameObject.SetActive(details && !_hull);
        _durability.gameObject.SetActive(details && _hasCondition);
        _durabilityFill.gameObject.SetActive(details && _hasCondition);
    }
    private void LateUpdate()
    { if (_background != null && ((RectTransform)transform).rect.size != _lastSize) Layout(); }
    private void Layout()
    {
        var root = (RectTransform)transform; _lastSize = root.rect.size;
        float w = root.rect.width, h = root.rect.height; if (w <= 0 || h <= 0) return;
        ResearchPanelUi2A.Place(_header.rectTransform, 0, 0, w, 44);
        float badge = Mathf.Min(58, w * .2f);
        // Equal margins keep the title at the card's true center, independent of its volume badge.
        float titleMargin = badge + 8;
        ResearchPanelUi2A.Place(_name.rectTransform, titleMargin, 3, w - 2 * titleMargin, 38);
        ResearchPanelUi2A.Place((RectTransform)_detailsButton.transform, 8, 6, 32, 32);
        ResearchPanelUi2A.Place(_sizeBadge.rectTransform, w - badge - 8, 4, badge, 36);
        float bodyCenter = h * .5f;
        ResearchPanelUi2A.Place(_placeholder.rectTransform, 12, bodyCenter - 24, w - 24, 48);
        ResearchPanelUi2A.Place(_quantity.rectTransform, 12, h - (HasFooter ? 76 : 42), (w - 32) * .42f, 32);
        ResearchPanelUi2A.Place(_footer.rectTransform, 0, h - 44, w, 44);
        float conditionWidth = _hasCondition ? Mathf.Min(94, w * .3f) : 0f;
        ResearchPanelUi2A.Place(_durability.rectTransform, 8, 4, conditionWidth, 36);
        if (HasFooter) ResearchPanelUi2A.Place(_price.rectTransform, conditionWidth + 8, 4, w - 2 * (conditionWidth + 8), 36);
        else
        {
            float priceX = 20 + (w - 32) * .42f;
            ResearchPanelUi2A.Place(_price.rectTransform, priceX, h - 42, w - priceX - 12, 32);
        }
        ResearchPanelUi2A.Place(_range.rectTransform, 12, bodyCenter - 63, w - 24, 30);
        ResearchPanelUi2A.Place(_damage.rectTransform, 12, bodyCenter - 31, w - 24, 30);
        ResearchPanelUi2A.Place(_shots.rectTransform, 12, bodyCenter + 1, w - 24, 30);
        ResearchPanelUi2A.Place(_cargoInfo.rectTransform, 12, _equipment && !_component ? bodyCenter + 33 : bodyCenter - 15, w - 24, 30);
    }
}
