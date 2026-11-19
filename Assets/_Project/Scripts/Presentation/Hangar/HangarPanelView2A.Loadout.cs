using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class HangarPanelView2A
{
    private sealed class LoadoutCard
    {
        public Button Button;
        public TMP_Text Title;
        public Image Icon;
        public bool SelectedByClick, Inventory;
        public string InfoKey, Info, EquipmentName;
        public (int Current, int Maximum) Durability;
        public int Level;
        public Color LevelColor;
    }

    private const float CardTitleSize = 25;
    private static readonly Color EmptySlotBackground = new Color(.35f, .26f, .07f);
    private static readonly Color SelectedEmptySlotBackground = new Color(.52f, .39f, .10f);
    private static readonly Color EmptySlotAccent = new Color(1f, .81f, .23f);

    private const float ChoiceCardHeight = 162;
    private const float InventoryInset = 16;
    private const float InventoryButtonHeight = 64;
    private const float InventoryListTop = InventoryInset + InventoryButtonHeight + 8;
    private const float InventoryActionGap = 8;
    private const float StackedInventoryHeight = InventoryListTop + ChoiceCardHeight + 8 +
        InventoryActionGap + InventoryButtonHeight + InventoryInset;
    private const float ShipSummaryHeight = 78;
    private const float ModuleTop = 170;
    private const float HullHeight = 290;
    private const float ShieldTop = ModuleTop + HullHeight + 12;
    private const float MinDiagramHeight = ShieldTop + 150;

    private readonly List<LoadoutCard> _slots = new List<LoadoutCard>();
    private readonly List<LoadoutCard> _choices = new List<LoadoutCard>();
    private RectTransform _diagram, _weaponGroup, _choiceContent;
    private ScrollRect _loadoutScroll, _choicesScroll;
    private float _choiceCardWidth;
    private TMP_Text _loadoutTemplate, _summary, _status, _emptyChoices, _equipLabel;
    private TMP_Text _repairLabel, _fullRepairLabel, _inventoryRepairLabel, _storageLabel;
    private Button _cargoTab, _warehouseTab, _equip, _remove, _removeAll, _repair, _fullRepair, _inventoryRepair;
    private Action<int> _selectChoice;

    public void BuildLoadout(TMP_Text template, Button repair, Action<int> selectSlot, Action<bool> selectStorage,
        Action<int> selectChoice, Action equip, Action remove,
        Action repairInstalled = null, Action repairFull = null, Action repairInventory = null, Action removeAll = null, bool stacked = false)
    {
        if (_screen != null) return;
        var legacy = new List<Transform>();
        foreach (Transform child in transform) legacy.Add(child);
        _stacked = stacked;
        BuildFrame();
        _loadoutTemplate = template;
        _selectChoice = selectChoice;

        _loadoutScroll = ResearchPanelUi2A.Scroll(_left, "ShipLoadout", false, out var loadout);
        PrepareContent(loadout);
        loadout.GetComponent<VerticalLayoutGroup>().padding.bottom = 0;
        _summary = ResearchPanelUi2A.Text(loadout, template, "ShipStats", string.Empty);
        Move(_summary, loadout, ShipSummaryHeight);
        _diagram = ResearchPanelUi2A.Rect(loadout, "ShipSlots");
        Move(_diagram, loadout, MinDiagramHeight);
        _weaponGroup = Panel(_diagram, "Weapons");

        for (int i = 0; i < ShipLoadoutRules.TotalSlots; i++)
        {
            int index = i;
            Transform parent = i < ShipLoadoutRules.FirstWeaponSlot ? _diagram : _weaponGroup;
            _slots.Add(CreateCard(parent, "Slot_" + index, () => selectSlot(index)));
        }

        var footer = ResearchPanelUi2A.Rect(_left, "ShipActions");
        _repair = repair;
        if (_repair != null)
        {
            Move(_repair, footer, 64);
            _repairLabel = _repair.GetComponentInChildren<TMP_Text>(true);
        }
        if (_repairLabel == null)
        {
            if (_repair != null) _repair.gameObject.SetActive(false);
            _repair = ResearchPanelUi2A.Button(footer, template, "RepairSelected", "Ремонт 0 кр", out _repairLabel);
        }
        _fullRepair = ResearchPanelUi2A.Button(footer, template, "RepairFull", "Ремонт полный 0 кр", out _fullRepairLabel);
        _remove = ResearchPanelUi2A.Button(footer, template, "Remove", "Снять", out _);
        _repair.onClick.AddListener(() => repairInstalled?.Invoke());
        _fullRepair.onClick.AddListener(() => repairFull?.Invoke());
        _remove.onClick.AddListener(() => remove());
        _removeAll = ResearchPanelUi2A.Button(footer, template, "RemoveAll", "Снять всё", out _);
        _removeAll.onClick.AddListener(() => removeAll?.Invoke());
        SetButton(_removeAll, false, false);
        _status = ResearchPanelUi2A.Text(_left, template, "HangarStatus", string.Empty);
        _status.fontSizeMin = 16;
        _status.gameObject.SetActive(false);

        _cargoTab = ResearchPanelUi2A.Button(_right, template, "Hold", "Трюм", out _);
        _warehouseTab = ResearchPanelUi2A.Button(_right, template, "Warehouse", "Станция", out _storageLabel);
        _cargoTab.onClick.AddListener(() => selectStorage(false));
        _warehouseTab.onClick.AddListener(() => selectStorage(true));
        _choicesScroll = ResearchPanelUi2A.Scroll(_right, "AvailableEquipment", _stacked, out _choiceContent);
        _emptyChoices = ResearchPanelUi2A.Text(_choiceContent, template, "Empty", "Нет подходящего оборудования");
        Move(_emptyChoices, _choiceContent, _stacked ? ChoiceCardHeight : 76);
        _equip = ResearchPanelUi2A.Button(_right, template, "Install", "Установить", out _equipLabel);
        _inventoryRepair = ResearchPanelUi2A.Button(_right, template, "RepairInventory", "Отремонтировать", out _inventoryRepairLabel);
        _equip.onClick.AddListener(() => equip());
        _inventoryRepair.onClick.AddListener(() => repairInventory?.Invoke());

        BuildEquipmentModal(equip, remove, repairInstalled, repairInventory);

        foreach (var child in legacy)
            if (child != null && child.parent == transform) child.gameObject.SetActive(false);
        RefreshLayout();
    }

    private LoadoutCard CreateCard(Transform parent, string name, Action selected, bool inventory = false)
    {
        var button = ResearchPanelUi2A.Button(parent, _loadoutTemplate, name, string.Empty, out var title);
        title.enableAutoSizing = false;
        title.fontSize = CardTitleSize;
        title.alignment = TextAlignmentOptions.Center;
        title.textWrappingMode = TextWrappingModes.Normal;
        title.overflowMode = TextOverflowModes.Ellipsis;
        var icon = ResearchPanelUi2A.Picture(button.transform, "Icon", Color.white);
        icon.preserveAspect = true;
        ResearchPanelUi2A.Stretch(icon.rectTransform);
        icon.transform.SetAsFirstSibling();
        var card = new LoadoutCard { Button = button, Title = title, Icon = icon, Inventory = inventory };
        button.onClick.AddListener(() =>
        {
            if (!button.interactable || _modalCard != null) return;
            if (!card.SelectedByClick)
            {
                ResetCardSelections();
                card.SelectedByClick = true;
                selected();
                return;
            }
            if (!string.IsNullOrEmpty(card.Info)) OpenEquipmentModal(card);
        });
        return card;
    }

    public void SetSlot(int index, string title, Sprite icon, bool available, bool selected,
        string info = null, string infoKey = null, bool empty = false, string equipmentName = null, (int Current, int Maximum) durability = default, int level = 0, Color? levelColor = null)
    {
        var card = _slots[index];
        SetCard(card, title, icon, available, selected, info, infoKey, equipmentName, durability, level, levelColor ?? Color.white);
        if (available && empty)
        {
            var background = (Image)card.Button.targetGraphic;
            background.color = selected ? SelectedEmptySlotBackground : EmptySlotBackground;
            MissionPanelStyle2A.Frame(background, EmptySlotAccent);
        }
    }

    public void SetInventory(bool warehouse, string storageLabel, int count, bool ready)
    {
        _storageLabel.SetText(storageLabel);
        SetButton(_cargoTab, true, !warehouse);
        SetButton(_warehouseTab, ready, warehouse);
        _emptyChoices.SetText(ready ? "Нет подходящего оборудования" : "Ангар недоступен");
        _emptyChoices.gameObject.SetActive(count == 0);
        for (int i = count; i < _choices.Count; i++)
        {
            if (_modalCard == _choices[i]) CloseEquipmentModal();
            _choices[i].SelectedByClick = false;
            _choices[i].Button.gameObject.SetActive(false);
        }
    }

    public void SetChoice(int index, string title, Sprite icon, bool selected, string info, string infoKey, (int Current, int Maximum) durability = default, int level = 0, Color? levelColor = null)
    {
        if (_choices.Count == index)
        {
            var choice = CreateCard(_choiceContent, "Item_" + index, () => _selectChoice(index), inventory: true);
            Move(choice.Button, _choiceContent, ChoiceCardHeight);
            _choices.Add(choice);
        }
        var card = _choices[index];
        card.Button.gameObject.SetActive(true);
        SetCard(card, string.Empty, icon, true, selected, info, infoKey, title, durability, level, levelColor ?? Color.white);
        LayoutChoiceCard(card);
    }

    public void SetLoadoutActions(bool occupied, bool canEquip, bool canRemove, string summary, string status,
        long repairPrice, long fullRepairPrice, long inventoryRepairPrice, int credits, bool canRemoveAll)
    {
        _summary.SetText(summary);
        _status.SetText(status);
        bool hasStatus = !string.IsNullOrEmpty(status);
        if (_status.gameObject.activeSelf != hasStatus)
        {
            _status.gameObject.SetActive(hasStatus);
            RefreshLayout();
        }
        _equipLabel.SetText(occupied ? "Заменить" : "Установить");
        _repairLabel.SetText("Ремонт " + repairPrice.ToString("N0") + " кр");
        _fullRepairLabel.SetText("Ремонт полный " + fullRepairPrice.ToString("N0") + " кр");
        _inventoryRepairLabel.SetText(inventoryRepairPrice > 0
            ? "Отремонтировать · " + inventoryRepairPrice.ToString("N0") + " кр" : "Отремонтировать");
        SetButton(_repair, repairPrice > 0 && repairPrice <= credits, false);
        SetButton(_fullRepair, fullRepairPrice > 0 && fullRepairPrice <= credits, false);
        SetButton(_inventoryRepair, inventoryRepairPrice > 0 && inventoryRepairPrice <= credits, false);
        SetButton(_equip, canEquip, false);
        SetButton(_remove, canRemove, false);
        SetButton(_removeAll, canRemoveAll, false);
        UpdateEquipmentModalActions(repairPrice, inventoryRepairPrice, status);
    }

    public void ResetLoadoutInventoryScroll()
    {
        _choicesScroll.StopMovement();
        if (_stacked) _choicesScroll.horizontalNormalizedPosition = 0;
        else _choicesScroll.verticalNormalizedPosition = 1;
    }

    private void SetCard(LoadoutCard card, string title, Sprite icon, bool available, bool selected,
        string info, string infoKey, string equipmentName, (int Current, int Maximum) durability, int level, Color levelColor)
    {
        if (!available || !selected || card.InfoKey != infoKey)
        {
            card.SelectedByClick = false;
            if (_modalCard == card) CloseEquipmentModal();
        }
        card.InfoKey = infoKey;
        card.Info = info;
        card.Durability = durability;
        card.Level = level; card.LevelColor = levelColor;
        card.EquipmentName = equipmentName ?? title;
        card.Title.SetText(title);
        card.Title.gameObject.SetActive(!string.IsNullOrEmpty(title));
        card.Icon.sprite = icon;
        card.Icon.enabled = icon != null;
        SetButton(card.Button, available, selected);
        if (available && level > 0)
            ((Image)card.Button.targetGraphic).color = Color.Lerp(
                MissionPanelStyle2A.CardBackground, levelColor, selected ? .38f : .22f);
        if (_modalCard == card) RefreshEquipmentModal();
    }

    public void ResetCardSelections()
    {
        CloseEquipmentModal();
        foreach (var card in _slots) card.SelectedByClick = false;
        foreach (var card in _choices) card.SelectedByClick = false;
    }

    private LoadoutCard _modalCard;
    private Image _equipmentOverlay, _equipmentPicture, _durabilityTrack, _durabilityFill;
    private RectTransform _equipmentWindow;
    private ScrollRect _equipmentScroll;
    private TMP_Text _equipmentTitle, _equipmentInfo, _equipmentStatus, _modalRepairLabel, _modalEquipmentActionLabel, _durabilityText;
    private Button _modalClose, _modalEquipmentAction, _modalRepair;
    private long _selectedRepairPrice, _storedRepairPrice;
    private string _actionStatus;

    private void BuildEquipmentModal(Action equip, Action remove, Action repairInstalled, Action repairInventory)
    {
        _equipmentOverlay = ResearchPanelUi2A.Picture(_screen, "EquipmentModal", new Color(0, 0, 0, .75f));
        ResearchPanelUi2A.Stretch(_equipmentOverlay.rectTransform);
        _equipmentOverlay.raycastTarget = true;
        var canvas = _equipmentOverlay.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;
        _equipmentOverlay.gameObject.AddComponent<GraphicRaycaster>();
        var backdrop = ResearchPanelUi2A.Picture(_equipmentOverlay.transform, "CloseBackdrop", Color.clear);
        ResearchPanelUi2A.Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = true;
        var closeBackdrop = backdrop.gameObject.AddComponent<Button>();
        closeBackdrop.transition = Selectable.Transition.None;
        closeBackdrop.navigation = new Navigation { mode = Navigation.Mode.None };
        closeBackdrop.onClick.AddListener(CloseEquipmentModal);

        _equipmentWindow = Panel(_equipmentOverlay.transform, "EquipmentWindow");
        _equipmentWindow.GetComponent<Image>().raycastTarget = true;
        var heading = Panel(_equipmentWindow, "Heading");
        _equipmentTitle = ResearchPanelUi2A.Text(heading, _loadoutTemplate, "Title", string.Empty);
        _equipmentTitle.alignment = TextAlignmentOptions.Center;
        _equipmentTitle.textWrappingMode = TextWrappingModes.Normal;

        _equipmentScroll = ResearchPanelUi2A.Scroll(_equipmentWindow, "EquipmentDetails", false, out var content);
        PrepareContent(content);
        _equipmentPicture = ResearchPanelUi2A.Picture(content, "Image", Color.white);
        _equipmentPicture.preserveAspect = true;
        Move(_equipmentPicture, content, 280);
        _equipmentInfo = ResearchPanelUi2A.Text(content, _loadoutTemplate, "EquipmentInfo", string.Empty);
        _equipmentStatus = ResearchPanelUi2A.Text(content, _loadoutTemplate, "Status", string.Empty);
        foreach (var text in new[] { _equipmentInfo, _equipmentStatus })
        {
            Move(text, content, 42);
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
        }

        _durabilityTrack = ResearchPanelUi2A.Picture(_equipmentWindow, "DurabilityTrack", MissionPanelStyle2A.Workspace);
        MissionPanelStyle2A.Frame(_durabilityTrack, MissionPanelStyle2A.CardAccent);
        _durabilityFill = ResearchPanelUi2A.Picture(_durabilityTrack.transform, "Fill", MissionPanelStyle2A.ActionBackground);
        _durabilityText = ResearchPanelUi2A.Text(_durabilityTrack.transform, _loadoutTemplate, "Durability", string.Empty);
        ResearchPanelUi2A.Stretch(_durabilityText.rectTransform);
        _durabilityText.alignment = TextAlignmentOptions.Center;

        _modalClose = ResearchPanelUi2A.Button(_equipmentWindow, _loadoutTemplate, "Close", "×", out _);
        _modalEquipmentAction = ResearchPanelUi2A.Button(_equipmentWindow, _loadoutTemplate, "EquipmentAction", "Снять", out _modalEquipmentActionLabel);
        _modalRepair = ResearchPanelUi2A.Button(_equipmentWindow, _loadoutTemplate, "RepairEquipment", "Ремонт", out _modalRepairLabel);
        foreach (var button in new[] { _modalClose, _modalEquipmentAction, _modalRepair })
            button.navigation = new Navigation { mode = Navigation.Mode.None };
        _modalClose.onClick.AddListener(CloseEquipmentModal);
        _modalEquipmentAction.onClick.AddListener(() =>
        {
            if (_modalCard == null || !_modalEquipmentAction.interactable) return;
            if (_modalCard.Inventory) equip?.Invoke();
            else remove?.Invoke();
        });
        _modalRepair.onClick.AddListener(() =>
        {
            if (_modalCard == null || !_modalRepair.interactable) return;
            if (_modalCard.Inventory) repairInventory?.Invoke();
            else repairInstalled?.Invoke();
        });
        _equipmentOverlay.gameObject.SetActive(false);
    }

    private void OpenEquipmentModal(LoadoutCard card)
    {
        _modalCard = card;
        _equipmentOverlay.gameObject.SetActive(true);
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(_modalClose.gameObject);
        RefreshEquipmentModal();
        _equipmentScroll.StopMovement();
        _equipmentScroll.verticalNormalizedPosition = 1;
    }

    public void CloseEquipmentModal()
    {
        if (_modalCard == null) return;
        _modalCard = null;
        _equipmentOverlay.gameObject.SetActive(false);
        if (UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject == _modalClose.gameObject)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    }

    private void RefreshEquipmentModal()
    {
        _equipmentPicture.sprite = _modalCard.Icon.sprite;
        _equipmentPicture.gameObject.SetActive(_equipmentPicture.sprite != null);
        _equipmentTitle.SetText(_modalCard.EquipmentName);
        _equipmentTitle.color = Color.white;
        _equipmentInfo.SetText(_modalCard.Info);
        var (current, maximum) = _modalCard.Durability;
        _durabilityTrack.gameObject.SetActive(maximum > 0);
        _durabilityText.SetText($"Прочность: {current} / {maximum}");
        var fill = _durabilityFill.rectTransform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f, 1);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        UpdateEquipmentModalActions(_selectedRepairPrice, _storedRepairPrice, _actionStatus);
    }

    private void UpdateEquipmentModalActions(long installedPrice, long inventoryPrice, string status)
    {
        _selectedRepairPrice = installedPrice;
        _storedRepairPrice = inventoryPrice;
        _actionStatus = status;
        if (_modalCard == null) return;
        long price = _modalCard.Inventory ? inventoryPrice : installedPrice;
        bool inventory = _modalCard.Inventory;
        _modalEquipmentActionLabel.SetText(inventory ? _equipLabel.text : "Снять");
        SetButton(_modalEquipmentAction, (inventory ? _equip : _remove).interactable, false);
        SetButton(_modalRepair, (_modalCard.Inventory ? _inventoryRepair : _repair).interactable, false);
        _modalRepairLabel.SetText(price > 0 ? $"Ремонт {price:N0} кр" : "Ремонт");
        _equipmentStatus.SetText(status);
        _equipmentStatus.gameObject.SetActive(!string.IsNullOrEmpty(status));
        LayoutEquipmentModal();
    }

    private void LayoutEquipmentModal()
    {
        float width = _screen.rect.width * .94f;
        float maxHeight = _screen.rect.height * .9f;
        const float inset = 16;
        const float durabilityHeight = 46;
        const float gap = 12;
        float barSpace = _durabilityTrack.gameObject.activeSelf ? durabilityHeight + gap : 0;
        float footer = 92 + barSpace;
        const float header = 64;
        var content = _equipmentScroll.content;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        float textWidth = Mathf.Max(1, width - layout.padding.horizontal);
        foreach (var text in new[] { _equipmentInfo, _equipmentStatus })
        {
            float height = Mathf.Ceil(text.GetPreferredValues(text.text, textWidth, 0).y) + 8;
            var size = text.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = height;
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        float heightAvailable = Mathf.Max(0, maxHeight - header - footer);
        float body = Mathf.Min(LayoutUtility.GetPreferredHeight(content), heightAvailable);
        float windowHeight = header + body + footer;
        ResearchPanelUi2A.Place(_equipmentWindow, (_screen.rect.width - width) / 2,
            (_screen.rect.height - windowHeight) / 2, width, windowHeight);
        ResearchPanelUi2A.Place((RectTransform)_equipmentTitle.transform.parent, 0, 0, width, header);
        ResearchPanelUi2A.Place(_equipmentTitle.rectTransform, 74, 0, width - 148, header);
        ResearchPanelUi2A.Place((RectTransform)_equipmentScroll.transform, 0, header, width, body);
        ResearchPanelUi2A.Place(_durabilityTrack.rectTransform, inset, header + body, width - 2 * inset, durabilityHeight);
        float buttonsTop = header + body + barSpace + gap;
        ResearchPanelUi2A.Place((RectTransform)_modalClose.transform, width - 68, 10, 56, 48);
        float buttonWidth = (width - 48) / 2;
        ResearchPanelUi2A.Place((RectTransform)_modalEquipmentAction.transform, 16, buttonsTop, buttonWidth, 64);
        ResearchPanelUi2A.Place((RectTransform)_modalRepair.transform, 32 + buttonWidth, buttonsTop, buttonWidth, 64);
    }

    private static void SetButton(Button button, bool available, bool selected)
    {
        button.interactable = available;
        MissionPanelStyle2A.Action(button, available);
        if (selected && available && button.targetGraphic is Image image)
            image.color = MissionPanelStyle2A.CardAccent;
    }

    private void LayoutShip(float left, float height)
    {
        const float footerHeight = 152;
        const float statusHeight = 60;
        const float footerGap = 10;
        float footerTop = Mathf.Max(0, height - footerHeight);
        float statusSpace = _status.gameObject.activeSelf ? statusHeight + footerGap : 0;
        float loadoutHeight = Mathf.Max(0, footerTop - footerGap - statusSpace);
        ResearchPanelUi2A.Place((RectTransform)_loadoutScroll.transform, 0, 0, left, loadoutHeight);

        var contentLayout = _loadoutScroll.content.GetComponent<VerticalLayoutGroup>();
        float diagramHeight = Mathf.Max(MinDiagramHeight,
            loadoutHeight - contentLayout.padding.vertical - ShipSummaryHeight - contentLayout.spacing);
        var diagramSize = _diagram.GetComponent<LayoutElement>();
        diagramSize.minHeight = diagramSize.preferredHeight = diagramHeight;
        float diagramWidth = Mathf.Max(0, left - 44);
        float side = diagramWidth * .24f;
        float center = Mathf.Max(0, diagramWidth - 2 * side - 24);
        float centerX = side + 12;
        const float weaponPadding = 8;
        float weaponGroupHeight = _stacked ? diagramHeight * .25f : ModuleTop - 14;
        float moduleTop = weaponGroupHeight + 14;
        const int columnSlots = 3;
        const float cardGap = 10;
        float cardHeight = (diagramHeight - moduleTop - (columnSlots - 1) * cardGap) / columnSlots;
        float hullHeight = _stacked ? 2 * cardHeight + cardGap : HullHeight;
        float shieldTop = _stacked ? moduleTop + hullHeight + cardGap : ShieldTop;
        LayoutCard(_slots[ShipLoadoutRules.HullSlot], centerX, moduleTop, center, hullHeight);
        for (int i = 0; i < columnSlots; i++)
        {
            float y = moduleTop + i * (cardHeight + cardGap);
            LayoutCard(_slots[ShipLoadoutRules.FirstComponentSlot + i], 0, y, side, cardHeight);
            LayoutCard(_slots[ShipLoadoutRules.FirstComponentSlot + i + columnSlots], diagramWidth - side, y, side, cardHeight);
        }
        LayoutCard(_slots[ShipLoadoutRules.FirstComponentSlot + (int)ShipComponentSlot.ShieldGenerator],
            centerX, shieldTop, center, diagramHeight - shieldTop);

        ResearchPanelUi2A.Place(_weaponGroup, 0, 0, diagramWidth, weaponGroupHeight);
        float weaponWidth = Mathf.Max(0, (diagramWidth - (ShipLoadoutRules.WeaponSlots + 1) * weaponPadding) / ShipLoadoutRules.WeaponSlots);
        for (int i = 0; i < ShipLoadoutRules.WeaponSlots; i++)
            LayoutCard(_slots[ShipLoadoutRules.FirstWeaponSlot + i],
                weaponPadding + i * (weaponWidth + weaponPadding), weaponPadding, weaponWidth, weaponGroupHeight - 2 * weaponPadding);

        var footer = (RectTransform)_repair.transform.parent;
        float repairWidth = Mathf.Max(0, (left - 44) * .5f);
        ResearchPanelUi2A.Place(footer, 0, footerTop, left, footerHeight);
        ResearchPanelUi2A.Place((RectTransform)_remove.transform, 16, 0, repairWidth, 64);
        ResearchPanelUi2A.Place((RectTransform)_removeAll.transform, 28 + repairWidth, 0, repairWidth, 64);
        ResearchPanelUi2A.Place((RectTransform)_repair.transform, 16, 74, repairWidth, 64);
        ResearchPanelUi2A.Place((RectTransform)_fullRepair.transform, 28 + repairWidth, 74, repairWidth, 64);
        ResearchPanelUi2A.Place(_status.rectTransform, 16, Mathf.Max(0, footerTop - footerGap - statusHeight), left - 32, statusHeight);
    }

    private void LayoutLegacyInventory(float right, float height)
    {
        float inner = Mathf.Max(0, right - 32);
        ResearchPanelUi2A.Place((RectTransform)_cargoTab.transform, 16, 16, inner, 64);
        ResearchPanelUi2A.Place((RectTransform)_warehouseTab.transform, 16, 96, inner, 64);
        ResearchPanelUi2A.Place((RectTransform)_choicesScroll.transform, 16, 168, inner, Mathf.Max(0, height - 320));
        ResearchPanelUi2A.Place((RectTransform)_inventoryRepair.transform, 16, Mathf.Max(168, height - 146), inner, 60);
        ResearchPanelUi2A.Place((RectTransform)_equip.transform, 16, Mathf.Max(238, height - 76), inner, 62);
        _choiceCardWidth = Mathf.Max(0, inner - 6);
        foreach (var card in _choices) LayoutChoiceCard(card);
    }

    private void LayoutStackedInventory(float width, float height)
    {
        const float buttonGap = 12;
        float inner = Mathf.Max(0, width - 2 * InventoryInset);
        float buttonWidth = Mathf.Max(0, (inner - buttonGap) * .5f);
        float secondColumn = InventoryInset + buttonWidth + buttonGap;
        ResearchPanelUi2A.Place((RectTransform)_cargoTab.transform, InventoryInset, InventoryInset, buttonWidth, InventoryButtonHeight);
        ResearchPanelUi2A.Place((RectTransform)_warehouseTab.transform, secondColumn, InventoryInset, buttonWidth, InventoryButtonHeight);

        float actionsTop = Mathf.Max(0, height - InventoryButtonHeight - InventoryInset);
        float rowHeight = Mathf.Max(0, actionsTop - InventoryListTop - InventoryActionGap);
        ResearchPanelUi2A.Place((RectTransform)_choicesScroll.transform, InventoryInset, InventoryListTop, inner, rowHeight);
        ResearchPanelUi2A.Place((RectTransform)_inventoryRepair.transform, InventoryInset, actionsTop, buttonWidth, InventoryButtonHeight);
        ResearchPanelUi2A.Place((RectTransform)_equip.transform, secondColumn, actionsTop, buttonWidth, InventoryButtonHeight);

        var row = _choiceContent.GetComponent<HorizontalLayoutGroup>();
        _choiceContent.sizeDelta = new Vector2(_choiceContent.sizeDelta.x, rowHeight);
        float rowWidth = Mathf.Max(0, inner - row.padding.horizontal);
        int visibleCards = Mathf.Clamp(Mathf.FloorToInt((rowWidth + row.spacing) / (220 + row.spacing)), 1, 4);
        _choiceCardWidth = Mathf.Max(0, (rowWidth - (visibleCards - 1) * row.spacing) / visibleCards);
        var emptySize = _emptyChoices.GetComponent<LayoutElement>();
        emptySize.minWidth = emptySize.preferredWidth = rowWidth;
        foreach (var card in _choices) LayoutChoiceCard(card);
    }

    private void LayoutChoiceCard(LoadoutCard card)
    {
        if (_stacked)
        {
            var size = card.Button.GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = _choiceCardWidth;
            size.flexibleWidth = 0;
        }
    }

    private static void LayoutCard(LoadoutCard card, float x, float y, float width, float height)
    {
        ResearchPanelUi2A.Place((RectTransform)card.Button.transform, x, y, width, height);
    }
}
