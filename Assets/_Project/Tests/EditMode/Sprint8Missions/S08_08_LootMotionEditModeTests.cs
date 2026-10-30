using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

public sealed class S08_08_LootMotionEditModeTests
{
    private Bootstrapper _previousBootstrap;
    private GameObject _host;
    private GrabberConfig _grabber;
    private AllyConfig _ally;
    private GameRuntimeState _state;
    private SystemNpcRuntimeState _ranger;
    private SystemSpaceLootService _loot;
    private IInventoryService _inventory;
    private ItemConfig _astralite;
    private WeaponConfig _heavyWeapon;
    private ShipMovementRuntimeState _movementState;
    private float _previousSecondsPerTick;

    [SetUp]
    public void SetUp()
    {
        _previousBootstrap = Bootstrapper.Instance;
        _previousSecondsPerTick = GameTimeState.SecondsPerDay;
        GameTimeState.SecondsPerDay = 1f;
        _host = new GameObject("LootMotionTest");
        _host.SetActive(false);
        var bootstrap = _host.AddComponent<Bootstrapper>();
        Bootstrapper.Instance = bootstrap;
        var services = new ServiceRegistry();
        bootstrap.ServiceRegistry = services;
        _grabber = ScriptableObject.CreateInstance<GrabberConfig>();
        _ally = ScriptableObject.CreateInstance<AllyConfig>();
        typeof(AllyConfig).GetField("grabber", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_ally, _grabber);
        _state = new GameRuntimeState
        {
            Player = new PlayerState
            {
                CurrentSystemId = "system-1", SystemMapShipPosition = Vector3.zero
            }
        };
        _state.Player.PlayerShipState.ActiveShipId = "ship-1";
        _state.Player.PlayerShipState.OwnedShips.Add(new ShipRuntimeData
        {
            ShipId = "ship-1", AllyConfigId = "ally-1"
        });
        _ranger = new SystemNpcRuntimeState
        {
            RuntimeNpcId = "ranger-1", NpcType = SystemNpcType.Ally,
            AllyRole = AllyRole2A.Ranger, ConfigId = "ally-1",
            CurrentSystemId = "system-1", CurrentPosition = Vector3.zero,
            IsAlive = true, CurrentBehavior = SystemNpcBehaviorType.CollectDebris,
            LootTargetId = "loot-1"
        };
        services.Register(new SimpleEventBus());
        _heavyWeapon = null;
        _astralite = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemConfig>(
            "Assets/_Project/Content/Configs/Items/item_astralite_ancients_01.asset");
        Assert.That(_astralite, Is.Not.Null);
        services.Register(S08MissionTestProxy.Make<IGameSessionService>((m, a) =>
            m.Name == "get_State" ? _state : DefaultValue(m)));
        services.Register(S08MissionTestProxy.Make<IConfigService>((m, a) => m.Name switch
        {
            "GetAllyConfigById" => _ally,
            "GetItemConfigById" => UnityEditor.AssetDatabase.LoadAssetAtPath<ItemConfig>(
                "Assets/_Project/Content/Configs/Items/" + (string)a[0] + ".asset"),
            "GetWeaponConfigById" => LoadTestWeapon((string)a[0]),
            "GetEnemyConfigById" => LoadTestEnemy((string)a[0]),
            "get_CombatFxVisualConfig" => UnityEditor.AssetDatabase.LoadAssetAtPath<CombatFxVisualConfig>(
                "Assets/_Project/Content/Configs/Game/combatFxVisualConfig_01.asset"),
            _ => DefaultValue(m)
        }));
        services.Register(S08MissionTestProxy.Make<ISystemNpcRuntimeService>((m, a) =>
        {
            if (m.Name != "TryGetNpc") return DefaultValue(m);
            a[1] = _ranger;
            return (string)a[0] == _ranger.RuntimeNpcId;
        }));
        _inventory = new InventoryService();
        services.Register(_inventory);
        services.Register(S08MissionTestProxy.Make<ISystemTravelService>((m, a) => DefaultValue(m)));
        _movementState = new ShipMovementRuntimeState();
        services.Register(S08MissionTestProxy.Make<IShipMovementService>((m, a) =>
            m.Name == "get_State" ? _movementState : DefaultValue(m)));
        services.Register(S08MissionTestProxy.Make<ITargetService2A>((m, a) => DefaultValue(m)));
        _loot = new SystemSpaceLootService();
        _loot.Add(new SpaceLootRuntimeState
        {
            LootId = "loot-1", SystemId = "system-1", Kind = SpaceLootKind.Diamonds,
            Quantity = 2, CurrentHealth = 1, Position = new Vector3(50f, 0f, 0f)
        });
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_host);
        Object.DestroyImmediate(_ally);
        Object.DestroyImmediate(_grabber);
        Bootstrapper.Instance = _previousBootstrap;
        GameTimeState.SecondsPerDay = _previousSecondsPerTick;
    }

    [TestCase(false, 4)]
    [TestCase(false, 60)]
    [TestCase(true, 4)]
    [TestCase(true, 60)]
    public void PullAdvancesBetweenTicksAndCollectsOnceOnArrival(bool ranger, int framesPerTick)
    {
        BeginPull(ranger);
        SpaceLootRuntimeState drop = _loot.Find("loot-1");
        Assert.That(drop.Position.x, Is.EqualTo(50f), "Claiming must not teleport loot.");
        float delta = 1f / framesPerTick;
        _loot.TickCollection(delta);
        Assert.That(drop.Position.x, Is.LessThan(50f).And.GreaterThan(25f),
            "Loot must move during a fraction of a tick, without a full-tick jump.");
        Assert.That(_loot.Find("loot-1"), Is.Not.Null);
        Assert.That(_state.Player.Diamonds, Is.Zero);
        for (int i = 1; i < framesPerTick; i++) _loot.TickCollection(delta);
        Assert.That(drop.Position.x, Is.EqualTo(25f).Within(0.001f));
        Assert.That(_loot.Find("loot-1"), Is.Not.Null);
        for (int i = 0; i < framesPerTick; i++) _loot.TickCollection(delta);
        Assert.That(_loot.Find("loot-1"), Is.Null);
        Assert.That(_state.Player.Diamonds, Is.EqualTo(ranger ? 0 : 2));
        _loot.TickCollection(1f);
        Assert.That(_state.Player.Diamonds, Is.EqualTo(ranger ? 0 : 2));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CancellingPullStopsItAtItsCurrentPosition(bool ranger)
    {
        BeginPull(ranger);
        _loot.TickCollection(0.25f);
        Vector3 position = _loot.Find("loot-1").Position;
        if (ranger) _ranger.IsOnPlanet = true;
        else Assert.That(_loot.TryTogglePlayerCollectionMark("loot-1", out _), Is.True);
        _loot.TickCollection(0.5f);
        Assert.That(_loot.Find("loot-1").Position, Is.EqualTo(position));
        Assert.That(_loot.Find("loot-1").PullCollectorId, Is.Null);
    }

    [Test]
    public void PullUsesConfiguredTickDurationAndDoesNotMoveWithoutElapsedTime()
    {
        GameTimeState.SecondsPerDay = 2f;
        BeginPull(false);
        _loot.TickCollection(0f);
        Assert.That(_loot.Find("loot-1").Position.x, Is.EqualTo(50f));
        _loot.TickCollection(1f);
        Assert.That(_loot.Find("loot-1").Position.x, Is.EqualTo(37.5f).Within(0.001f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PlayerPullsNearestMarkedLootOneAtATimeRegardlessOfMarkOrder(bool nearFirst)
    {
        foreach (var pair in new[] { ("near", 25f), ("middle", 40f) })
            _loot.Add(new SpaceLootRuntimeState
            {
                LootId = pair.Item1, SystemId = "system-1", Kind = SpaceLootKind.Diamonds,
                Quantity = 1, CurrentHealth = 1, Position = new Vector3(pair.Item2, 0f, 0f)
            });
        foreach (string id in nearFirst
                     ? new[] { "near", "middle", "loot-1" }
                     : new[] { "loot-1", "middle", "near" })
            Assert.That(_loot.TryTogglePlayerCollectionMark(id, out _), Is.True);
        _loot.TickCollection(0.5f);
        Assert.That(_loot.Find("near").Position.x, Is.EqualTo(12.5f));
        Assert.That(_loot.Find("near").PullCollectorId, Is.EqualTo("player"));
        Assert.That(_loot.Find("middle").Position.x, Is.EqualTo(40f));
        Assert.That(_loot.Find("middle").PullCollectorId, Is.Null);
        Assert.That(_loot.Find("loot-1").Position.x, Is.EqualTo(50f));
        Assert.That(_loot.Find("loot-1").PullCollectorId, Is.Null);
        _loot.TickCollection(0.5f);
        Assert.That(_loot.Find("near"), Is.Null);
        Assert.That(_loot.Find("middle").Position.x, Is.EqualTo(40f));
        _loot.TickCollection(1.6f);
        Assert.That(_loot.Find("middle"), Is.Null);
        Assert.That(_loot.Find("loot-1").Position.x, Is.EqualTo(50f));
        _loot.TickCollection(0.5f);
        Assert.That(_loot.Find("loot-1").Position.x, Is.EqualTo(37.5f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ActivePullFinishesBeforeNewCloserLootAndCancellationReleasesQueue(bool destroyActive)
    {
        BeginPull(false);
        _loot.TickCollection(1f);
        _loot.Add(new SpaceLootRuntimeState
        {
            LootId = "new-near", SystemId = "system-1", Kind = SpaceLootKind.Diamonds,
            Quantity = 1, CurrentHealth = 1, Position = new Vector3(10f, 0f, 0f)
        });
        Assert.That(_loot.TryTogglePlayerCollectionMark("new-near", out _), Is.True);
        _loot.TickCollection(0.2f);
        Assert.That(_loot.Find("loot-1").Position.x, Is.EqualTo(20f));
        Assert.That(_loot.Find("new-near").Position.x, Is.EqualTo(10f));
        Assert.That(_loot.Find("new-near").PullCollectorId, Is.Null);
        if (destroyActive) Assert.That(_loot.Remove("loot-1"), Is.True);
        else Assert.That(_loot.TryTogglePlayerCollectionMark("loot-1", out _), Is.True);
        _loot.TickCollection(0.2f);
        Assert.That(_loot.Find("new-near").Position.x, Is.EqualTo(5f));
        Assert.That(_loot.Find("new-near").PullCollectorId, Is.EqualTo("player"));
    }

    [Test]
    public void OverloadFeedbackExplainsWhyFlightIsBlocked()
    {
        string message = null;
        Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>()
            .Subscribe<SpaceLootFeedbackEvent2A>(e => message = e.Message);
        Assert.That(_loot.TryDebugAddAstralite(out _), Is.True);
        Assert.That(PlayerCargoOverload2A.RejectRouteIfOverloaded(), Is.True);
        Assert.That(message, Is.EqualTo("Трюм переполнен. Полет невозможен"));
        Assert.That(_loot.TryDebugRemoveAstralite(out _), Is.True);
        Assert.That(PlayerCargoOverload2A.RejectRouteIfOverloaded(), Is.False);
    }

    [Test]
    public void CargoPanelUsesPlanetStyleGroupsGoodsAndKeepsEquipmentSeparate()
    {
        GameObject canvasHost = CreateCargoHud(out var cargo, out var template, out var bottom);
        try
        {
            ShipRuntimeData ship = _state.Player.GetActiveShip();
            ship.CargoCapacity = 100;
            ship.Cargo.AddLootItem(_astralite.Id, 3);
            ship.Cargo.entries.Add(new RuntimeCargoEntryData(_astralite.Id, 2)
                { MissionRuntimeId = "mission-cargo" });
            _heavyWeapon = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponConfig>(
                "Assets/_Project/Content/Configs/Weapons/AI/weapon_ai_core_bolt_silver_L10_01.asset");
            Assert.That(_heavyWeapon, Is.Not.Null);
            foreach (string id in new[] { "weapon-instance-1", "weapon-instance-2" })
                ship.CargoEquipment.Add(new SystemNpcWeaponRuntimeState
                    { InstanceId = id, WeaponConfigId = _heavyWeapon.Id });
            cargo.Toggle();
            Transform panel = canvasHost.transform.Find("PlayerCargoPanel");
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.GetComponent<Image>().sprite, Is.SameAs(template.GetComponent<Image>().sprite));
            Assert.That(panel.GetComponent<Image>().color, Is.EqualTo(template.GetComponent<Image>().color));
            Assert.That(panel.Find("HeaderBackground").GetComponent<Image>().color,
                Is.EqualTo(template.Find("HeaderBackground").GetComponent<Image>().color));
            Transform cards = panel.Find("Viewport/Cards");
            Assert.That(cards.childCount, Is.EqualTo(3));
            Assert.That(panel.Find("HeaderText").GetComponent<TMP_Text>().text, Is.EqualTo("ТРЮМ (33 / 100)"));
            Assert.That(panel.Find("CargoSummary"), Is.Null);
            Assert.That(panel.Find("SelectedCargoVolume"), Is.Null);
            Assert.That(cards.Find("CargoCard_0/Count").GetComponent<TMP_Text>().text, Is.EqualTo("Кол-во 5"));
            Assert.That(cards.Find("CargoCard_0/Origin").GetComponent<TMP_Text>().text, Is.EqualTo("Древние"));
            Assert.That(cards.Find("CargoCard_0/Name").GetComponent<TMP_Text>().text, Is.EqualTo("Астралит"));
            TMP_Text rowStyle = template.Find("RowsText").GetComponent<TMP_Text>();
            foreach (string part in new[] { "Origin", "Name", "Count", "Volume" })
            {
                TMP_Text text = cards.Find("CargoCard_0/" + part).GetComponent<TMP_Text>();
                Assert.That(text.font, Is.SameAs(rowStyle.font));
                Assert.That(text.fontSharedMaterial, Is.SameAs(rowStyle.fontSharedMaterial));
                Assert.That(text.fontSize, Is.EqualTo(rowStyle.fontSize));
                Assert.That(text.fontStyle, Is.EqualTo(rowStyle.fontStyle));
            }
            foreach (string part in new[] { "SelectedCargoSpace", "Quantity",
                         "Less/Label", "More/Label", "Drop/Label", "Close/Label" })
            {
                TMP_Text text = panel.Find(part).GetComponent<TMP_Text>();
                Assert.That(text.font, Is.SameAs(rowStyle.font), part);
                Assert.That(text.fontSharedMaterial, Is.SameAs(rowStyle.fontSharedMaterial), part);
                Assert.That(text.fontSize, Is.EqualTo(rowStyle.fontSize), part);
                Assert.That(text.fontStyle, Is.EqualTo(rowStyle.fontStyle), part);
            }
            Assert.That(cards.Find("CargoCard_0/Volume").GetComponent<RectTransform>().anchorMax,
                Is.EqualTo(Vector2.one));
            Assert.That(cards.Find("CargoCard_0/Icon").GetComponent<Image>().sprite, Is.SameAs(_astralite.Icon));
            Assert.That(cards.Find("CargoCard_0/Volume").GetComponent<TMP_Text>().text,
                Is.EqualTo("5"));
            for (int i = 1; i <= 2; i++)
            {
                Assert.That(cards.Find("CargoCard_" + i + "/Count").GetComponent<TMP_Text>().text, Is.EqualTo("Кол-во 1"));
                Assert.That(cards.Find("CargoCard_" + i + "/Volume").GetComponent<TMP_Text>().text, Is.EqualTo("14"));
                Assert.That(cards.Find("CargoCard_" + i + "/Icon").GetComponent<Image>().sprite,
                    Is.SameAs(_heavyWeapon.Icon));
            }
            Assert.That(panel.Find("Close").GetComponent<RectTransform>().rect.height,
                Is.EqualTo(panel.Find("HeaderBackground").GetComponent<RectTransform>().rect.height));
            Assert.That(panel.Find("Close/Icon").GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(panel.Find("Close/Icon").GetComponent<Image>().preserveAspect, Is.True);
            Assert.That(cards.Find("CargoCard_0/Origin").GetComponent<RectTransform>().anchoredPosition.x,
                Is.Zero);
            Assert.That(cards.Find("CargoCard_0/Origin").GetComponent<TMP_Text>().alignment,
                Is.EqualTo(TextAlignmentOptions.Center));
            Assert.That(panel.Find("Drop").GetComponent<RectTransform>().anchoredPosition.y,
                Is.EqualTo(panel.Find("Quantity").GetComponent<RectTransform>().anchoredPosition.y));
            Assert.That(panel.Find("Drop").GetComponent<RectTransform>().rect.height, Is.EqualTo(48f));
            var panelCorners = new Vector3[4];
            var bottomCorners = new Vector3[4];
            panel.GetComponent<RectTransform>().GetWorldCorners(panelCorners);
            bottom.GetWorldCorners(bottomCorners);
            Assert.That(panelCorners[0].y - bottomCorners[1].y, Is.EqualTo(8f).Within(0.01f));
            Assert.That(panel.GetComponent<RectTransform>().rect.width,
                Is.EqualTo(canvasHost.GetComponent<RectTransform>().rect.width - 32f));
        }
        finally { cargo.Close(); Object.DestroyImmediate(canvasHost); }
    }

    [Test]
    public void CargoCardsStayInOneRowAndScrollRightWhenTheyOverflow()
    {
        GameObject canvasHost = CreateCargoHud(out var cargo, out _, out _);
        try
        {
            for (int i = 0; i < 8; i++)
                _state.Player.GetActiveShip().CargoEquipment.Add(new SystemNpcWeaponRuntimeState
                    { InstanceId = "row-equipment-" + i, WeaponConfigId = "test-weapon" });
            cargo.Toggle();
            Transform viewport = canvasHost.transform.Find("PlayerCargoPanel/Viewport");
            ScrollRect scroll = viewport.GetComponent<ScrollRect>();
            Assert.That(scroll.horizontal, Is.True);
            Assert.That(scroll.vertical, Is.False);
            Assert.That(scroll.horizontalScrollbar, Is.Not.Null);
            Assert.That(scroll.content.rect.width, Is.GreaterThan(scroll.viewport.rect.width));
            float y = scroll.content.Find("CargoCard_0").GetComponent<RectTransform>().anchoredPosition.y;
            float previousX = float.NegativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                RectTransform card = scroll.content.Find("CargoCard_" + i).GetComponent<RectTransform>();
                Assert.That(card.anchoredPosition.y, Is.EqualTo(y));
                Assert.That(card.anchoredPosition.x, Is.GreaterThan(previousX));
                previousX = card.anchoredPosition.x;
            }
            scroll.horizontalNormalizedPosition = 1f;
            Assert.That(scroll.content.anchoredPosition.x, Is.LessThan(0f),
                "Scrolling right must reveal the later cards.");
        }
        finally { cargo.Close(); Object.DestroyImmediate(canvasHost); }
    }

    [Test]
    public void CargoQuantityButtonsRespectStackLimitsAndEquipmentIsAlwaysOne()
    {
        GameObject canvasHost = CreateCargoHud(out var cargo, out _, out _);
        try
        {
            _state.Player.GetActiveShip().Cargo.AddLootItem(_astralite.Id, 5);
            _state.Player.GetActiveShip().CargoEquipment.Add(new SystemNpcWeaponRuntimeState
                { InstanceId = "single-equipment", WeaponConfigId = "test-weapon" });
            cargo.Toggle();
            Transform panel = canvasHost.transform.Find("PlayerCargoPanel");
            Button less = panel.Find("Less").GetComponent<Button>();
            Button more = panel.Find("More").GetComponent<Button>();
            Button drop = panel.Find("Drop").GetComponent<Button>();
            Assert.That(less.interactable, Is.False);
            Assert.That(more.interactable, Is.True);
            Assert.That(drop.interactable, Is.True);
            Assert.That(panel.Find("Quantity").GetComponent<TMP_Text>().text, Is.EqualTo("1 / 5"));
            Assert.That(panel.Find("Viewport/Cards/CargoCard_0").GetComponent<Outline>().enabled, Is.True);
            panel.Find("Viewport/Cards/CargoCard_0").GetComponent<Button>().onClick.Invoke();
            Assert.That(less.interactable, Is.False);
            Assert.That(more.interactable, Is.True);
            for (int i = 0; i < 4; i++) more.onClick.Invoke();
            Assert.That(panel.Find("Quantity").GetComponent<TMP_Text>().text, Is.EqualTo("5 / 5"));
            Assert.That(more.interactable, Is.False);
            Assert.That(less.interactable, Is.True);
            less.onClick.Invoke();
            Assert.That(panel.Find("Quantity").GetComponent<TMP_Text>().text, Is.EqualTo("4 / 5"));
            Assert.That(more.interactable, Is.True);
            Assert.That(panel.Find("SelectedCargoSpace").GetComponent<TMP_Text>().text,
                Is.EqualTo("места в трюме 4 ед"));
            panel.Find("Viewport/Cards/CargoCard_1").GetComponent<Button>().onClick.Invoke();
            Assert.That(panel.Find("Quantity").GetComponent<TMP_Text>().text, Is.EqualTo("1 / 1"));
            Assert.That(less.interactable || more.interactable, Is.False);
            Assert.That(drop.interactable, Is.True);
        }
        finally { cargo.Close(); Object.DestroyImmediate(canvasHost); }
    }

    [Test]
    public void CargoPanelSuppressesCombatListsUntilClosedWithoutChangingCollapsedState()
    {
        GameObject canvasHost = CreateCargoHud(out var cargo, out _, out _);
        var weaponsHost = new GameObject("WeaponHudTest");
        var threatsHost = new GameObject("ThreatHudTest");
        weaponsHost.SetActive(false);
        threatsHost.SetActive(false);
        try
        {
            var weapons = weaponsHost.AddComponent<SystemPlayerWeaponListHud2A>();
            var threats = threatsHost.AddComponent<SystemPlayerThreatListHud2A>();
            var weaponsPanel = new GameObject("WeaponsPanel");
            weaponsPanel.transform.SetParent(weaponsHost.transform);
            var threatsPanel = new GameObject("ThreatsPanel");
            threatsPanel.transform.SetParent(threatsHost.transform);
            SetPrivateField(weapons, "panelRoot", weaponsPanel);
            SetPrivateField(threats, "panelRoot", threatsPanel);
            SetPrivateField(weapons, "isCollapsed", true);
            SetPrivateField(cargo, "_weaponHuds", new[] { weapons });
            SetPrivateField(cargo, "_threatHuds", new[] { threats });
            cargo.Toggle();
            foreach (var hud in new MonoBehaviour[] { weapons, threats })
                hud.GetType().GetMethod("SetPanelVisible", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(hud, new object[] { true });
            Assert.That(weaponsPanel.activeSelf || threatsPanel.activeSelf, Is.False,
                "Regular HUD refreshes must not reopen the combat lists over cargo.");
            cargo.Close();
            foreach (var hud in new MonoBehaviour[] { weapons, threats })
                hud.GetType().GetMethod("SetPanelVisible", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(hud, new object[] { true });
            Assert.That(weaponsPanel.activeSelf && threatsPanel.activeSelf, Is.True);
            Assert.That(typeof(SystemPlayerWeaponListHud2A)
                .GetField("isCollapsed", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(weapons), Is.EqualTo(true));
        }
        finally
        {
            cargo.Close();
            Object.DestroyImmediate(weaponsHost);
            Object.DestroyImmediate(threatsHost);
            Object.DestroyImmediate(canvasHost);
        }
    }

    [Test]
    public void CargoSampleCommandsProvideDistinctMutedColorsForAllOrigins()
    {
        Assert.That(_loot.TryDebugAddEnemyEquipment(out _), Is.True);
        Assert.That(_loot.TryDebugAddNpcEquipment(out _), Is.True);
        Assert.That(_loot.TryDebugAddEveryAstralite(out _), Is.True);
        Assert.That(_loot.TryDebugAddCommodity(out _), Is.True);
        var equipment = _state.Player.GetActiveShip().CargoEquipment;
        Assert.That(equipment.Count, Is.EqualTo(4));
        var instanceIds = new System.Collections.Generic.HashSet<string>();
        foreach (var item in equipment)
        {
            Assert.That(instanceIds.Add(item.InstanceId), Is.True);
            Assert.That(item.HasLootOrigin, Is.True);
        }
        Assert.That(equipment[3].LootOriginNpcType, Is.EqualTo(SystemNpcType.Ally));
        foreach (string id in new[] { "item_astralite_ai_01", "item_astralite_ancients_01",
                     "item_astralite_infected_01", "item_ore_01" })
            Assert.That(_inventory.GetItemCount(id), Is.EqualTo(1));
        GameObject canvasHost = CreateCargoHud(out var cargo, out _, out _);
        try
        {
            cargo.Toggle();
            Transform cards = canvasHost.transform.Find("PlayerCargoPanel/Viewport/Cards");
            Assert.That(cards.childCount, Is.EqualTo(8));
            string[] origins = { "ИИ", "Древние", "Заражённые", "Товар", "ИИ", "Древние", "Заражённые", "Союзники" };
            for (int i = 0; i < origins.Length; i++)
                Assert.That(cards.Find("CargoCard_" + i + "/Origin").GetComponent<TMP_Text>().text, Is.EqualTo(origins[i]));
            typeof(SystemPlayerCargoPanel2A).GetMethod("ClearSelection", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cargo, null);
            typeof(SystemPlayerCargoPanel2A).GetMethod("UpdateSelection", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cargo, null);
            var backgrounds = new Color[8];
            for (int i = 0; i < backgrounds.Length; i++)
            {
                backgrounds[i] = cards.Find("CargoCard_" + i).GetComponent<Image>().color;
                Assert.That(Mathf.Max(backgrounds[i].r, backgrounds[i].g, backgrounds[i].b), Is.LessThan(0.4f));
                Color header = cards.Find("CargoCard_" + i + "/CardHeader").GetComponent<Image>().color;
                Assert.That(header.r, Is.LessThan(backgrounds[i].r));
                Assert.That(header.g, Is.LessThan(backgrounds[i].g));
                Assert.That(header.b, Is.LessThan(backgrounds[i].b));
            }
            for (int i = 0; i < 3; i++)
            {
                Assert.That(backgrounds[i], Is.EqualTo(backgrounds[i + 4]),
                    "Enemy equipment and astralite must share their species tint.");
                Assert.That(equipment[i].LootOriginNpcType, Is.EqualTo(SystemNpcType.Enemy));
            }
            Assert.That(backgrounds[0], Is.Not.EqualTo(backgrounds[1]));
            Assert.That(backgrounds[1], Is.Not.EqualTo(backgrounds[2]));
            Assert.That(backgrounds[3].g, Is.GreaterThan(backgrounds[3].r).And.GreaterThan(backgrounds[3].b));
            Assert.That(backgrounds[7].b, Is.GreaterThan(backgrounds[7].r).And.GreaterThan(backgrounds[7].g));
            cards.Find("CargoCard_1").GetComponent<Button>().onClick.Invoke();
            Color selected = cards.Find("CargoCard_1").GetComponent<Image>().color;
            Assert.That(selected.r, Is.GreaterThan(backgrounds[1].r));
            Assert.That(cards.Find("CargoCard_0").GetComponent<Image>().color, Is.EqualTo(backgrounds[0]));
        }
        finally { cargo.Close(); Object.DestroyImmediate(canvasHost); }
    }

    [Test]
    public void EquipmentOriginSurvivesCargoTransferCopySerializationAndPlayerEjection()
    {
        const string weaponId = "weapon_common_laser_silver_L01_01";
        _loot.Add(new SpaceLootRuntimeState
        {
            LootId = "enemy-origin-weapon", SystemId = "system-1", CurrentHealth = 1,
            Position = new Vector3(20f, 0f, 0f), Kind = SpaceLootKind.Equipment, Quantity = 1,
            EquipmentConfigId = weaponId, SourceNpcType = SystemNpcType.Enemy,
            SourceNpcConfigId = "enemy_ai_L01_01",
            Equipment = new SystemNpcWeaponRuntimeState { InstanceId = "origin-instance", WeaponConfigId = weaponId }
        });
        SpaceLootRuntimeState copy = _loot.Find("enemy-origin-weapon").Copy();
        copy = JsonUtility.FromJson<SpaceLootRuntimeState>(JsonUtility.ToJson(copy));
        Assert.That(copy.Equipment.HasLootOrigin, Is.True);
        Assert.That(copy.Equipment.LootOriginNpcConfigId, Is.EqualTo("enemy_ai_L01_01"));
        Assert.That(_loot.TryCollectForPlayer("enemy-origin-weapon", out _), Is.True);
        Assert.That(_state.Player.GetActiveShip().CargoEquipment[0].HasLootOrigin, Is.True);
        Assert.That(_loot.TryDropPlayerEquipment("origin-instance", out _), Is.True);
        SpaceLootRuntimeState ejected = null;
        foreach (SpaceLootRuntimeState item in _loot.Loot)
            if (item.Equipment?.InstanceId == "origin-instance") ejected = item;
        Assert.That(ejected, Is.Not.Null);
        Assert.That(ejected.Equipment.HasLootOrigin, Is.True);
        Assert.That(ejected.Equipment.LootOriginNpcType, Is.EqualTo(SystemNpcType.Enemy));
        Assert.That(ejected.Equipment.LootOriginNpcConfigId, Is.EqualTo("enemy_ai_L01_01"));
    }

    [Test]
    public void EjectionCompletesUsingRealFrameDeltasWithoutAnyCollectionTicks()
    {
        _loot.Add(new SpaceLootRuntimeState
        {
            LootId = "ejected", SystemId = "system-1", Kind = SpaceLootKind.Item,
            ItemConfigId = _astralite.Id, Quantity = 1, CurrentHealth = 1,
            Position = Vector3.zero, EjectionTargetPosition = new Vector3(100f, 0f, 0f),
            EjectionRemainingSeconds = 0.6f
        });
        _loot.TickEjection(0.3f);
        Assert.That(_loot.Find("ejected").Position.x, Is.EqualTo(50f).Within(0.001f));
        _loot.TickEjection(0.3f);
        Assert.That(_loot.Find("ejected").Position.x, Is.EqualTo(100f));
        Assert.That(_loot.Find("ejected").EjectionRemainingSeconds, Is.Zero);
        _loot.TickEjection(1f);
        Assert.That(_loot.Find("ejected").Position.x, Is.EqualTo(100f));
    }

    [Test]
    public void CargoPanelClosesForRouteStartAndManualMovement()
    {
        GameObject canvasHost = CreateCargoHud(out var cargo, out _, out _);
        try
        {
            cargo.Toggle();
            GameObject panel = canvasHost.transform.Find("PlayerCargoPanel").gameObject;
            Assert.That(panel.activeSelf, Is.True);
            Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>().Publish(
                new SystemTravelStartedEvent(TravelDestinationType.MapPoint, Vector2.zero, Vector2.one));
            Assert.That(panel.activeSelf, Is.False);
            cargo.Toggle();
            Assert.That(panel.activeSelf, Is.True);
            _movementState.SetVelocity(Vector2.one, Vector2.one);
            typeof(SystemPlayerCargoPanel2A).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cargo, null);
            Assert.That(panel.activeSelf, Is.False);
        }
        finally { cargo.Close(); Object.DestroyImmediate(canvasHost); }
    }

    [Test]
    public void CargoCardVolumeFitsFourDigitsAndIsRightAligned()
    {
        GameObject canvasHost = CreateCargoHud(out var cargo, out _, out _);
        try
        {
            _state.Player.GetActiveShip().Cargo.AddLootItem(_astralite.Id, 9999);
            cargo.Toggle();
            TMP_Text count = canvasHost.transform.Find("PlayerCargoPanel/Viewport/Cards/CargoCard_0/Volume")
                .GetComponent<TMP_Text>();
            Assert.That(count.text, Is.EqualTo("9999"));
            Assert.That(count.alignment, Is.EqualTo(TextAlignmentOptions.Right));
            Assert.That(count.rectTransform.pivot, Is.EqualTo(Vector2.one));
            Assert.That(count.rectTransform.anchoredPosition.x, Is.EqualTo(-8f));
            Assert.That(count.rectTransform.rect.width, Is.GreaterThanOrEqualTo(count.GetPreferredValues("9999").x));
        }
        finally { cargo.Close(); Object.DestroyImmediate(canvasHost); }
    }

    private WeaponConfig LoadTestWeapon(string id)
    {
        if (_heavyWeapon != null && _heavyWeapon.Id == id) return _heavyWeapon;
        string folder = id.StartsWith("weapon_ai_") ? "AI" : id.StartsWith("weapon_ancient_") ? "Ancients"
            : id.StartsWith("weapon_infected_") ? "Infected" : id.StartsWith("weapon_common_") ? "Common" : null;
        return folder == null ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponConfig>(
            "Assets/_Project/Content/Configs/Weapons/" + folder + "/" + id + ".asset");
    }

    private static EnemyConfig LoadTestEnemy(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        string folder = id.StartsWith("enemy_ai_") ? "AI" : id.StartsWith("enemy_ancients_") ? "Ancients"
            : id.StartsWith("enemy_infected_") ? "Infected" : null;
        return folder == null ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyConfig>(
            "Assets/_Project/Content/Configs/Population/Enemies/" + folder + "/" + id + ".asset");
    }

    private GameObject CreateCargoHud(out SystemPlayerCargoPanel2A cargo,
        out Transform template, out RectTransform bottom)
    {
        var canvasHost = new GameObject("CargoTestCanvas", typeof(RectTransform), typeof(Canvas));
        canvasHost.SetActive(false);
        canvasHost.GetComponent<RectTransform>().sizeDelta = new Vector2(1400f, 900f);
        var objectsHost = new GameObject("CargoObjectsHudTest", typeof(RectTransform));
        objectsHost.transform.SetParent(canvasHost.transform, false);
        var objectsHud = objectsHost.AddComponent<SystemObjectsListHud2A>();
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/Hud/PF_HudTopRoot.prefab");
        Assert.That(prefab, Is.Not.Null);
        template = Object.Instantiate(prefab.transform.Find("SystemObjectsListPanel").gameObject,
            objectsHost.transform, false).transform;
        template.name = "SystemObjectsListPanel";
        var bottomHost = new GameObject("CargoTestBottomHud", typeof(RectTransform));
        bottomHost.transform.SetParent(canvasHost.transform, false);
        bottom = bottomHost.GetComponent<RectTransform>();
        bottom.anchorMin = Vector2.zero;
        bottom.anchorMax = Vector2.right;
        bottom.pivot = new Vector2(0.5f, 0f);
        bottom.sizeDelta = new Vector2(0f, 150f);
        bottom.anchoredPosition = Vector2.zero;
        bottomHost.AddComponent<SystemOffscreenEnemyIndicatorsHud2A>();
        Bootstrapper.Instance.ServiceRegistry.Register(_loot);
        cargo = _host.AddComponent<SystemPlayerCargoPanel2A>();
        SetPrivateField(cargo, "panelStyleSource", objectsHud);
        SetPrivateField(cargo, "bottomHudPanel", bottom);
        SetPrivateField(cargo, "_weaponHuds", new SystemPlayerWeaponListHud2A[0]);
        SetPrivateField(cargo, "_threatHuds", new SystemPlayerThreatListHud2A[0]);
        return canvasHost;
    }

    private static void SetPrivateField(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private void BeginPull(bool ranger)
    {
        if (ranger)
        {
            Assert.That(_loot.TryPullTowardNpc("loot-1", _ranger, 25f, out bool arrived), Is.True);
            Assert.That(arrived, Is.False);
        }
        else Assert.That(_loot.TryTogglePlayerCollectionMark("loot-1", out _), Is.True);
    }

    [Test]
    public void DebugCargoCommandsAddFiveBeyondCapacityAndClearAllHoldContents()
    {
        ShipRuntimeData ship = _state.Player.GetActiveShip();
        ship.CargoCapacity = 2;
        int notifications = 0;
        int reportedMass = -1;
        Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>().Subscribe<CargoChangedEvent>(e =>
        {
            notifications++;
            reportedMass = e.UsedCargo;
        });
        int credits = _state.Player.Credits;
        int diamonds = _state.Player.Diamonds;
        ship.EquippedWeaponIds.Add("installed-weapon");

        Assert.That(_loot.TryDebugAddAstralite(out _), Is.True);
        Assert.That(_inventory.GetItemCount(_astralite.Id), Is.EqualTo(5));
        Assert.That(_inventory.GetFreeCargo(), Is.EqualTo(-3));
        Assert.That(notifications, Is.EqualTo(1));
        Assert.That(reportedMass, Is.EqualTo(5));
        ship.Cargo.entries.Add(new RuntimeCargoEntryData(_astralite.Id, 1)
            { MissionRuntimeId = "mission-1" });
        ship.CargoEquipment.Add(new SystemNpcWeaponRuntimeState { InstanceId = "cargo-weapon" });

        Assert.That(_loot.TryDebugClearPlayerCargo(out _), Is.True);
        Assert.That(_inventory.GetItems(), Is.Empty);
        Assert.That(ship.CargoEquipment, Is.Empty);
        Assert.That(_inventory.GetFreeCargo(), Is.EqualTo(2));
        Assert.That(notifications, Is.EqualTo(2));
        Assert.That(reportedMass, Is.Zero);
        Assert.That(ship.EquippedWeaponIds, Is.EqualTo(new[] { "installed-weapon" }));
        Assert.That(_state.Player.Credits, Is.EqualTo(credits));
        Assert.That(_state.Player.Diamonds, Is.EqualTo(diamonds));
    }

    [TestCase("Weapons/Ancients/weapon_ancient_shard_volley_silver_L10_01.asset", "Осколковый залп")]
    [TestCase("Weapons/AI/weapon_ai_core_bolt_silver_L10_01.asset", "Энергетическое ядро")]
    public void HeavyEquipmentErrorUsesRussianAssetDisplayName(string path, string expectedName)
    {
        _heavyWeapon = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponConfig>(
            "Assets/_Project/Content/Configs/" + path);
        Assert.That(_heavyWeapon, Is.Not.Null);
        _loot.Add(new SpaceLootRuntimeState
        {
            LootId = "heavy-1", SystemId = "system-1", Kind = SpaceLootKind.Equipment,
            EquipmentConfigId = _heavyWeapon.Id, Quantity = 1, CurrentHealth = 1
        });
        Assert.That(_loot.TryTogglePlayerCollectionMark("heavy-1", out string error), Is.False);
        Assert.That(error, Is.EqualTo("Трофей «" + expectedName + "»\nслишком тяжёлый"));
        Assert.That(System.Text.RegularExpressions.Regex.IsMatch(error, "[A-Za-z]"), Is.False);
        Assert.That(_loot.IsPlayerCollectionMarked("heavy-1"), Is.False);
    }

    [Test]
    public void DebugRemoveAndRestoreGrabberCancelsPullAndGatesMarking()
    {
        BeginPull(false);
        _loot.TickCollection(0.25f);
        Vector3 position = _loot.Find("loot-1").Position;
        Assert.That(_loot.TrySetPlayerGrabberEnabled(false, out _), Is.True);
        Assert.That(_loot.IsPlayerCollectionMarked("loot-1"), Is.False);
        Assert.That(_loot.Find("loot-1").PullCollectorId, Is.Null);
        Assert.That(_loot.TryTogglePlayerCollectionMark("loot-1", out string error), Is.False);
        Assert.That(error, Is.EqualTo("На корабле не установлен захват. Сбор невозможен"));
        Assert.That(_loot.TryCollectForPlayer("loot-1", out _), Is.False);
        _loot.TickCollection(0.5f);
        Assert.That(_loot.Find("loot-1").Position, Is.EqualTo(position));
        Assert.That(_loot.TrySetPlayerGrabberEnabled(true, out _), Is.True);
        Assert.That(_loot.IsPlayerCollectionMarked("loot-1"), Is.False,
            "Restoring a grabber must not silently restore cancelled marks.");
        Assert.That(_loot.TryTogglePlayerCollectionMark("loot-1", out _), Is.True);
        _loot.TickCollection(0.25f);
        Assert.That(_loot.Find("loot-1").Position.x, Is.LessThan(position.x));
    }

    [Test]
    public void RemovingPlayerGrabberDoesNotChangeSharedConfigOrRangerGrabber()
    {
        Assert.That(_loot.TrySetPlayerGrabberEnabled(false, out _), Is.True);
        Assert.That(_ally.Grabber, Is.SameAs(_grabber));
        BeginPull(true);
        _loot.TickCollection(0.25f);
        Assert.That(_loot.Find("loot-1").Position.x, Is.EqualTo(43.75f).Within(0.001f));
    }

    private static object DefaultValue(MethodInfo method) =>
        method.ReturnType == typeof(void) || !method.ReturnType.IsValueType
            ? null : Activator.CreateInstance(method.ReturnType);
}
