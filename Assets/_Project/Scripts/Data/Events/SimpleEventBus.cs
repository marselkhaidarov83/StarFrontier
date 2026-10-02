using System;
using System.Collections.Generic;
using UnityEngine;

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

    public void PublishProfiled<T>(
        T eventData,
        double slowSubscriberThresholdMs,
        double slowPublishThresholdMs,
        Action<string, double, int, int> onSlowSubscriber,
        Action<double, int, string, double> onSlowPublish)
    {
        var eventType = typeof(T);

        if (!_handlers.TryGetValue(eventType, out var handler))
            return;

        Action<T> typedHandler =
            handler as Action<T>;

        if (typedHandler == null)
            return;

        Delegate[] subscribers =
            typedHandler.GetInvocationList();

        long publishStartedAt =
            BeginPerfMeasure();

        double maxSubscriberMs = 0d;
        string maxSubscriberName = string.Empty;

        for (int i = 0; i < subscribers.Length; i++)
        {
            Action<T> subscriber =
                subscribers[i] as Action<T>;

            if (subscriber == null)
                continue;

            long subscriberStartedAt =
                BeginPerfMeasure();

            subscriber.Invoke(eventData);

            double subscriberMs =
                EndPerfMeasureMs(subscriberStartedAt);

            string subscriberName =
                GetSubscriberName(subscriber);

            if (subscriberMs > maxSubscriberMs)
            {
                maxSubscriberMs = subscriberMs;
                maxSubscriberName = subscriberName;
            }

            if (subscriberMs >= slowSubscriberThresholdMs &&
                onSlowSubscriber != null)
            {
                onSlowSubscriber.Invoke(
                    subscriberName,
                    subscriberMs,
                    i,
                    subscribers.Length);
            }
        }

        double publishMs =
            EndPerfMeasureMs(publishStartedAt);

        if (publishMs >= slowPublishThresholdMs &&
            onSlowPublish != null)
        {
            onSlowPublish.Invoke(
                publishMs,
                subscribers.Length,
                maxSubscriberName,
                maxSubscriberMs);
        }
    }

    public bool HasListeners<TEvent>()
    {
        return _handlers.ContainsKey(typeof(TEvent));
    }

    public void Clear()
    {
        _handlers.Clear();
        Debug.Log("[SimpleEventBus] Cleared all listeners");
    }

    private static string GetSubscriberName(Delegate subscriber)
    {
        if (subscriber == null)
            return "Unknown";

        string targetName =
            subscriber.Target != null
                ? subscriber.Target.GetType().Name
                : "Static";

        string methodName =
            subscriber.Method != null
                ? subscriber.Method.Name
                : "UnknownMethod";

        return targetName + "." + methodName;
    }

    private static long BeginPerfMeasure()
    {
        return System.Diagnostics.Stopwatch.GetTimestamp();
    }

    private static double EndPerfMeasureMs(long startedAt)
    {
        long elapsedTicks =
            System.Diagnostics.Stopwatch.GetTimestamp() - startedAt;

        return elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }
}