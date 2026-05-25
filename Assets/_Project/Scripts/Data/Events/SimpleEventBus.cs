using System;
using System.Collections.Generic;

public sealed class SimpleEventBus
{
    private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

    public void Subscribe<T>(Action<T> handler)
    {
        var eventType = typeof(T);

        if (_handlers.TryGetValue(eventType, out var existing))
        {
            _handlers[eventType] = Delegate.Combine(existing, handler);
        }
        else
        {
            _handlers[eventType] = handler;
        }
    }

    public void Unsubscribe<T>(Action<T> handler)
    {
        var eventType = typeof(T);

        if (!_handlers.TryGetValue(eventType, out var existing))
            return;

        var updated = Delegate.Remove(existing, handler);

        if (updated == null)
            _handlers.Remove(eventType);
        else
            _handlers[eventType] = updated;
    }

    public void Publish<T>(T eventData)
    {
        var eventType = typeof(T);

        if (_handlers.TryGetValue(eventType, out var handler))
        {
            ((Action<T>)handler)?.Invoke(eventData);
        }
    }
}