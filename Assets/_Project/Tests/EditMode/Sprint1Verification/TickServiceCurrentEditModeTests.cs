using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace StarFrontier.Tests.Sprint1
{
    [TestFixture]
    public sealed class TickServiceCurrentEditModeTests
    {
        [Test]
        public void RegisterAndTick_PassesDeltaTime()
        {
            var service = new TickService();
            var tickable = new RecordingTickable();

            service.Register(tickable);
            service.Tick(0.25f);

            Assert.That(tickable.CallCount, Is.EqualTo(1));
            Assert.That(tickable.LastDeltaTime, Is.EqualTo(0.25f));
            Assert.That(service.Count, Is.EqualTo(1));
        }

        [Test]
        public void Tick_WithZeroDeltaTime_IsAllowed()
        {
            var service = new TickService();
            var tickable = new RecordingTickable();

            service.Register(tickable);
            service.Tick(0f);

            Assert.That(tickable.CallCount, Is.EqualTo(1));
            Assert.That(tickable.LastDeltaTime, Is.Zero);
        }

        [Test]
        public void Tick_UsesOrderAndStableRegistrationOrder()
        {
            var service = new TickService();
            var calls = new List<string>();

            service.Register(new DelegateTickable(_ => calls.Add("late")), 100);
            service.Register(new DelegateTickable(_ => calls.Add("first")), -100);
            service.Register(new DelegateTickable(_ => calls.Add("middle-a")), 0);
            service.Register(new DelegateTickable(_ => calls.Add("middle-b")), 0);

            service.Tick(0.1f);

            Assert.That(calls, Is.EqualTo(new[]
            {
                "first",
                "middle-a",
                "middle-b",
                "late"
            }));
        }

        [Test]
        public void TickOrder_HasExpectedServiceSequence()
        {
            Assert.That(TickOrder.GameTime, Is.LessThan(TickOrder.PlayerInput));
            Assert.That(TickOrder.PlayerInput, Is.LessThan(TickOrder.PlayerControl));
            Assert.That(TickOrder.PlayerControl, Is.LessThan(TickOrder.ShipMovement));
            Assert.That(TickOrder.ShipMovement, Is.LessThan(TickOrder.Targeting));
            Assert.That(TickOrder.Targeting, Is.LessThan(TickOrder.Interaction));
            Assert.That(TickOrder.Interaction, Is.LessThan(TickOrder.Camera));
            Assert.That(TickOrder.Camera, Is.LessThan(TickOrder.Hud));
            Assert.That(TickOrder.Hud, Is.LessThan(TickOrder.Debug));
        }

        [Test]
        public void RegisterSameInstance_ThrowsAndKeepsOriginalRegistration()
        {
            var service = new TickService();
            var tickable = new RecordingTickable();
            service.Register(tickable);

            Assert.Throws<InvalidOperationException>(() => service.Register(tickable));
            Assert.That(service.Count, Is.EqualTo(1));
            Assert.That(service.Contains(tickable), Is.True);
        }

        [Test]
        public void RegisterNull_ThrowsArgumentNullException()
        {
            var service = new TickService();

            Assert.Throws<ArgumentNullException>(() => service.Register(null));
            Assert.That(service.Count, Is.Zero);
        }

        [Test]
        public void ContainsNull_ReturnsFalse()
        {
            Assert.That(new TickService().Contains(null), Is.False);
        }

        [Test]
        public void UnregisterNull_ReturnsFalse()
        {
            Assert.That(new TickService().Unregister(null), Is.False);
        }

        [Test]
        public void Unregister_PreventsFurtherTicks()
        {
            var service = new TickService();
            var tickable = new RecordingTickable();
            service.Register(tickable);

            bool removed = service.Unregister(tickable);
            service.Tick(0.1f);

            Assert.That(removed, Is.True);
            Assert.That(service.Contains(tickable), Is.False);
            Assert.That(tickable.CallCount, Is.Zero);
            Assert.That(service.Count, Is.Zero);
        }

        [Test]
        public void RegisterDuringTick_StartsOnNextTick()
        {
            var service = new TickService();
            var lateTickable = new RecordingTickable();
            var registeringTickable = new DelegateTickable(_ =>
            {
                if (!service.Contains(lateTickable))
                    service.Register(lateTickable, 100);
            });

            service.Register(registeringTickable, 0);
            service.Tick(0.1f);

            Assert.That(lateTickable.CallCount, Is.Zero,
                "A tickable registered during Tick must start on the next Tick.");

            service.Tick(0.1f);
            Assert.That(lateTickable.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void UnregisterDuringTick_PreventsLaterInvocation()
        {
            var service = new TickService();
            var laterTickable = new RecordingTickable();
            var unregisteringTickable =
                new DelegateTickable(_ => service.Unregister(laterTickable));

            service.Register(unregisteringTickable, 0);
            service.Register(laterTickable, 100);
            service.Tick(0.1f);

            Assert.That(laterTickable.CallCount, Is.Zero);
            Assert.That(service.Contains(laterTickable), Is.False);
        }

        [Test]
        public void UnregisterAndRegisterSameInstanceDuringTick_StartsOnNextTick()
        {
            var service = new TickService();
            var target = new RecordingTickable();
            bool replaced = false;
            var replacingTickable = new DelegateTickable(_ =>
            {
                if (replaced)
                    return;

                service.Unregister(target);
                service.Register(target, 100);
                replaced = true;
            });

            service.Register(replacingTickable, 0);
            service.Register(target, 100);
            service.Tick(0.1f);

            Assert.That(target.CallCount, Is.Zero);

            service.Tick(0.1f);
            Assert.That(target.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void Clear_RemovesAllTickables()
        {
            var service = new TickService();
            var first = new RecordingTickable();
            var second = new RecordingTickable();
            service.Register(first);
            service.Register(second);

            service.Clear();
            service.Tick(0.1f);

            Assert.That(service.Count, Is.Zero);
            Assert.That(first.CallCount, Is.Zero);
            Assert.That(second.CallCount, Is.Zero);
        }

        [Test]
        public void ClearDuringTick_PreventsLaterInvocationAndRestoresState()
        {
            var service = new TickService();
            var laterTickable = new RecordingTickable();
            var clearingTickable = new DelegateTickable(_ => service.Clear());
            service.Register(clearingTickable, 0);
            service.Register(laterTickable, 100);

            service.Tick(0.1f);

            Assert.That(laterTickable.CallCount, Is.Zero);
            Assert.That(service.Count, Is.Zero);
            Assert.That(service.IsTicking, Is.False);
        }

        [Test]
        public void Tick_WithInvalidDeltaTime_ThrowsWithoutEnteringTick()
        {
            var service = new TickService();
            float[] invalidValues =
            {
                -0.01f,
                float.NaN,
                float.PositiveInfinity,
                float.NegativeInfinity
            };

            foreach (float invalidValue in invalidValues)
                Assert.Throws<ArgumentOutOfRangeException>(() => service.Tick(invalidValue));

            Assert.That(service.IsTicking, Is.False);
        }

        [Test]
        public void RecursiveTick_ThrowsAndRestoresState()
        {
            var service = new TickService();
            service.Register(new DelegateTickable(_ => service.Tick(0.1f)));

            Assert.Throws<InvalidOperationException>(() => service.Tick(0.1f));
            Assert.That(service.IsTicking, Is.False);
        }

        [Test]
        public void GameTimeServiceInterface_IsTickable()
        {
            Assert.That(typeof(ITickable).IsAssignableFrom(typeof(IGameTimeService)), Is.True,
                "IGameTimeService must be updated through the canonical TickService.");
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
}
