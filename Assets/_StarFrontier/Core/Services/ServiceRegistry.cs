using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ServiceRegistry
{
    private readonly Dictionary<Type, object> _services = new();

    public void Register<T>(T service) where T : class
    {
        var type = typeof(T);

        if (service == null)
        {
            Debug.LogError($"ServiceRegistry: cannot register null service {type.Name}");
            return;
        }

        if (_services.ContainsKey(type))
        {
            Debug.LogWarning($"ServiceRegistry: service {type.Name} already registered. It will be replaced.");
        }

        _services[type] = service;
    }

    public T Get<T>() where T : class
    {
        var type = typeof(T);

        if (_services.TryGetValue(type, out var service))
        {
            return service as T;
        }

        Debug.LogError($"ServiceRegistry: service {type.Name} not found.");
        return null;
    }

    public bool TryGet<T>(out T service) where T : class
    {
        var type = typeof(T);

        if (_services.TryGetValue(type, out var rawService))
        {
            service = rawService as T;
            return service != null;
        }

        service = null;
        return false;
    }

    public bool Has<T>() where T : class
    {
        return _services.ContainsKey(typeof(T));
    }

    public void Unregister<T>() where T : class
    {
        var type = typeof(T);

        if (_services.ContainsKey(type))
        {
            _services.Remove(type);
        }
    }

    public void Clear()
    {
        _services.Clear();
    }
}