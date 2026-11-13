using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlanetServiceVisualsEditModeTests
{
    private readonly List<UnityEngine.Object> _temporary = new List<UnityEngine.Object>();
    private PlanetServiceVisualCatalog2A Catalog => AssetDatabase.LoadAssetAtPath<PlanetServiceVisualCatalog2A>(
        "Assets/_Project/Content/Configs/ServiceVisuals/planetServiceVisuals_01.asset");
    private PlanetConfig Planet(PlanetType type)
    {
        var planet = ScriptableObject.CreateInstance<PlanetConfig>(); _temporary.Add(planet);
        var fields = new SerializedObject(planet); fields.FindProperty("planetType").intValue = (int)type;
        fields.ApplyModifiedPropertiesWithoutUndo(); return planet;
    }
    private GameObject Root(string name)
    {
        var root = new GameObject(name, typeof(RectTransform)); _temporary.Add(root);
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(1440, 2560); return root;
    }
    [TearDown] public void Cleanup()
    {
        for (int i = _temporary.Count - 1; i >= 0; i--) if (_temporary[i] != null) UnityEngine.Object.DestroyImmediate(_temporary[i]);
        _temporary.Clear();
    }
    [TestCase(PlanetServiceKind2A.Refuel, "solaria-refuel-v2-9x4.png")]
    [TestCase(PlanetServiceKind2A.Repair, "solaria-repair-v3-9x4.png")]
    [TestCase(PlanetServiceKind2A.Market, "solaria-market-v3-9x4.png")]
    public void HubPlanets_UseApprovedServiceArtRegardlessOfPlanetIdentity(PlanetServiceKind2A kind, string name)
    {
        Assert.That(Catalog, Is.Not.Null);
        var first = Catalog.Get(Planet(PlanetType.Hub), kind, null);
        var second = Catalog.Get(Planet(PlanetType.Hub), kind, null);
        Assert.That(first, Is.Not.Null); Assert.That(second, Is.SameAs(first));
        Assert.That(AssetDatabase.GetAssetPath(first), Is.EqualTo("Assets/Art/Services/Solaria/" + name));
        Assert.That(first.rect.width, Is.EqualTo(1881)); Assert.That(first.rect.height, Is.EqualTo(836));
    }
    [TestCase(PlanetType.Frontier, "frontier")]
    [TestCase(PlanetType.Mining, "mining")]
    [TestCase(PlanetType.Industrial, "industrial")]
    [TestCase(PlanetType.Science, "science")]
    public void PlanetTypes_UseTheirOwnServiceArt(PlanetType type, string location)
    {
        var planet = Planet(type);
        AssertServiceArt(kind => Catalog.Get(planet, kind, null), "Planets/planet-" + location);
    }

    [TestCase(StationType.Military, "military")]
    [TestCase(StationType.RangerBase, "ranger")]
    [TestCase(StationType.Trade, "trade")]
    [TestCase(StationType.Science, "science")]
    [TestCase(StationType.Medical, "medical")]
    public void StationTypes_UseTheirOwnServiceArt(StationType type, string location)
    {
        var stationType = ScriptableObject.CreateInstance<StationTypeConfig>();
        _temporary.Add(stationType);
        var typeFields = new SerializedObject(stationType);
        typeFields.FindProperty("stationType").intValue = (int)type;
        typeFields.ApplyModifiedPropertiesWithoutUndo();

        var station = ScriptableObject.CreateInstance<StationConfig>();
        _temporary.Add(station);
        var stationFields = new SerializedObject(station);
        stationFields.FindProperty("stationTypeConfig").objectReferenceValue = stationType;
        stationFields.ApplyModifiedPropertiesWithoutUndo();
        AssertServiceArt(kind => Catalog.Get(null, kind, null, station), "Stations/station-" + location);
    }

    private static void AssertServiceArt(Func<PlanetServiceKind2A, Sprite> lookup, string pathPrefix)
    {
        foreach (PlanetServiceKind2A kind in Enum.GetValues(typeof(PlanetServiceKind2A)))
        {
            var sprite = lookup(kind);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(sprite),
                Is.EqualTo("Assets/Art/Services/" + pathPrefix + "-" + kind.ToString().ToLowerInvariant() + "-v1-9x4.png"));
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(1881, 836)));
        }
    }

    [Test] public void DeadPlanetAndMissingLocation_KeepExistingFallback()
    {
        var fallback = Catalog.Get(Planet(PlanetType.Hub), PlanetServiceKind2A.Refuel, null);
        foreach (PlanetServiceKind2A kind in Enum.GetValues(typeof(PlanetServiceKind2A)))
        {
            Assert.That(Catalog.Get(Planet(PlanetType.Dead), kind, fallback), Is.SameAs(fallback));
            Assert.That(Catalog.Get(null, kind, fallback), Is.SameAs(fallback));
        }
    }
    [Test] public void RepairFuelAndMarket_KeepHeroInTheSamePlaceAndWorkBelowIt()
    {
        var hub = Planet(PlanetType.Hub);
        var repairRoot = Root("Repair"); var repair = repairRoot.AddComponent<HangarPanelView2A>();
        repair.BuildLoadout(null, null, _ => { }, _ => { }, _ => { }, () => { }, () => { });
        repair.SetHeader(Catalog.Get(hub, PlanetServiceKind2A.Repair, null));
        var fuelRoot = Root("Fuel"); var fuel = fuelRoot.AddComponent<RefuelPanelView2A>(); fuel.Build(null);
        fuel.SetSprites(Catalog.Get(hub, PlanetServiceKind2A.Refuel, null), null);
        var marketRoot = Root("Market"); var market = marketRoot.AddComponent<MarketPanelView2A>();
        market.Build(null, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, () => { }, () => { }, () => { });
        market.SetHeader(Catalog.Get(hub, PlanetServiceKind2A.Market, null));
        foreach (var image in new[] { repair.Header, fuel.Header, market.Header })
        {
            Assert.That(image.preserveAspect, Is.True);
            Assert.That(image.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(0, -194)));
            Assert.That(image.rectTransform.sizeDelta.x, Is.EqualTo(1440));
            Assert.That(image.rectTransform.sizeDelta.y, Is.EqualTo(640).Within(.1f));
        }
        var work = repairRoot.transform.Find("SharedRepairLayout/RepairWorkspace").GetComponent<RectTransform>();
        Assert.That(work.offsetMax.y, Is.EqualTo(-834).Within(.1f));
        Assert.That(work.offsetMin.y, Is.EqualTo(142));
    }
    [Test] public void StackedHangar_ReservesSideMenusAndPinsHorizontalInventoryToTheBottom()
    {
        var root = Root("Repair");
        var legacy = root.AddComponent<HangarPanelView2A>();
        legacy.BuildLoadout(null, null, _ => { }, _ => { }, _ => { }, () => { }, () => { });
        legacy.HideLayout();

        var host = ResearchPanelUi2A.Rect(root.transform, "StackedHangar");
        var view = host.gameObject.AddComponent<HangarPanelView2A>();
        view.BuildLoadout(null, null, _ => { }, _ => { }, _ => { }, () => { }, () => { }, stacked: true);
        view.RefreshLayout();

        var oldLayout = root.transform.Find("SharedRepairLayout");
        Assert.That(oldLayout.gameObject.activeSelf, Is.False);
        Assert.That(oldLayout.Find("LocationIllustration"), Is.Not.Null);
        Assert.That(view.IsReady, Is.True);
        Assert.That(view.Header, Is.Null);

        var work = host.Find("StackedHangarLayout/RepairWorkspace");
        var ship = (RectTransform)work.Find("ShipRepair");
        var inventory = (RectTransform)work.Find("HangarEquipment");
        float leftInset = ship.anchoredPosition.x;
        float rightInset = ((RectTransform)work).rect.width - leftInset - ship.rect.width;
        Assert.That(leftInset, Is.GreaterThan(0));
        Assert.That(rightInset, Is.EqualTo(leftInset).Within(.1f));

        Assert.That(inventory.rect.width, Is.EqualTo(1440).Within(.1f));
        Assert.That(-inventory.anchoredPosition.y + inventory.rect.height,
            Is.EqualTo(((RectTransform)work).rect.height).Within(.1f));
        Assert.That(inventory.anchoredPosition.y, Is.LessThan(-ship.rect.height));

        view.SetInventory(false, "Станция", 5, true);
        Assert.That(inventory.Find("ChoiceTitle"), Is.Null);
        for (int i = 0; i < 5; i++) view.SetChoice(i, "Двигатель", null, false, "", "engine-" + i);
        var scroll = inventory.GetComponentInChildren<ScrollRect>();
        Assert.That(scroll.horizontal, Is.True);
        Assert.That(scroll.vertical, Is.False);
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        var first = (RectTransform)scroll.content.Find("Item_0");
        var last = (RectTransform)scroll.content.Find("Item_4");
        Assert.That(last.anchoredPosition.y, Is.EqualTo(first.anchoredPosition.y).Within(.1f));
        Assert.That(last.anchoredPosition.x, Is.GreaterThan(scroll.viewport.rect.width - last.rect.width));

        float inventoryHeight = inventory.rect.height;
        var hull = (RectTransform)ship.Find("ShipLoadout/Viewport/Content/ShipSlots/Slot_0");
        float hullHeight = hull.rect.height;
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(1440, 2800);
        view.RefreshLayout();
        Assert.That(inventory.rect.height, Is.EqualTo(inventoryHeight).Within(.1f));
        Assert.That(hull.rect.height, Is.GreaterThan(hullHeight));
    }

    [Test] public void UnavailableWeaponSlot_IsVisiblyDisabledAndCannotBeSelected()
    {
        var root = Root("Repair");
        var view = root.AddComponent<HangarPanelView2A>();
        view.BuildLoadout(null, null, _ => { }, _ => { }, _ => { }, () => { }, () => { });
        int slot = ShipLoadoutRules.FirstWeaponSlot + 4;
        view.SetSlot(slot, "Оружие", null, false, false, empty: true);
        var button = System.Array.Find(root.GetComponentsInChildren<Button>(true), x => x.name == "Slot_" + slot);
        Assert.That(button, Is.Not.Null);
        Assert.That(button.interactable, Is.False);
        Assert.That(((Image)button.targetGraphic).color, Is.EqualTo(MissionPanelStyle2A.DisabledBackground));
        var title = button.GetComponentInChildren<TMP_Text>();
        Assert.That(title.text, Is.EqualTo("Оружие"));
        Assert.That(title.alignment, Is.EqualTo(TextAlignmentOptions.Center));
        Assert.That(title.enableAutoSizing, Is.False);

        view.SetSlot(slot, "Оружие дальнего боя", null, true, true, empty: true);
        var vacant = ((Image)button.targetGraphic).GetComponent<Outline>().effectColor;
        Assert.That(vacant.r, Is.GreaterThan(vacant.b));
        Assert.That(vacant.g, Is.GreaterThan(vacant.b));
        Assert.That(title.text, Is.EqualTo("Оружие дальнего боя"));
        Assert.That(title.fontSize, Is.EqualTo(25));
        Assert.That(title.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
    }

    [TestCase(false, true, "Снять")]
    [TestCase(true, false, "Установить")]
    [TestCase(true, true, "Заменить")]
    public void HangarCard_SelectsThenOpensBlockingEquipmentModal(bool inventory, bool occupied, string actionLabel)
    {
        var root = Root("Repair");
        var view = root.AddComponent<HangarPanelView2A>();
        const string details = "Описание двигателя";
        int selections = 0, equips = 0, removals = 0, installedRepairs = 0, inventoryRepairs = 0;
        view.BuildLoadout(null, null, _ => selections++, _ => { }, _ => selections++, () => equips++, () => removals++,
            () => installedRepairs++, repairInventory: () => inventoryRepairs++);
        if (inventory) view.SetChoice(0, "Двигатель", null, true, details, "engine", (80, 100));
        else view.SetSlot(ShipLoadoutRules.FirstComponentSlot, "", null, true, true, details, "engine", equipmentName: "Двигатель", durability: (80, 100));
        view.SetLoadoutActions(occupied, false, !inventory, "", "", 40, 0, 80, 100, true);

        string name = inventory ? "Item_0" : "Slot_" + ShipLoadoutRules.FirstComponentSlot;
        var button = System.Array.Find(root.GetComponentsInChildren<Button>(true), x => x.name == name);
        if (inventory) Assert.That(button.transform.Find("Label").gameObject.activeSelf, Is.False);
        var modal = root.transform.Find("SharedRepairLayout/EquipmentModal");
        button.onClick.Invoke();
        Assert.That(selections, Is.EqualTo(1));
        Assert.That(modal.gameObject.activeSelf, Is.False);

        button.onClick.Invoke();
        Assert.That(modal.gameObject.activeSelf, Is.True);
        Assert.That(modal.GetComponent<Image>().raycastTarget, Is.True);
        var window = modal.Find("EquipmentWindow");
        Assert.That(window.Find("Heading/Title").GetComponent<TMP_Text>().text, Is.EqualTo("Двигатель"));
        Assert.That(modal.GetComponentInChildren<ScrollRect>().content.Find("EquipmentInfo").GetComponent<TMP_Text>().text, Is.EqualTo(details));
        button.onClick.Invoke();
        Assert.That(selections, Is.EqualTo(1));

        var track = (RectTransform)window.Find("DurabilityTrack");
        var fill = (RectTransform)track.Find("Fill");
        Assert.That(track.Find("Durability").GetComponent<TMP_Text>().text, Is.EqualTo("Прочность: 80 / 100"));
        Assert.That(fill.anchorMax.x, Is.EqualTo(.8f).Within(.001f));
        var action = window.Find("EquipmentAction").GetComponent<Button>();
        Assert.That(action.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(actionLabel));
        Assert.That(action.interactable, Is.EqualTo(!inventory));
        if (inventory)
        {
            action.onClick.Invoke();
            Assert.That(equips, Is.Zero);
            view.SetLoadoutActions(occupied, true, false, "", "", 40, 0, 80, 100, true);
            Assert.That(action.interactable, Is.True);
        }
        action.onClick.Invoke();
        Assert.That(equips, Is.EqualTo(inventory ? 1 : 0));
        window.Find("RepairEquipment").GetComponent<Button>().onClick.Invoke();
        Assert.That(removals, Is.EqualTo(inventory ? 0 : 1));
        Assert.That(installedRepairs, Is.EqualTo(inventory ? 0 : 1));
        Assert.That(inventoryRepairs, Is.EqualTo(inventory ? 1 : 0));
        modal.Find("CloseBackdrop").GetComponent<Button>().onClick.Invoke();
        Assert.That(modal.gameObject.activeSelf, Is.False);
    }

    [Test] public void RepairLayout_ReusesControlsAndTheirHandlers_AndRetiresOldWrappers()
    {
        var root = Root("Repair");
        var legacy = ResearchPanelUi2A.Rect(root.transform, "OldRepairControls");
        var button = ResearchPanelUi2A.Button(legacy, null, "ExistingRepairButton", "Repair", out _);
        int calls = 0; button.onClick.AddListener(() => calls++);
        var view = root.AddComponent<HangarPanelView2A>();
        view.BuildLoadout(null, button, _ => { }, _ => { }, _ => { }, () => { }, () => { });
        Assert.That(button.transform.parent.name, Is.EqualTo("ShipActions"));
        Assert.That(button.gameObject.activeInHierarchy, Is.True);
        Assert.That(legacy.gameObject.activeSelf, Is.False);
        button.onClick.Invoke(); Assert.That(calls, Is.EqualTo(1));
        view.BuildLoadout(null, button, _ => { }, _ => { }, _ => { }, () => { }, () => { });
        button.onClick.Invoke(); Assert.That(calls, Is.EqualTo(2));
        Assert.That(root.GetComponentsInChildren<HangarPanelView2A>(true), Has.Length.EqualTo(1));
    }
}
