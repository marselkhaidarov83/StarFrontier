using System;
using NUnit.Framework;

/// <summary>
/// 2A-S06-04: runtime event contract.
/// The separate existing source-level UI tests still require Play Mode/Android evidence.
/// </summary>
public sealed class S06_04_WarNewsEventRuntimeTests
{
    [Test]
    public void NewsEvent_PreservesPriorityKindFactionSystemAndTick()
    {
        var evt = new WarNewsItemCreatedEvent(
            WarNewsEventKind.InvasionStarted,
            "system_a", "ai", "Warning", "Enemy fleet incoming", 42, 100);

        Assert.That(evt.Kind, Is.EqualTo(WarNewsEventKind.InvasionStarted));
        Assert.That(evt.SystemId, Is.EqualTo("system_a"));
        Assert.That(evt.FactionId, Is.EqualTo("ai"));
        Assert.That(evt.Title, Is.EqualTo("Warning"));
        Assert.That(evt.Body, Is.EqualTo("Enemy fleet incoming"));
        Assert.That(evt.CreatedAtTick, Is.EqualTo(42));
        Assert.That(evt.Priority, Is.EqualTo(100));
    }

    [Test]
    public void NewsEvent_NullTextDoesNotReachUiAsNull()
    {
        var evt = new WarNewsItemCreatedEvent(
            WarNewsEventKind.SystemCaptured,
            null, null, null, null, 2, 100);

        Assert.That(evt.SystemId, Is.Empty);
        Assert.That(evt.FactionId, Is.Empty);
        Assert.That(evt.Title, Is.Empty);
        Assert.That(evt.Body, Is.Empty);
    }

    [Test]
    public void EventBus_DeliversWarNewsAndStopsAfterUnsubscribe()
    {
        var bus = new SimpleEventBus();
        int received = 0;
        WarNewsEventKind lastKind = WarNewsEventKind.InvasionStarted;
        Action<WarNewsItemCreatedEvent> handler = evt =>
        {
            received++;
            lastKind = evt.Kind;
        };

        try
        {
            bus.Subscribe(handler);
            bus.Publish(new WarNewsItemCreatedEvent(
                WarNewsEventKind.SystemLiberated,
                "system_a", "ai", "Liberated", "Clear", 3, 50));
            bus.Unsubscribe(handler);
            bus.Publish(new WarNewsItemCreatedEvent(
                WarNewsEventKind.FrontChanged,
                "system_b", "ai", "Front", "Updated", 4, 10));

            Assert.That(received, Is.EqualTo(1));
            Assert.That(lastKind, Is.EqualTo(WarNewsEventKind.SystemLiberated));
        }
        finally
        {
            bus.Clear();
        }
    }

    [Test]
    public void NewsKinds_IncludeSevenSeparateWarOutcomes()
    {
        Assert.That(Enum.GetValues(typeof(WarNewsEventKind)).Length, Is.EqualTo(7));
        Assert.That(WarNewsEventKind.InfrastructureDamaged,
            Is.Not.EqualTo(WarNewsEventKind.InfrastructureDestroyed));
        Assert.That(WarNewsEventKind.SystemCaptured,
            Is.Not.EqualTo(WarNewsEventKind.SystemLiberated));
    }
}
