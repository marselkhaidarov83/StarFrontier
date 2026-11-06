using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class StationInterfaceFlowEditModeTests
{
    private readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
    private Session _session;
    private StationService _stations;
    private StationCardService _cards;
    private StationConfig _station;
    private StarSystemConfig _config;
    private StationLevelEffectsConfig _effects;
    private StationPaymentOption _upgrade;
    private Vector2 _position;
    private StarSystemRuntimeState System => _session.State.Galaxy.Systems[0];
    private sealed class Session : IGameSessionService
    {
        public GameRuntimeState State { get; private set; } = new GameRuntimeState();
        public bool HasActiveSession => State != null;
        public void StartNewSession(GameRuntimeState state) { State = state; }
        public void LoadSession(GameRuntimeState state) { State = state; }
        public void ClearSession() { State = null; }
    }
    private T Asset<T>() where T : ScriptableObject { var asset = ScriptableObject.CreateInstance<T>(); _assets.Add(asset); return asset; }
    private static void Set(object target, string field, object value)
    {
        var type = target.GetType(); FieldInfo member = null;
        while (type != null && member == null) { member = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic); type = type.BaseType; }
        Assert.That(member, Is.Not.Null, field); member.SetValue(target, value);
    }
    private static StationPaymentCost[] Costs(int diamonds) => new[] { new StationPaymentCost { Resource = StationPaymentResource.Diamonds, Amount = diamonds } };
    [SetUp] public void Setup()
    {
        _position = Vector2.zero; _session = new Session();
        _session.State.Player.CurrentSystemId = "sys"; _session.State.Player.Diamonds = 100;
        var type = Asset<StationTypeConfig>(); _station = Asset<StationConfig>(); Set(_station, "id", "station");
        Set(_station, "stationTypeConfig", type); Set(_station, "initialLevel", 2);
        _effects = Asset<StationLevelEffectsConfig>(); _effects.Levels = new[] {
            new StationLevelEffectsSettings { Level = 2, RepairPaymentOptions = new[] { new StationInstantPaymentOption { Id = "pay", Costs = Costs(100) } },
                RecoveryPaymentOptions = new[] { new StationInstantPaymentOption { Id = "pay", Costs = Costs(20) } } },
            new StationLevelEffectsSettings { Level = 3, RequiredGalaxyLevel = 1 } };
        Set(_station, "levelEffects", _effects);
        _upgrade = new StationPaymentOption { Id = "pay", Costs = Costs(10), DurationSeconds = 20 };
        Set(_station, "upgradeSettings", new[] { new StationUpgradeSettings { TargetLevel = 3, PaymentOptions = new[] { _upgrade } } });
        Set(_station, "constructionPaymentOptions", new[] { new StationPaymentOption { Id = "pay", Costs = Costs(10), DurationSeconds = 5 } });
        _config = Asset<StarSystemConfig>(); Set(_config, "id", "sys"); Set(_config, "station", _station);
        _session.State.Galaxy.Systems.Add(new StarSystemRuntimeState { SystemId = "sys" });
        _stations = new StationService(_session, _ => _config, new SimpleEventBus(), new StationResourcePaymentGateway(_session)); _stations.InitializeSession(true);
        _cards = new StationCardService(_session, _stations, _ => _config, () => _position, _ => false);
    }
    [TearDown] public void Cleanup() { foreach (var asset in _assets) UnityEngine.Object.DestroyImmediate(asset); _assets.Clear(); }
    private StationActionConfirmation Prepare(StationOperationKind kind = StationOperationKind.Upgrade)
    {
        Assert.That(_cards.Prepare("sys", kind, "pay", out var confirmation, out string reason), Is.True, reason); return confirmation;
    }
    [Test] public void OpenAndPrepare_DoNotSpendOrStartOperation()
    {
        _cards.GetCard("sys"); _cards.GetOffers("sys"); Prepare();
        Assert.That(_session.State.Player.Diamonds, Is.EqualTo(100)); Assert.That(System.Station.Development, Is.Null);
    }
    [Test] public void DoubleConfirm_IsConsumedBeforePaymentCanRepeat()
    {
        var ticket = Prepare(); Assert.That(_cards.Confirm(ticket.TicketId, out _), Is.True);
        Assert.That(_cards.Confirm(ticket.TicketId, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("ConfirmationExpired")); Assert.That(_session.State.Player.Diamonds, Is.EqualTo(90));
    }
    [Test] public void Cancel_DoesNotSpendAnything()
    {
        var ticket = Prepare(); _cards.CancelConfirmation(); Assert.That(_cards.Confirm(ticket.TicketId, out _), Is.False);
        Assert.That(_session.State.Player.Diamonds, Is.EqualTo(100));
    }
    [Test] public void NewPreparation_InvalidatesEarlierTicket()
    {
        var old = Prepare(); var current = Prepare(); Assert.That(_cards.Confirm(old.TicketId, out _), Is.False);
        Assert.That(_cards.Confirm(current.TicketId, out _), Is.True); Assert.That(_session.State.Player.Diamonds, Is.EqualTo(90));
    }
    [Test] public void DamageAfterPreview_InvalidatesConfirmationWithoutSpending()
    {
        var ticket = Prepare(); System.ApplyInfrastructureDamageFromWar(10, false);
        Assert.That(_cards.Confirm(ticket.TicketId, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("StationStateChanged")); Assert.That(_session.State.Player.Diamonds, Is.EqualTo(100));
    }
    [Test] public void PriceEditAfterPreview_IsRejectedWithoutSpending()
    {
        var ticket = Prepare(); _upgrade.Costs[0].Amount = 30;
        Assert.That(_cards.Confirm(ticket.TicketId, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("QuoteChanged")); Assert.That(_session.State.Player.Diamonds, Is.EqualTo(100));
    }
    [Test] public void DurationEditAfterPreview_IsRejected()
    {
        var ticket = Prepare(); _upgrade.DurationSeconds = 1;
        Assert.That(_cards.Confirm(ticket.TicketId, out string reason), Is.False); Assert.That(reason, Is.EqualTo("QuoteChanged"));
    }
    [Test] public void MoneySpentElsewhereAfterPreview_IsRejected()
    {
        var ticket = Prepare(); _session.State.Player.Diamonds = 9;
        Assert.That(_cards.Confirm(ticket.TicketId, out string reason), Is.False); Assert.That(reason, Is.EqualTo("NotEnoughResources"));
        Assert.That(_session.State.Player.Diamonds, Is.EqualTo(9));
    }
    [Test] public void MutatingUiPreview_CannotAlterRealPrice()
    {
        var ticket = Prepare(); ticket.Offer.Quote.PaidCosts[0].Amount = 1;
        Assert.That(_cards.Confirm(ticket.TicketId, out _), Is.True); Assert.That(_session.State.Player.Diamonds, Is.EqualTo(90));
    }
    [Test] public void LeavingSystemAfterPreview_PreventsExecution()
    {
        var ticket = Prepare(); _session.State.Player.CurrentSystemId = "other";
        Assert.That(_cards.Confirm(ticket.TicketId, out _), Is.False); Assert.That(_session.State.Player.Diamonds, Is.EqualTo(100));
    }
    [Test] public void MovingAwayAfterRepairPreview_PreventsExecution()
    {
        System.ApplyInfrastructureDamageFromWar(135, false); var ticket = Prepare(StationOperationKind.Repair);
        _position = new Vector2(1000, 1000);
        Assert.That(_cards.Confirm(ticket.TicketId, out string reason), Is.False); Assert.That(reason, Is.EqualTo("ApproachRequired"));
        Assert.That(_session.State.Player.Diamonds, Is.EqualTo(100));
    }
    [Test] public void MaximumLevel_HasNoUpgradeOffer()
    { System.Station.Level = 10; Assert.That(_cards.GetOffers("sys").Any(x => x.Kind == StationOperationKind.Upgrade), Is.False); }
    [Test] public void InsufficientResources_ShowsExplicitUnavailableReason()
    {
        _session.State.Player.Diamonds = 0;
        var offer = _cards.GetOffers("sys").First(x => x.Kind == StationOperationKind.Upgrade);
        Assert.That(offer.UnavailableReason, Is.EqualTo("NotEnoughResources")); Assert.That(offer.Available, Is.False);
    }
    [Test] public void RequiredGalaxyLevel_IsShownBeforeConfirmation()
    {
        _effects.Levels[1].RequiredGalaxyLevel = 5;
        Assert.That(_cards.GetOffers("sys").First().UnavailableReason, Is.EqualTo("GalaxyLevelRequired"));
    }
    [Test] public void RunningUpgrade_ShowsUnavailableReason()
    {
        var ticket = Prepare(); _cards.Confirm(ticket.TicketId, out _);
        Assert.That(_cards.GetOffers("sys").First().UnavailableReason, Is.EqualTo("DevelopmentAlreadyRunning"));
    }
    [Test] public void Snapshot_IsDetachedFromLiveStation()
    { _cards.GetCard("sys").Station.Level = 10; Assert.That(System.Station.Level, Is.EqualTo(2)); }
    [Test] public void RepairPreview_ShowsProportionalInstantPrice()
    {
        System.ApplyInfrastructureDamageFromWar(135, false);
        var offer = _cards.GetOffers("sys").First(x => x.Kind == StationOperationKind.Repair);
        Assert.That(offer.Quote.DurationSeconds, Is.Zero); Assert.That(offer.Quote.PaidCosts[0].Amount, Is.EqualTo(30));
    }
    [Test] public void DestroyedStation_OffersRecoveryAndKeepsLevel()
    {
        System.ApplyInfrastructureDamageFromWar(100, true);
        var ticket = Prepare(StationOperationKind.Recovery); Assert.That(_cards.Confirm(ticket.TicketId, out _), Is.True);
        Assert.That(System.Station.Level, Is.EqualTo(2)); Assert.That(System.Station.IsDestroyed, Is.False);
    }
    [Test] public void ReentrantObserver_CannotReuseConfirmation()
    {
        var events = new SimpleEventBus();
        _stations = new StationService(_session, _ => _config, events, new StationResourcePaymentGateway(_session));
        _stations.InitializeSession(false); _cards = new StationCardService(_session, _stations, _ => _config, () => _position, _ => false);
        var ticket = Prepare(); bool replay = true;
        events.Subscribe<SaveNeedEvent>(evt => replay = _cards.Confirm(ticket.TicketId, out _));
        Assert.That(_cards.Confirm(ticket.TicketId, out _), Is.True); Assert.That(replay, Is.False);
        Assert.That(_session.State.Player.Diamonds, Is.EqualTo(90));
    }
    [Test] public void LoadingAnotherSession_InvalidatesEvenMatchingRevision()
    {
        var ticket = Prepare(); _session.LoadSession(JsonUtility.FromJson<GameRuntimeState>(JsonUtility.ToJson(_session.State)));
        Assert.That(_cards.Confirm(ticket.TicketId, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("StationStateChanged")); Assert.That(_session.State.Player.Diamonds, Is.EqualTo(100));
    }

}
