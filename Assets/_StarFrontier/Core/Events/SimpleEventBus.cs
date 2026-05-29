using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarFrontier.Core.Events
{
    public sealed class SimpleEventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new();

        public void Subscribe<TEvent>(Action<TEvent> handler)
        {
            var type = typeof(TEvent);

            if (_handlers.TryGetValue(type, out var existing))
            {
                _handlers[type] = Delegate.Combine(existing, handler);
            }
            else
            {
                _handlers[type] = handler;
            }
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            var type = typeof(TEvent);

            if (!_handlers.TryGetValue(type, out var existing))
            {
                return;
            }

            var updated = Delegate.Remove(existing, handler);

            if (updated == null)
            {
                _handlers.Remove(type);
            }
            else
            {
                _handlers[type] = updated;
            }
        }

        public void Publish<TEvent>(TEvent eventData)
        {
            var type = typeof(TEvent);

            if (!_handlers.TryGetValue(type, out var handler))
            {
                return;
            }

            try
            {
                ((Action<TEvent>)handler)?.Invoke(eventData);
            }
            catch (Exception exception)
            {
                Debug.LogError($"SimpleEventBus: error while publishing {type.Name}: {exception}");
            }
        }

        public void Clear()
        {
            _handlers.Clear();
        }
    }
}