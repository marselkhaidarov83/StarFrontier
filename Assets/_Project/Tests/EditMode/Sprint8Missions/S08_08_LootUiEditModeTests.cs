using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class S08_08_LootUiEditModeTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void FirstClickBuildsRouteRegardlessOfCollectionMark(bool initiallyMarked)
    {
        Bootstrapper previousBootstrap = Bootstrapper.Instance;
        var host = new GameObject("LootSelectionTest");
        host.SetActive(false);
        try
        {
            var bootstrap = host.AddComponent<Bootstrapper>();
            Bootstrapper.Instance = bootstrap;
            var services = new ServiceRegistry();
            bootstrap.ServiceRegistry = services;
            var state = new GameRuntimeState
            {
                Player = new PlayerState { CurrentSystemId = "system-1" }
            };
            services.Register(S08MissionTestProxy.Make<IGameSessionService>((m, a) =>
                m.Name == "get_State" ? state : DefaultValue(m)));
            services.Register(new SimpleEventBus());
            services.Register(S08MissionTestProxy.Make<ISystemNpcRuntimeService>((m, a) => DefaultValue(m)));
            services.Register(S08MissionTestProxy.Make<IConfigService>((m, a) => DefaultValue(m)));
            services.Register(S08MissionTestProxy.Make<IInventoryService>((m, a) => DefaultValue(m)));
            services.Register(S08MissionTestProxy.Make<IShipMovementService>((m, a) => DefaultValue(m)));

            string selectedId = null;
            var targetService = S08MissionTestProxy.Make<ITargetService2A>((m, a) =>
            {
                if (m.Name == "TrySelectTarget")
                {
                    selectedId = (string)a[0];
                    a[6] = TargetSelectionFailReason2A.None;
                    return true;
                }
                return m.Name == "IsCurrentTarget" ? selectedId == (string)a[0] : DefaultValue(m);
            });
            services.Register(targetService);
            var destinations = new List<Vector3>();
            var travel = S08MissionTestProxy.Make<ISystemTravelService>((m, a) =>
            {
                if (m.Name == "SetMapPointDestination") destinations.Add((Vector3)a[0]);
                return DefaultValue(m);
            });
            services.Register(travel);
            var lootService = new SystemSpaceLootService();
            var position = new Vector3(200f, 75f, 0f);
            lootService.Add(new SpaceLootRuntimeState
            {
                LootId = "loot-1", SystemId = "system-1", Position = position,
                Kind = SpaceLootKind.Credits, Quantity = 1, CurrentHealth = 1
            });
            var marks = (HashSet<string>)typeof(SystemSpaceLootService)
                .GetField("_playerCollectionMarks", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(lootService);
            if (initiallyMarked) marks.Add("loot-1");

            var controller = host.AddComponent<SystemSpaceLootViewController>();
            SetControllerField(controller, "_lootService", lootService);
            SetControllerField(controller, "_gameSessionService", services.Get<IGameSessionService>());
            SetControllerField(controller, "_targetService", targetService);
            SetControllerField(controller, "_systemTravelService", travel);
            // Supply a panel so this test does not depend on a loaded HUD scene.
            var panel = new GameObject("LootPanel");
            panel.transform.SetParent(host.transform, false);
            panel.SetActive(false);
            SetControllerField(controller, "_panel", panel);

            controller.OpenLoot("loot-1");

            Assert.That(selectedId, Is.EqualTo("loot-1"));
            Assert.That(destinations, Is.EqualTo(new[] { position }));
            Assert.That(lootService.IsPlayerCollectionMarked("loot-1"), Is.EqualTo(initiallyMarked));
            if (initiallyMarked)
            {
                controller.OpenLoot("loot-1");
                Assert.That(lootService.IsPlayerCollectionMarked("loot-1"), Is.False);
                Assert.That(destinations.Count, Is.EqualTo(1),
                    "Toggling a collection mark must not rebuild the route.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            Bootstrapper.Instance = previousBootstrap;
        }
    }

    private static object DefaultValue(MethodInfo method) =>
        method.ReturnType == typeof(void) || !method.ReturnType.IsValueType
            ? null : Activator.CreateInstance(method.ReturnType);

    private static void SetControllerField(SystemSpaceLootViewController controller, string name, object value) =>
        typeof(SystemSpaceLootViewController)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, value);

    [Test]
    public void SelectingLootHidesPreviousMapPointMarkerWithoutClearingNewTarget()
    {
        var host = new GameObject("LootDestinationSelectionTest");
        host.SetActive(false);
        var planet = ScriptableObject.CreateInstance<PlanetConfig>();
        try
        {
            var markerController = host.AddComponent<SystemDestinationMarkerController2>();
            var point = new GameObject("OldMapPointMarker");
            point.transform.SetParent(host.transform, false);
            typeof(SystemDestinationMarkerController2)
                .GetField("mapPointDestinationObject", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(markerController, point);
            var controller = host.AddComponent<SystemMapDestinationController2>();
            var bus = new SimpleEventBus();
            int targetClears = 0;
            ITargetService2A targeting = S08MissionTestProxy.Make<ITargetService2A>((m, a) =>
            {
                if (m.Name == "ClearTarget") targetClears++;
                return DefaultValue(m);
            });
            foreach (var pair in new Dictionary<string, object>
            {
                { "_simpleEventBus", bus }, { "markerController", markerController },
                { "_targetService", targeting }, { "_selectedPlanetData", planet }
            })
                typeof(SystemMapDestinationController2)
                    .GetField(pair.Key, BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, pair.Value);
            typeof(SystemMapDestinationController2)
                .GetMethod("Subscribe", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);

            Assert.That(point.activeSelf, Is.True);
            bus.Publish(new TargetChangedEvent2A("loot-1", SystemGameplayTargetType.Loot, true));

            Assert.That(point.activeSelf, Is.False);
            Assert.That(typeof(SystemMapDestinationController2)
                .GetField("_selectedPlanetData", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller), Is.Null,
                "An old moving-planet marker must not reappear after selecting loot.");
            Assert.That(targetClears, Is.Zero, "The newly selected loot target must remain selected.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(planet);
        }
    }

    [Test]
    public void TractorBeamFollowsActivePullAndHidesOnCancellationOrDisable()
    {
        Material material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Art/SystemFrame/Materials/M_LootTractorBeam2A.mat");
        Assert.That(material, Is.Not.Null);
        Assert.That(material.HasProperty("_BeamPhase"), Is.True);
        var texture = new Texture2D(2, 2);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        var root = new GameObject("LootBeamTest");
        try
        {
            var view = root.AddComponent<SystemSpaceLootView>();
            Vector3 target = new Vector3(50f, 20f, -2f);
            view.Initialize("beam-loot", sprite, target, 28f, null, null, sprite);
            view.SetCollectionMarked(true);
            view.SetPullBeam(false, Vector3.zero, material, 0f);
            Assert.That(root.GetComponentInChildren<LineRenderer>(), Is.Null,
                "A collection mark alone must not create a tractor beam.");

            Vector3 origin = new Vector3(10f, 5f, -2f);
            view.SetPullBeam(true, origin, material, 2f);
            LineRenderer beam = root.GetComponentInChildren<LineRenderer>();
            Assert.That(beam, Is.Not.Null);
            Assert.That(beam.enabled, Is.True);
            Assert.That(beam.sharedMaterial, Is.SameAs(material));
            Assert.That(beam.GetPosition(0), Is.EqualTo(origin));
            Assert.That(beam.GetPosition(1), Is.EqualTo(target));
            Assert.That(beam.transform.lossyScale.x, Is.EqualTo(1f).Within(0.001f));
            var properties = new MaterialPropertyBlock();
            beam.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_BeamPhase"), Is.EqualTo(1.6f).Within(0.001f));

            root.transform.position = target * 0.5f;
            view.SetPullBeam(true, origin, material, 2f);
            Assert.That(beam.GetPosition(1), Is.EqualTo(root.transform.position));
            beam.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_BeamPhase"), Is.EqualTo(1.6f).Within(0.001f),
                "Flow animation must not advance while simulation time is frozen.");
            view.SetPullBeam(false, origin, material, 2f);
            Assert.That(beam.enabled, Is.False);
            Assert.That(root.transform.Find("CollectionMarker").GetComponent<SpriteRenderer>().enabled,
                Is.True, "Hiding the beam must not clear the collection mark.");
            view.SetPullBeam(true, origin, material, 2f);
            view.enabled = false;
            // Ordinary MonoBehaviour lifecycle callbacks are not automatically
            // dispatched by this synchronous EditMode test. Exercise the handler explicitly.
            typeof(SystemSpaceLootView)
                .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);
            Assert.That(beam.enabled, Is.False);
            view.SetPullBeam(true, origin, material, 2f);
            Assert.That(beam.enabled, Is.False,
                "The controller must not reopen a beam on a disabled loot view.");
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }
    }

    [TestCase(2f)]
    [TestCase(100f)]
    public void CollectionMarkerIsSeparateFromSelectionAndOutsideLootIcon(float pixelsPerUnit)
    {
        var texture = new Texture2D(2, 2);
        var sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f),
            new Vector2(0.5f, 0.5f), pixelsPerUnit);
        var root = new GameObject("LootViewTest");
        try
        {
            var view = root.AddComponent<SystemSpaceLootView>();
            view.Initialize("loot-1", sprite, Vector3.zero, 28f, null, null, sprite);
            Transform marker = root.transform.Find("CollectionMarker");
            Assert.That(marker, Is.Not.Null);
            SpriteRenderer badge = marker.GetComponent<SpriteRenderer>();
            SpriteRenderer lootRenderer = root.GetComponent<SpriteRenderer>();
            Assert.That(lootRenderer.sortingLayerName, Is.EqualTo(Pseudo3DRenderOrder2.ShipFxLayer));
            Assert.That(lootRenderer.sortingOrder, Is.GreaterThan(Pseudo3DRenderOrder2.Ship));
            Assert.That(badge.sortingLayerID, Is.EqualTo(lootRenderer.sortingLayerID));
            Assert.That(badge.sortingOrder, Is.GreaterThan(lootRenderer.sortingOrder));
            Bounds lootBounds = root.GetComponent<SpriteRenderer>().bounds;
            Assert.That(badge.sprite, Is.SameAs(sprite));
            Assert.That(badge.color, Is.EqualTo(Color.white));
            Assert.That(badge.bounds.size.x, Is.EqualTo(28f * 0.32f).Within(0.001f));
            Assert.That(badge.bounds.min.x - lootBounds.max.x,
                Is.EqualTo(28f * 0.02f).Within(0.001f));
            Assert.That(badge.bounds.min.y - lootBounds.max.y,
                Is.EqualTo(28f * 0.02f).Within(0.001f));
            Assert.That(badge.enabled, Is.False);

            view.SetCollectionMarked(true);
            Assert.That(marker.GetComponent<SpriteRenderer>().enabled, Is.True);
            view.SetCollectionMarked(false);
            Assert.That(marker.GetComponent<SpriteRenderer>().enabled, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }
    }
}
