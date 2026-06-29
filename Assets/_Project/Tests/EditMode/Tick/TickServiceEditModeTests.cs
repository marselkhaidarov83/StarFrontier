using System;
using System.Collections.Generic;
using NUnit.Framework;

public sealed class TickServiceEditModeTests
{
    [Test]
    public void RegisterAndTick_PassesDeltaTime()
    {
        TickService service = new TickService();
        RecordingTickable tickable = new RecordingTickable();

        service.Register(tickable);
        service.Tick(0.25f);

        Assert.AreEqual(1, tickable.CallCount);
        Assert.AreEqual(0.25f, tickable.LastDeltaTime);
        Assert.AreEqual(1, service.Count);
    }

    [Test]
    public void Tick_WithZeroDeltaTime_IsAllowed()
    {
        TickService service = new TickService();
        RecordingTickable tickable = new RecordingTickable();

        service.Register(tickable);
        service.Tick(0f);

        Assert.AreEqual(1, tickable.CallCount);
        Assert.AreEqual(0f, tickable.LastDeltaTime);
    }

    [Test]
    public void Tick_UsesOrderAndStableRegistrationOrder()
    {
        TickService service = new TickService();
        List<string> calls = new List<string>();

        service.Register(
            new DelegateTickable(_ => calls.Add("late")),
            100);

        service.Register(
            new DelegateTickable(_ => calls.Add("first")),
            -100);

        service.Register(
            new DelegateTickable(_ => calls.Add("middle-a")),
            0);

        service.Register(
            new DelegateTickable(_ => calls.Add("middle-b")),
            0);

        service.Tick(0.1f);

        CollectionAssert.AreEqual(
            new[]
            {
                "first",
                "middle-a",
                "middle-b",
                "late"
            },
            calls);
    }

    [Test]
    public void TickOrder_HasExpectedServiceSequence()
    {
        Assert.Less(
            TickOrder.GameTime,
            TickOrder.PlayerInput);

        Assert.Less(
            TickOrder.PlayerInput,
            TickOrder.PlayerControl);

        Assert.Less(
            TickOrder.PlayerControl,
            TickOrder.ShipMovement);

        Assert.Less(
            TickOrder.ShipMovement,
            TickOrder.Targeting);

        Assert.Less(
            TickOrder.Targeting,
            TickOrder.Interaction);

        Assert.Less(
            TickOrder.Interaction,
            TickOrder.Camera);

        Assert.Less(
            TickOrder.Camera,
            TickOrder.Hud);

        Assert.Less(
            TickOrder.Hud,
            TickOrder.Debug);
    }

    [Test]
    public void RegisterSameInstance_Throws()
    {
        TickService service = new TickService();
        RecordingTickable tickable = new RecordingTickable();

        service.Register(tickable);

        Assert.Throws<InvalidOperationException>(
            () => service.Register(tickable));
    }

    [Test]
    public void RegisterNull_ThrowsArgumentNullException()
    {
        TickService service = new TickService();

        Assert.Throws<ArgumentNullException>(
            () => service.Register(null));
    }

    [Test]
    public void ContainsNull_ReturnsFalse()
    {
        TickService service = new TickService();

        Assert.IsFalse(service.Contains(null));
    }

    [Test]
    public void UnregisterNull_ReturnsFalse()
    {
        TickService service = new TickService();

        Assert.IsFalse(service.Unregister(null));
    }

    [Test]
    public void Unregister_PreventsFurtherTicks()
    {
        TickService service = new TickService();
        RecordingTickable tickable = new RecordingTickable();

        service.Register(tickable);

        bool removed = service.Unregister(tickable);

        service.Tick(0.1f);

        Assert.IsTrue(removed);
        Assert.IsFalse(service.Contains(tickable));
        Assert.AreEqual(0, tickable.CallCount);
        Assert.AreEqual(0, service.Count);
    }

    [Test]
    public void RegisterDuringTick_StartsOnNextTick()
    {
        TickService service = new TickService();
        RecordingTickable lateTickable = new RecordingTickable();

        DelegateTickable registeringTickable =
            new DelegateTickable(_ =>
            {
                if (!service.Contains(lateTickable))
                {
                    service.Register(lateTickable, 100);
                }
            });

        service.Register(registeringTickable, 0);

        service.Tick(0.1f);

        Assert.AreEqual(
            0,
            lateTickable.CallCount,
            "Зарегистрированный во время Tick объект " +
            "не должен выполняться в том же Tick.");

        service.Tick(0.1f);

        Assert.AreEqual(1, lateTickable.CallCount);
    }

