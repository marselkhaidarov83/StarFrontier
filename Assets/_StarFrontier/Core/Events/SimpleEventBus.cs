using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SimpleEventBus
{
    private readonly Dictionary<Type, Delegate> _handlersByEventType = new();

    public void Subscribe<TEvent>(Action<TEvent> handler)
    {
        if (handler == null)
        {
            Debug.LogWarning($"SimpleEventBus: null handler for {typeof(TEvent).Name} skipped.");
            return;
        }

        var eventType = typeof(TEvent);

        if (_handlersByEventType.TryGetValue(eventType, out var existingHandler))
        {
            _handlersByEventType[eventType] = Delegate.Combine(existingHandler, handler);
        }
        else
        {
            _handlersByEventType[eventType] = handler;
        }
    }

    public void Unsubscribe<TEvent>(Action<TEvent> handler)
    {
        if (handler == null)
        {
            return;
        }

        var eventType = typeof(TEvent);

        if (!_handlersByEventType.TryGetValue(eventType, out var existingHandler))
        {
            return;
        }

        var updatedHandler = Delegate.Remove(existingHandler, handler);

        if (updatedHandler == null)
        {
            _handlersByEventType.Remove(eventType);
        }
        else
        {
            _handlersByEventType[eventType] = updatedHandler;
        }
    }

    public void Publish<TEvent>(TEvent eventData)
    {
        var eventType = typeof(TEvent);

        if (!_handlersByEventType.TryGetValue(eventType, out var handler))
        {
            return;
        }

        try
        {
            ((Action<TEvent>)handler)?.Invoke(eventData);
        }
        catch (Exception exception)
        {
            Debug.LogError($"SimpleEventBus: error while publishing {eventType.Name}: {exception}");
        }
    }

    public void Clear()
    {
        _handlersByEventType.Clear();
    }
}