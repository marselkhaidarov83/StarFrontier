using System;
using NUnit.Framework;

namespace StarFrontier.Tests.Sprint1
{
    [TestFixture]
    public sealed class SimpleEventBusEditModeTests
    {
        private SimpleEventBus _eventBus;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new SimpleEventBus();
        }

        [TearDown]
        public void TearDown()
        {
            _eventBus.Clear();
        }

        [Test]
        public void SubscribeThenPublish_InvokesHandlerOnceWithPayload()
        {
            int callCount = 0;
            int receivedValue = 0;
            _eventBus.Subscribe<TestEvent>(evt =>
            {
                callCount++;
                receivedValue = evt.Value;
            });

            _eventBus.Publish(new TestEvent(42));

            Assert.That(callCount, Is.EqualTo(1));
            Assert.That(receivedValue, Is.EqualTo(42));
        }

        [Test]
        public void Unsubscribe_RemovesOnlySpecifiedHandler()
        {
            int firstCalls = 0;
            int secondCalls = 0;
            Action<TestEvent> first = _ => firstCalls++;
            Action<TestEvent> second = _ => secondCalls++;
            _eventBus.Subscribe(first);
            _eventBus.Subscribe(second);

            _eventBus.Unsubscribe(first);
            _eventBus.Publish(new TestEvent(1));

            Assert.That(firstCalls, Is.Zero);
            Assert.That(secondCalls, Is.EqualTo(1));
        }

        [Test]
        public void Clear_RemovesAllEventTypes()
        {
            int testEventCalls = 0;
            int otherEventCalls = 0;
            _eventBus.Subscribe<TestEvent>(_ => testEventCalls++);
            _eventBus.Subscribe<OtherEvent>(_ => otherEventCalls++);

            _eventBus.Clear();
            _eventBus.Publish(new TestEvent(1));
            _eventBus.Publish(new OtherEvent());

            Assert.That(testEventCalls, Is.Zero);
            Assert.That(otherEventCalls, Is.Zero);
            Assert.That(_eventBus.HasListeners<TestEvent>(), Is.False);
            Assert.That(_eventBus.HasListeners<OtherEvent>(), Is.False);
        }

        [Test]
        public void HandlerUnsubscribesItself_DoesNotBreakCurrentPublish_AndIsAbsentNextTime()
        {
            int selfCalls = 0;
            int stableCalls = 0;
            Action<TestEvent> selfRemoving = null;
            selfRemoving = _ =>
            {
                selfCalls++;
                _eventBus.Unsubscribe(selfRemoving);
            };
            _eventBus.Subscribe(selfRemoving);
            _eventBus.Subscribe<TestEvent>(_ => stableCalls++);

            Assert.DoesNotThrow(() => _eventBus.Publish(new TestEvent(1)));
            Assert.DoesNotThrow(() => _eventBus.Publish(new TestEvent(2)));

            Assert.That(selfCalls, Is.EqualTo(1));
            Assert.That(stableCalls, Is.EqualTo(2));
        }

        [Test]
        public void HandlerAddsSubscriber_NewSubscriberStartsOnNextPublish()
        {
            int lateCalls = 0;
            bool added = false;
            Action<TestEvent> late = _ => lateCalls++;
            _eventBus.Subscribe<TestEvent>(_ =>
            {
                if (added)
                    return;

                added = true;
                _eventBus.Subscribe(late);
            });

            _eventBus.Publish(new TestEvent(1));
            Assert.That(lateCalls, Is.Zero,
                "The active publication must use a stable subscriber snapshot.");

            _eventBus.Publish(new TestEvent(2));
            Assert.That(lateCalls, Is.EqualTo(1));
        }

        [Test]
        public void PublishWithoutSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _eventBus.Publish(new TestEvent(5)));
        }

        private readonly struct TestEvent
        {
            public TestEvent(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private readonly struct OtherEvent
        {
        }
    }
}
