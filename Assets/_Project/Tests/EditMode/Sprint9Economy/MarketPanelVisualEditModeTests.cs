using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class MarketPanelVisualEditModeTests
{
    private GameObject _root;
    private MarketPanelView2A View(Action<string> select = null, Action<string> category = null, Action<MarketKindFilter2A> kind = null)
    {
        _root = new GameObject("MarketLayoutFixture", typeof(RectTransform));
        _root.GetComponent<RectTransform>().sizeDelta = new Vector2(1440, 2560);
        var view = _root.AddComponent<MarketPanelView2A>();
        view.Build(null, kind ?? (_ => { }), category ?? (_ => { }), _ => { }, select ?? (_ => { }), _ => { }, () => { }, () => { }, () => { });
        return view;
    }
    [TearDown] public void Cleanup() { if (_root != null) UnityEngine.Object.DestroyImmediate(_root); }
    [Test] public void KindAndCategoryFilters_KeepZeroStockGoodsButExcludeEmptyEquipment()
    {
        var goods = new MarketCardData2A { Equipment = false, Quantity = 0, CategoryKey = "medicine" };
        var equipment = new MarketCardData2A { Equipment = true, Quantity = 1, CategoryKey = "laser" };
        Assert.That(goods.Matches(MarketKindFilter2A.Goods, "all"), Is.True);
        Assert.That(goods.Matches(MarketKindFilter2A.Weapons, "all"), Is.False);
        Assert.That(equipment.Matches(MarketKindFilter2A.All, "laser"), Is.True);
        Assert.That(equipment.Matches(MarketKindFilter2A.All, "medicine"), Is.False);
        equipment.Quantity = 0;
        Assert.That(equipment.Matches(MarketKindFilter2A.All, "all"), Is.False);
    }
    [Test] public void GoodsHullsAndWeapons_AreSeparateKinds_AndAllIncludesEach()
    {
        var goods = new MarketCardData2A { Quantity = 0, CategoryKey = "goods:Medicine" };
        var hull = new MarketCardData2A { Equipment = true, Hull = true, Quantity = 1, CategoryKey = "equipment:hull" };
        var weapon = new MarketCardData2A { Equipment = true, Quantity = 1, CategoryKey = "equipment:Laser" };
        var items = new[] { goods, hull, weapon };
        var kinds = new[] { MarketKindFilter2A.Goods, MarketKindFilter2A.Hulls, MarketKindFilter2A.Weapons };
        for (int i = 0; i < items.Length; i++)
        {
            for (int j = 0; j < kinds.Length; j++) Assert.That(items[i].Matches(kinds[j], "all"), Is.EqualTo(i == j));
            Assert.That(items[i].Matches(MarketKindFilter2A.All, "all"), Is.True);
        }
        hull.Quantity = 0;
        Assert.That(hull.Matches(MarketKindFilter2A.Hulls, "all"), Is.False);
    }
    [Test] public void Subtypes_KeepGoodsAndAlliedWeaponTypes_ButHullsHaveOnlyAll()
    {
        var goods = new MarketCardData2A { CategoryKey = "goods:Medicine", CategoryTitle = "Medicine", Quantity = 0 };
        var weapon = new MarketCardData2A { Equipment = true, CategoryKey = "equipment:Laser", Quantity = 1 };
        var hull = new MarketCardData2A { Equipment = true, Hull = true, CategoryKey = "equipment:hull", Quantity = 1 };
        var rows = new List<KeyValuePair<string, string>>(); var seen = new HashSet<string>();
        var offers = new[] { goods, weapon, hull };
        var alliedTypes = new[] { WeaponType.Laser, WeaponType.Pulse };
        MarketScreenRules2A.BuildCategories(MarketKindFilter2A.Goods, offers, offers, rows, seen, alliedTypes);
        Assert.That(rows, Has.Count.EqualTo(2)); Assert.That(rows[1].Key, Is.EqualTo("goods:Medicine"));
        MarketScreenRules2A.BuildCategories(MarketKindFilter2A.Hulls, offers, offers, rows, seen, alliedTypes);
        Assert.That(rows, Has.Count.EqualTo(1)); Assert.That(rows[0].Key, Is.EqualTo("all"));
        MarketScreenRules2A.BuildCategories(MarketKindFilter2A.Weapons, Array.Empty<MarketCardData2A>(), Array.Empty<MarketCardData2A>(), rows, seen, alliedTypes);
        Assert.That(rows.Count, Is.EqualTo(alliedTypes.Length + 1));
        foreach (var type in alliedTypes)
            Assert.That(rows.Exists(x => x.Key == "equipment:" + type), Is.True, type.ToString());
        Assert.That(rows.Exists(x => x.Key == "equipment:hull"), Is.False);
        Assert.That(rows.Exists(x => x.Key == "equipment:Spore"), Is.False);
        MarketScreenRules2A.BuildCategories(MarketKindFilter2A.All, offers, offers, rows, seen, alliedTypes);
        Assert.That(rows.Exists(x => x.Key == "goods:Medicine"), Is.True);
        Assert.That(rows.Exists(x => x.Key == "equipment:hull"), Is.True);
        Assert.That(rows.Exists(x => x.Key == "equipment:Laser"), Is.True);
    }
    [Test] public void WeaponTypeCatalog_UsesOnlyAlliedRulesAndDeduplicatesTypes()
    {
        var ally = ScriptableObject.CreateInstance<WeaponConfig>();
        var enemy = ScriptableObject.CreateInstance<WeaponConfig>();
        try
        {
            var enemyFields = new SerializedObject(enemy);
            enemyFields.FindProperty("weaponType").intValue = (int)WeaponType.Spore;
            enemyFields.ApplyModifiedPropertiesWithoutUndo();
            var types = new List<WeaponType>();
            MarketScreenRules2A.CollectAlliedWeaponTypes(new[] {
                new MarketEquipmentRule { Weapon = ally, Allied = true },
                new MarketEquipmentRule { Weapon = ally, Allied = true },
                new MarketEquipmentRule { Weapon = enemy, Allied = false },
                new MarketEquipmentRule { Weapon = null, Allied = true } }, types);
            Assert.That(types, Is.EquivalentTo(new[] { WeaponType.Laser }));
            MarketScreenRules2A.CollectAlliedWeaponTypes(null, types);
            Assert.That(types, Is.Empty);
        }
        finally { UnityEngine.Object.DestroyImmediate(ally); UnityEngine.Object.DestroyImmediate(enemy); }
    }
    [Test] public void FiveTopButtons_SelectTheirCorrespondingKind_AndFitInOneRow()
    {
        var selected = MarketKindFilter2A.All;
        var view = View(kind: value => selected = value);
        var market = _root.transform.Find("SharedMarketLayout/MarketWorkspace/MarketInventory").GetComponent<RectTransform>();
        string[] labels = { "\u0422\u043e\u0432\u0430\u0440\u044b", "\u041a\u043e\u0440\u043f\u0443\u0441\u0430", "\u041e\u0440\u0443\u0436\u0438\u0435", "Модули", "\u0412\u0441\u0435" };
        for (int i = 0; i < labels.Length; i++)
        {
            var button = market.Find("Kind_" + i).GetComponent<Button>();
            Assert.That(button.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(labels[i]));
            button.onClick.Invoke(); Assert.That(selected, Is.EqualTo((MarketKindFilter2A)i));
            var rect = (RectTransform)button.transform;
            Assert.That(rect.anchoredPosition.x + rect.sizeDelta.x, Is.LessThanOrEqualTo(market.sizeDelta.x - 14 + .1f));
        }
    }
    [Test] public void MissionGoodsRemainVisibleButCarryALockedState()
    {
        var card = new MarketCardData2A { Equipment = false, Quantity = 5, TradeableQuantity = 0, CategoryKey = "medicine" };
        Assert.That(card.Matches(MarketKindFilter2A.Goods, "all"), Is.True);
        Assert.That(card.Locked, Is.True);
        card.TradeableQuantity = 2;
        Assert.That(card.Locked, Is.False, "Mixed mission and ordinary stacks still allow the ordinary units to be traded.");
        Assert.That(card.Quantity, Is.EqualTo(5), "The displayed hold quantity includes mission-owned units.");
    }
    [Test] public void SharedLayout_HasFourTopFiltersTwoStorageTabsAndTwoTradeActions()
    {
        var view = View(); Assert.That(view.IsReady, Is.True);
        var layout = _root.transform.Find("SharedMarketLayout");
        var market = layout.Find("MarketWorkspace/MarketInventory");
        var personal = layout.Find("MarketWorkspace/PersonalInventory");
        Assert.That(market.Find("Kind_0").GetComponent<Button>(), Is.Not.Null);
        Assert.That(market.Find("Kind_1").GetComponent<Button>(), Is.Not.Null);
        Assert.That(market.Find("Kind_2").GetComponent<Button>(), Is.Not.Null);
        Assert.That(market.Find("Kind_3").GetComponent<Button>(), Is.Not.Null);
        Assert.That(personal.Find("CargoTab").GetComponent<Button>(), Is.Not.Null);
        Assert.That(personal.Find("StorageTab").GetComponent<Button>(), Is.Not.Null);
        Assert.That(market.Find("Buy").GetComponent<Button>(), Is.Not.Null);
        Assert.That(personal.Find("Sell").GetComponent<Button>(), Is.Not.Null);
        Assert.That(personal.Find("Transfer").GetComponent<Button>(), Is.Not.Null);
        Assert.That(market.Find("AvailableItems/Viewport/Content").GetComponents<LayoutGroup>().Length, Is.EqualTo(1));
    }
    [Test] public void RefreshReusesCardsAndSelectionHandlerUsesLatestBinding()
    {
        string selected = null; int calls = 0;
        var view = View(key => { selected = key; calls++; });
        var empty = Array.Empty<MarketCardData2A>();
        view.SetCards(new[] { new MarketCardData2A { Key = "one", Title = "First", Quantity = 1, Price = 10, CargoSize = 2 } }, empty, null, null);
        var first = _root.GetComponentInChildren<MarketCardView2A>(true);
        view.SetCards(new[] { new MarketCardData2A { Key = "two", Title = "Second", Quantity = 0, Price = 20, CargoSize = 3 } }, empty, null, null);
        Assert.That(view.MarketCardPoolCount, Is.EqualTo(1));
        Assert.That(_root.GetComponentInChildren<MarketCardView2A>(true), Is.SameAs(first));
        first.GetComponent<Button>().onClick.Invoke();
        Assert.That(selected, Is.EqualTo("two")); Assert.That(calls, Is.EqualTo(1));
        Assert.That(first.transform.Find("CargoSizeBadge"), Is.Not.Null);
    }
    [Test] public void CategoryRow_HasVisibleButtonsAndForwardsTheChosenFilter()
    {
        string selected = null;
        var view = View(category: key => selected = key);
        view.SetFilters(MarketKindFilter2A.Goods, new[] {
            new KeyValuePair<string, string>("all", "All"),
            new KeyValuePair<string, string>("goods:Medicine", "Medicine") }, "goods:Medicine", false, false);
        var content = _root.transform.Find("SharedMarketLayout/MarketWorkspace/MarketInventory/CategoryFilters/Viewport/Content").GetComponent<RectTransform>();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        var button = content.Find("Category_1").GetComponent<Button>();
        Assert.That(content.rect.height, Is.GreaterThanOrEqualTo(72));
        Assert.That(button.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(64));
        Assert.That(button.GetComponent<Image>().color, Is.EqualTo(MissionPanelStyle2A.ActionBackground));
        Assert.That(button.GetComponent<Outline>().effectColor, Is.EqualTo(Color.white));
        button.onClick.Invoke();
        Assert.That(selected, Is.EqualTo("goods:Medicine"));
        var medicine = new MarketCardData2A { CategoryKey = "goods:Medicine", Quantity = 5 };
        var fuel = new MarketCardData2A { CategoryKey = "goods:Fuel", Quantity = 5 };
        Assert.That(medicine.Matches(MarketKindFilter2A.Goods, selected), Is.True);
        Assert.That(fuel.Matches(MarketKindFilter2A.Goods, selected), Is.False);
    }
    [Test] public void GoodsCardShowsStock_EquipmentHidesIt_AndImageIsCentered()
    {
        var view = View();
        var card = new MarketCardData2A { Key = "goods", Title = "Medicine", Quantity = 0, CargoSize = 2 };
        view.SetCards(new[] { card }, Array.Empty<MarketCardData2A>(), null, null);
        var rendered = _root.GetComponentInChildren<MarketCardView2A>(true);
        var rect = (RectTransform)rendered.transform;
        rect.sizeDelta = new Vector2(320, 236);
        rendered.Bind(card, false);
        Assert.That(rendered.transform.Find("CardHeader"), Is.Not.Null);
        var title = rendered.transform.Find("Name").GetComponent<RectTransform>();
        Assert.That(title.anchoredPosition.x + title.sizeDelta.x / 2, Is.EqualTo(160).Within(.1f));
        var image = rendered.transform.Find("ItemImage").GetComponent<RectTransform>();
        Assert.That(image.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(image.anchorMax, Is.EqualTo(Vector2.one));
        Assert.That(image.offsetMin, Is.EqualTo(Vector2.zero));
        Assert.That(image.offsetMax, Is.EqualTo(Vector2.zero));
        var stock = rendered.transform.Find("Quantity").GetComponent<TMP_Text>();
        Assert.That(stock.text, Is.EqualTo("0 ед."));
        card.Equipment = true; card.Quantity = 1; card.Range = 12.5f; card.Damage = 42; card.ShotCount = 3;
        rendered.Bind(card, false);
        Assert.That(stock.gameObject.activeSelf, Is.False);
        Assert.That(rendered.transform.Find("Range").GetComponent<TMP_Text>().text, Does.StartWith("Дальность: "));
        Assert.That(rendered.transform.Find("Damage").GetComponent<TMP_Text>().text, Is.EqualTo("Урон: 42"));
        Assert.That(rendered.transform.Find("ShotCount").GetComponent<TMP_Text>().text, Is.EqualTo("Кол-во выстрелов: 3"));
        card.Equipment = false;
        rendered.Bind(card, false);
        Assert.That(stock.gameObject.activeSelf, Is.True, "Pooled cards restore the goods stock label.");
        Assert.That(rendered.transform.Find("Range").gameObject.activeSelf, Is.False);
        Assert.That(stock.rectTransform.anchoredPosition.y, Is.EqualTo(rendered.transform.Find("UnitPrice").GetComponent<RectTransform>().anchoredPosition.y));
    }
    [Test] public void PurchaseQuantityForwardsChanges_RespectsLimits_AndHidesForEquipment()
    {
        _root = new GameObject("MarketLayoutFixture", typeof(RectTransform));
        _root.GetComponent<RectTransform>().sizeDelta = new Vector2(1440, 2560);
        var view = _root.AddComponent<MarketPanelView2A>();
        int delta = 0; int maxCalls = 0; int saleDelta = 0; int saleMaxCalls = 0; int minCalls = 0; int saleMinCalls = 0;
        view.Build(null, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, () => { }, () => { }, () => { }, value => delta += value, () => maxCalls++, value => saleDelta += value, () => saleMaxCalls++, () => minCalls++, () => saleMinCalls++);
        var panel = _root.transform.Find("SharedMarketLayout/MarketWorkspace/MarketInventory");
        var row = panel.Find("PurchaseRow");
        var picker = row.Find("PurchaseQuantity");
        view.SetPurchase(true, 1, 3, 42);
        Assert.That(picker.gameObject.activeSelf, Is.True);
        var rowRect = (RectTransform)row;
        Assert.That(rowRect.anchoredPosition.x + rowRect.sizeDelta.x / 2,
            Is.EqualTo(((RectTransform)panel).sizeDelta.x / 2).Within(.1f));
        var info = row.Find("PurchaseSelection").GetComponent<RectTransform>();
        var pickerRect = (RectTransform)picker;
        Assert.That(info.gameObject.activeSelf, Is.False);
        Assert.That(picker.Find("Plus").GetComponent<RectTransform>().sizeDelta.y, Is.EqualTo(36));
        Assert.That(picker.Find("Minus").GetComponent<Button>().interactable, Is.False);
        var plus = picker.Find("Plus").GetComponent<Button>();
        Assert.That(plus.interactable, Is.True);
        plus.onClick.Invoke(); Assert.That(delta, Is.EqualTo(1));
        var max = picker.Find("Max").GetComponent<Button>();
        Assert.That(max.GetComponent<RectTransform>().anchoredPosition.x,
            Is.GreaterThan(picker.Find("Plus").GetComponent<RectTransform>().anchoredPosition.x));
        Assert.That(picker.Find("Min").GetComponent<RectTransform>().anchoredPosition.x,
            Is.LessThan(picker.Find("Minus").GetComponent<RectTransform>().anchoredPosition.x));
        Assert.That(max.interactable, Is.True);
        max.onClick.Invoke(); Assert.That(maxCalls, Is.EqualTo(1));
        Assert.That(panel.Find("Buy").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Купить за 42 кр"));
        view.SetPurchase(true, 3, 3, 126);
        Assert.That(plus.interactable, Is.False);
        Assert.That(max.interactable, Is.False);
        var min = picker.Find("Min").GetComponent<Button>();
        Assert.That(min.interactable, Is.True); min.onClick.Invoke();
        Assert.That(minCalls, Is.EqualTo(1));
        Assert.That(picker.Find("Quantity").GetComponent<TMP_Text>().text, Is.EqualTo("3"));
        view.SetPurchase(false, 1, 1, 100);
        Assert.That(picker.gameObject.activeSelf, Is.False);
        var personal = panel.parent.Find("PersonalInventory");
        var sale = personal.Find("SaleRow/SaleQuantity");
        view.SetSale(true, 1, 5, 90);
        Assert.That(sale.gameObject.activeSelf, Is.True);
        Assert.That(sale.Find("Max").GetComponent<RectTransform>().anchoredPosition.x,
            Is.GreaterThan(sale.Find("Plus").GetComponent<RectTransform>().anchoredPosition.x));
        sale.Find("Plus").GetComponent<Button>().onClick.Invoke();
        sale.Find("Max").GetComponent<Button>().onClick.Invoke();
        Assert.That(saleDelta, Is.EqualTo(1)); Assert.That(saleMaxCalls, Is.EqualTo(1));
        Assert.That(personal.Find("Sell").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Продать за 90 кр"));
        view.SetSale(true, 5, 5, 450);
        sale.Find("Min").GetComponent<Button>().onClick.Invoke();
        Assert.That(saleMinCalls, Is.EqualTo(1));
        Assert.That(sale.Find("Plus").GetComponent<Button>().interactable, Is.False);
        view.SetSale(false, 1, 1, 100);
        Assert.That(sale.gameObject.activeSelf, Is.False);
        Assert.That(panel.parent.Find("Notice"), Is.Null);
    }
    [Test] public void EquipmentDetailsToggle_ReplacesImage_PreservesModeOnRefresh_ResetsForAnotherItem()
    {
        int selections = 0;
        var view = View(_ => selections++);
        var data = new MarketCardData2A { Key = "weapon-a", Title = "Laser", Equipment = true, Quantity = 1,
            Damage = 19, Range = 8, ShotCount = 3, CargoSize = 7 };
        view.SetCards(new[] { data }, Array.Empty<MarketCardData2A>(), null, null);
        var card = _root.GetComponentInChildren<MarketCardView2A>(true);
        var toggle = card.transform.Find("DetailsToggle").GetComponent<Button>();
        Assert.That(toggle.gameObject.activeSelf, Is.True);
        Assert.That(card.transform.Find("ItemImage").gameObject.activeSelf, Is.True);
        Assert.That(card.transform.Find("Range").gameObject.activeSelf, Is.False);
        toggle.onClick.Invoke();
        Assert.That(selections, Is.Zero, "The help button must not change the trade selection.");
        Assert.That(card.transform.Find("ItemImage").gameObject.activeSelf, Is.False);
        Assert.That(card.transform.Find("Placeholder").gameObject.activeSelf, Is.False);
        foreach (string name in new[] { "Range", "Damage", "ShotCount", "CargoInfo" })
            Assert.That(card.transform.Find(name).gameObject.activeSelf, Is.True);
        Assert.That(card.transform.Find("CardFooter").gameObject.activeSelf, Is.True);
        card.Bind(data, true);
        Assert.That(card.transform.Find("Range").gameObject.activeSelf, Is.True);
        toggle.onClick.Invoke();
        Assert.That(card.transform.Find("ItemImage").gameObject.activeSelf, Is.True);
        Assert.That(card.transform.Find("Range").gameObject.activeSelf, Is.False);
        toggle.onClick.Invoke(); data.Key = "weapon-b"; card.Bind(data, false);
        Assert.That(card.transform.Find("Range").gameObject.activeSelf, Is.False);
        Assert.That(card.transform.Find("ItemImage").gameObject.activeSelf, Is.True);
        data.Equipment = false; card.Bind(data, false);
        Assert.That(toggle.gameObject.activeSelf, Is.True);
        toggle.onClick.Invoke();
        Assert.That(card.transform.Find("Range").gameObject.activeSelf, Is.False);
        Assert.That(card.transform.Find("ItemImage").gameObject.activeSelf, Is.False);
        Assert.That(card.transform.Find("CargoInfo").gameObject.activeSelf, Is.True);
        Assert.That(card.transform.Find("CargoInfo").GetComponent<TMP_Text>().text, Is.EqualTo("Занимает в трюме: 7"));
        toggle.onClick.Invoke();
        Assert.That(card.transform.Find("ItemImage").gameObject.activeSelf, Is.True);
        Assert.That(card.transform.Find("Range").gameObject.activeSelf, Is.False);
    }
    [Test] public void HullCard_UsesEquipmentFilterAndShowsHullSpecificInformation()
    {
        var data = new MarketCardData2A { Key = "hull", Title = "Hull", Equipment = true, Hull = true, Quantity = 1,
            CategoryKey = "equipment:hull", Damage = 120, CargoSize = 7 };
        Assert.That(data.Matches(MarketKindFilter2A.Hulls, "equipment:hull"), Is.True);
        Assert.That(data.Matches(MarketKindFilter2A.Goods, "all"), Is.False);
        var view = View(); view.SetCards(new[] { data }, Array.Empty<MarketCardData2A>(), null, null);
        var card = _root.GetComponentInChildren<MarketCardView2A>(true);
        card.transform.Find("DetailsToggle").GetComponent<Button>().onClick.Invoke();
        Assert.That(card.transform.Find("Range").GetComponent<TMP_Text>().text, Is.EqualTo("Прочность: 120"));
        Assert.That(card.transform.Find("Damage").gameObject.activeSelf, Is.False);
        Assert.That(card.transform.Find("ShotCount").gameObject.activeSelf, Is.False);
        Assert.That(card.transform.Find("CargoInfo").gameObject.activeSelf, Is.False);
        Assert.That(card.transform.Find("CargoSizeBadge").gameObject.activeSelf, Is.False);
    }
    [TestCase(0)] [TestCase(80)] [TestCase(160)]
    public void EquipmentFooter_ShowsConditionOnlyInDetails_AndCentersPrice(int current)
    {
        var view = View();
        var data = new MarketCardData2A { Key = "weapon", Title = "Laser", Equipment = true, Quantity = 1, Price = 123, CurrentDurability = current, MaximumDurability = 160 };
        view.SetCards(new[] { data }, Array.Empty<MarketCardData2A>(), null, null);
        var rendered = _root.GetComponentInChildren<MarketCardView2A>(true);
        var rect = (RectTransform)rendered.transform; rect.sizeDelta = new Vector2(360, 300);
        rendered.Bind(data, false);
        var footer = rendered.transform.Find("CardFooter").GetComponent<RectTransform>();
        Assert.That(footer.gameObject.activeSelf, Is.True);
        Assert.That(footer.anchoredPosition.y, Is.EqualTo(-(300 - 44)));
        var fill = footer.Find("DurabilityFill").GetComponent<RectTransform>();
        var condition = footer.Find("Durability").GetComponent<TMP_Text>();
        Assert.That(fill.gameObject.activeSelf, Is.False);
        Assert.That(condition.gameObject.activeSelf, Is.False);
        rendered.transform.Find("DetailsToggle").GetComponent<Button>().onClick.Invoke();
        Assert.That(fill.gameObject.activeSelf, Is.True);
        Assert.That(condition.gameObject.activeSelf, Is.True);
        Assert.That(fill.anchorMax.x, Is.EqualTo(current / 160f));
        Assert.That(condition.text, Is.EqualTo(current + " / 160"));
        var price = footer.Find("UnitPrice").GetComponent<RectTransform>();
        Assert.That(price.anchoredPosition.x + price.sizeDelta.x / 2, Is.EqualTo(180).Within(.1f));
        Assert.That(rendered.transform.Find("Name").GetComponent<TMP_Text>().text, Is.EqualTo("Laser"));
        data.Equipment = false; data.MaximumDurability = 0; rendered.Bind(data, false);
        Assert.That(footer.gameObject.activeSelf, Is.False);
        Assert.That(rendered.transform.Find("UnitPrice").gameObject.activeInHierarchy, Is.True);
    }
    [Test] public void NewHubIllustrationIsBound_AndOtherPlanetTypesUseTheirBaseArtwork()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MarketVisualCatalog2A>("Assets/_Project/Content/Configs/MarketProfiles/marketVisualCatalog_01.asset");
        Assert.That(catalog, Is.Not.Null); Assert.That(catalog.BasePlanetIllustrations, Is.Not.Null);
        Assert.That(catalog.EquipmentIcon, Is.Not.Null); Assert.That(catalog.MissileIcon, Is.Not.Null);
        var hub = AssetDatabase.LoadAssetAtPath<PlanetConfig>("Assets/_Project/Content/Configs/Galaxy/Planets/planet_solaria_hub_01.asset");
        var mining = AssetDatabase.LoadAssetAtPath<PlanetConfig>("Assets/_Project/Content/Configs/Galaxy/Planets/planet_solaria_mining_01.asset");
        Sprite hero = catalog.Get(hub, null);
        Assert.That(hero, Is.Not.Null); Assert.That(hero, Is.Not.SameAs(catalog.BasePlanetIllustrations.Get(PlanetType.Hub, null)));
        Assert.That(hero.rect.width / hero.rect.height, Is.EqualTo(9f / 4f).Within(.02f));
        Assert.That(catalog.Get(mining, null), Is.SameAs(catalog.BasePlanetIllustrations.Get(PlanetType.Mining, null)));
    }
    [Test] public void LayoutKeepsWideMarketAndNarrowPersonalColumn_BelowIllustration()
    {
        var view = View();
        var layout = _root.transform.Find("SharedMarketLayout");
        var hero = layout.Find("MarketIllustration").GetComponent<RectTransform>();
        var workspace = layout.Find("MarketWorkspace").GetComponent<RectTransform>();
        var market = workspace.Find("MarketInventory").GetComponent<RectTransform>();
        var personal = workspace.Find("PersonalInventory").GetComponent<RectTransform>();
        Assert.That(-hero.anchoredPosition.y, Is.EqualTo(194));
        Assert.That(-workspace.anchoredPosition.y, Is.EqualTo(194 + 1440 * 4f / 9f).Within(.1f));
        Assert.That(market.sizeDelta.x, Is.GreaterThan(personal.sizeDelta.x));
        var buy = market.Find("Buy").GetComponent<RectTransform>();
        var sell = personal.Find("Sell").GetComponent<RectTransform>();
        Assert.That(buy.anchoredPosition.y, Is.EqualTo(sell.anchoredPosition.y));
    }
}
