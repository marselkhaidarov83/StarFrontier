using System;
using System.Collections.Generic;

public class ServiceRegistry : IServiceRegistry
{
    private readonly Dictionary<Type, object> _services = new();

    public void Register<T>(T service)
    {
        Type serviceType = typeof(T);

        if (_services.ContainsKey(serviceType))
        {
            throw new InvalidOperationException($"Service of type {serviceType.Name} is already registered.");
        }

        _services[serviceType] = service!;
    }

    public T Get<T>()
    {
        Type serviceType = typeof(T);

        if (_services.TryGetValue(serviceType, out object service))
        {
            return (T)service;
        }

        throw new InvalidOperationException($"Service of type {serviceType.Name} is not registered.");
    }

    public bool TryGet<T>(out T service)
    {
        Type serviceType = typeof(T);

        if (_services.TryGetValue(serviceType, out object foundService))
        {
            service = (T)foundService;
            return true;
        }

        service = default!;
        return false;
    }
}