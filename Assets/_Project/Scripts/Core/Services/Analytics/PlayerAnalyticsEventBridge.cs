using System;

public sealed class PlayerAnalyticsEventBridge : IDisposable
{
    private readonly SimpleEventBus _bus;
    private readonly IPlayerAnalytics _analytics;
    private PlayerAnalyticsSnapshot _travelBefore;
    private bool _disposed;

    public PlayerAnalyticsEventBridge(SimpleEventBus bus, IPlayerAnalytics analytics)
    {
        _bus = bus; _analytics = analytics;
        bus.Subscribe<TravelStartedEvent>(TravelStarted);
        bus.Subscribe<TravelFinishedEvent>(TravelFinished);
        bus.Subscribe<InteractionExecutionEvent2A>(Interaction);
        bus.Subscribe<CombatRuntimeStartedEvent2A>(CombatStarted);
        bus.Subscribe<CombatVictoryEvent2A>(CombatVictory);
        bus.Subscribe<CombatDefeatEvent2A>(CombatDefeat);
        bus.Subscribe<PlayerShipDestroyedByNpcEvent>(PlayerDestroyed);
    }

    private void TravelStarted(TravelStartedEvent value)
    {
        try { _travelBefore = _analytics.Capture(); } catch { _travelBefore = null; }
    }
    private void TravelFinished(TravelFinishedEvent value)
    {
        try { _analytics.Track(PlayerAnalyticsAction.Travel, value.Success, value.FailReason.ToString(), _travelBefore, value.ToSystemId); }
        catch { }
        finally { _travelBefore = null; }
    }
    private void Interaction(InteractionExecutionEvent2A value)
    {
        if (value?.Result == null) return;
        Safe(PlayerAnalyticsAction.Interaction, value.Result.Success, value.Result.FailReason.ToString(), value.Result.Descriptor?.ActionId);
    }
    private void CombatStarted(CombatRuntimeStartedEvent2A value) => Safe(PlayerAnalyticsAction.CombatStarted, true, "None", value.EncounterId);
    private void CombatVictory(CombatVictoryEvent2A value) => Safe(PlayerAnalyticsAction.CombatVictory, true, "None", value.EncounterId);
    private void CombatDefeat(CombatDefeatEvent2A value) => Safe(PlayerAnalyticsAction.CombatDefeat, false, value.Reason.ToString(), value.EncounterId);
    private void PlayerDestroyed(PlayerShipDestroyedByNpcEvent value) => Safe(PlayerAnalyticsAction.PlayerDestroyed, false, "DestroyedByNpc", null);
    private void Safe(PlayerAnalyticsAction action, bool success, string reason, string id)
    {
        try { _analytics.Track(action, success, reason, contentId: id); } catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _bus.Unsubscribe<TravelStartedEvent>(TravelStarted);
        _bus.Unsubscribe<TravelFinishedEvent>(TravelFinished);
        _bus.Unsubscribe<InteractionExecutionEvent2A>(Interaction);
        _bus.Unsubscribe<CombatRuntimeStartedEvent2A>(CombatStarted);
        _bus.Unsubscribe<CombatVictoryEvent2A>(CombatVictory);
        _bus.Unsubscribe<CombatDefeatEvent2A>(CombatDefeat);
        _bus.Unsubscribe<PlayerShipDestroyedByNpcEvent>(PlayerDestroyed);
    }
}