    [Test]
    public void UnregisterDuringTick_PreventsLaterInvocation()
    {
        TickService service = new TickService();
        RecordingTickable laterTickable = new RecordingTickable();

        DelegateTickable unregisteringTickable =
            new DelegateTickable(_ =>
            {
                service.Unregister(laterTickable);
            });

        service.Register(unregisteringTickable, 0);
        service.Register(laterTickable, 100);

        service.Tick(0.1f);

        Assert.AreEqual(0, laterTickable.CallCount);
        Assert.IsFalse(service.Contains(laterTickable));
    }

    [Test]
    public void UnregisterAndRegisterSameInstanceDuringTick_StartsOnNextTick()
    {
        TickService service = new TickService();
        RecordingTickable target = new RecordingTickable();

        bool registrationWasReplaced = false;

        DelegateTickable replacingTickable =
            new DelegateTickable(_ =>
            {
                if (registrationWasReplaced)
                    return;

                service.Unregister(target);
                service.Register(target, 100);

                registrationWasReplaced = true;
            });

        service.Register(replacingTickable, 0);
        service.Register(target, 100);

        service.Tick(0.1f);

        Assert.AreEqual(
            0,
            target.CallCount,
            "Повторно зарегистрированный объект должен " +
            "начать работу со следующего Tick.");

        service.Tick(0.1f);

        Assert.AreEqual(1, target.CallCount);
    }

    [Test]
    public void Clear_RemovesAllTickables()
    {
        TickService service = new TickService();
        RecordingTickable first = new RecordingTickable();
        RecordingTickable second = new RecordingTickable();

        service.Register(first);
        service.Register(second);

        service.Clear();
        service.Tick(0.1f);

        Assert.AreEqual(0, service.Count);
        Assert.AreEqual(0, first.CallCount);
        Assert.AreEqual(0, second.CallCount);
    }

    [Test]
    public void ClearDuringTick_PreventsLaterInvocation()
    {
        TickService service = new TickService();
        RecordingTickable laterTickable = new RecordingTickable();

        DelegateTickable clearingTickable =
            new DelegateTickable(_ =>
            {
                service.Clear();
            });

        service.Register(clearingTickable, 0);
        service.Register(laterTickable, 100);

        service.Tick(0.1f);

        Assert.AreEqual(0, laterTickable.CallCount);
        Assert.AreEqual(0, service.Count);
        Assert.IsFalse(service.IsTicking);
    }

    [Test]
    public void Tick_WithInvalidDeltaTime_Throws()
    {
        TickService service = new TickService();

        float[] invalidValues =
        {
            -0.01f,
            float.NaN,
            float.PositiveInfinity,
            float.NegativeInfinity
        };

        foreach (float invalidValue in invalidValues)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => service.Tick(invalidValue));
        }
    }

    [Test]
    public void RecursiveTick_ThrowsAndRestoresState()
    {
        TickService service = new TickService();

        DelegateTickable recursiveTickable =
            new DelegateTickable(_ =>
            {
                service.Tick(0.1f);
            });

        service.Register(recursiveTickable);

        Assert.Throws<InvalidOperationException>(
            () => service.Tick(0.1f));

        Assert.IsFalse(service.IsTicking);
    }

    [Test]
    public void GameTimeServiceInterface_IsTickable()
    {
        bool isTickable =
            typeof(ITickable).IsAssignableFrom(
                typeof(IGameTimeService));

        Assert.IsTrue(
            isTickable,
            "IGameTimeService должен наследовать ITickable, " +
            "потому что в S3-06 GameTimeService обновляется " +
            "через TickService.");
    }

    private sealed class RecordingTickable : ITickable
    {
        public int CallCount { get; private set; }

        public float LastDeltaTime { get; private set; }

        public void Tick(float deltaTime)
        {
            CallCount++;
            LastDeltaTime = deltaTime;
        }
    }

    private sealed class DelegateTickable : ITickable
    {
        private readonly Action<float> _onTick;

        public DelegateTickable(Action<float> onTick)
        {
            _onTick = onTick;
        }

        public void Tick(float deltaTime)
        {
            _onTick?.Invoke(deltaTime);
        }
    }
}