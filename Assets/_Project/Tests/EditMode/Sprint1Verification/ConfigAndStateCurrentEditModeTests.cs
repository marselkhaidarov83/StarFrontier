using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace StarFrontier.Tests.Sprint1
{
    public sealed class ConfigAndStateCurrentEditModeTests
    {
        private Sprint1BootstrapFixture _bootstrap;
        private readonly List<UnityEngine.Object> _assets = new();

        [SetUp]
        public void SetUp()
        {
            _bootstrap = new Sprint1BootstrapFixture();
            _bootstrap.Registry.Register<IGameSessionService>(new TestGameSessionService());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object asset in _assets) UnityEngine.Object.DestroyImmediate(asset);
            _bootstrap.Dispose();
        }

        [Test]
        public void Lookup_TrimsIdAndUsesOrdinalComparison()
        {
            ItemConfig item = Item("alpha");
            ConfigService service = CreateService(new[] { item });
            Assert.That(service.GetItemConfigById(" alpha "), Is.SameAs(item));
            Assert.That(service.GetItemConfigById("ALPHA"), Is.Null);
        }

        [Test]
        public void EmptyAndDuplicateIds_AreSkippedWithDiagnostics()
        {
            ItemConfig first = Item("same");
            ItemConfig duplicate = Item("same");
            ItemConfig empty = Item(" ");
            LogAssert.Expect(LogType.Warning,
                "ConfigService: duplicate ItemConfig Id 'same' was skipped.");
            LogAssert.Expect(LogType.Warning,
                "ConfigService: ItemConfig with empty Id was skipped.");

            ConfigService service = CreateService(new[] { first, duplicate, empty });
            Assert.That(service.GetAllItems(), Has.Count.EqualTo(1));
            Assert.That(service.GetItemConfigById("same"), Is.SameAs(first));
        }

        [Test]
        public void ReturnedCollections_DoNotExposeMutableBackingLists()
        {
            ConfigService service = CreateService(new[] { Item("one") });
            Assert.That(service.GetAllItems() is IList<ItemConfig>, Is.False,
                "IReadOnlyList must not expose its mutable backing List by a cast.");
        }

        [Test]
        public void GameRuntimeState_JsonRoundTripPreservesCoreValues()
        {
            var source = new GameRuntimeState();
            source.Meta.SaveDataVersion = SaveDataVersions.Current;
            source.Player.PlayerId = "qa-player";
            source.Player.Credits = 4321;
            source.Player.CurrentSystemId = "system-alpha";
            GameRuntimeState restored = JsonUtility.FromJson<GameRuntimeState>(
                JsonUtility.ToJson(source));
            Assert.That(restored.Meta.SaveDataVersion, Is.EqualTo(SaveDataVersions.Current));
            Assert.That(restored.Player.PlayerId, Is.EqualTo("qa-player"));
            Assert.That(restored.Player.Credits, Is.EqualTo(4321));
            Assert.That(restored.Player.CurrentSystemId, Is.EqualTo("system-alpha"));
        }

        [Test]
        public void PersistentStateGraph_HasNoUnityObjectReferences()
        {
            var violations = new List<string>();
            Scan(typeof(GameRuntimeState), "GameRuntimeState", new HashSet<Type>(), violations, 0);
            Assert.That(violations, Is.Empty,
                "Persistent state must use IDs instead of Unity object references:\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void Migration_RecreatesNullNestedBlocks()
        {
            var state = new GameRuntimeState
            {
                Meta = null,
                Player = null,
                Galaxy = null,
                Settings = null,
                MissionBlock = null
            };
            SaveMigrationService.Migrate(state);
            Assert.That(state.Meta, Is.Not.Null);
            Assert.That(state.Player, Is.Not.Null);
            Assert.That(state.Galaxy, Is.Not.Null);
            Assert.That(state.Settings, Is.Not.Null);
            Assert.That(state.MissionBlock, Is.Not.Null);
        }

        [Test]
        public void PersistentRoot_DoesNotEmbedSessionRuntimeStateTypes()
        {
            Type[] forbidden = { typeof(PlayerControlRuntimeState), typeof(ShipMovementRuntimeState),
                typeof(SystemCameraRuntimeState), typeof(TargetingRuntimeState), typeof(InteractionRuntimeState) };
            Type[] fields = typeof(GameRuntimeState).GetFields().Select(field => field.FieldType).ToArray();
            Assert.That(fields.Intersect(forbidden), Is.Empty);
        }

        private ConfigService CreateService(IEnumerable<ItemConfig> items)
        {
            return new ConfigService(null, null, null, null, null,
                null,
                Array.Empty<SectorConfig>(), Array.Empty<StarSystemConfig>(),
                Array.Empty<PlanetConfig>(), items, 
                Array.Empty<EnemyConfig>(), Array.Empty<AllyConfig>(),
                Array.Empty<AllySpawnRuleConfig>(), Array.Empty<PirateConfig>(),
                Array.Empty<PirateGroupSpawnRuleConfig>(), Array.Empty<ModuleConfig>(),
                Array.Empty<WeaponConfig>());
        }

        private ItemConfig Item(string id)
        {
            ItemConfig item = ScriptableObject.CreateInstance<ItemConfig>();
            _assets.Add(item);
            typeof(BaseConfig).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(item, id);
            return item;
        }

        private static void Scan(Type type, string path, ISet<Type> active,
            ICollection<string> violations, int depth)
        {
            if (type == null || depth > 10) return;
            if (type.IsArray) type = type.GetElementType();
            if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
            {
                Type[] arguments = type.GetGenericArguments();
                if (arguments.Length == 1) type = arguments[0];
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            { violations.Add(path + " -> " + type.FullName); return; }
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || active.Contains(type)) return;
            active.Add(type);
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                       BindingFlags.NonPublic)
                         .Where(field => field.IsPublic || field.IsDefined(typeof(SerializeField), true)))
                Scan(field.FieldType, path + "." + field.Name, active, violations, depth + 1);
            active.Remove(type);
        }
    }
}
